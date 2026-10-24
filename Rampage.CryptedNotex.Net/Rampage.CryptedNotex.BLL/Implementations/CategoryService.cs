using Rampage.AppLog;
using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.DependencyInjection;
using Rampage.Hierarchy;
using Rampage.Model;
using Rampage.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 分类服务实现
    /// </summary>
    public class CategoryService : BaseService<Category, ICategoryDao>, ICategoryService
    {
        /// <summary>
        /// 分类数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected ICategoryDao CategoryDao = default!;

        /// <summary>
        /// 笔记数据访问，由 [Auto] 注入(删除分类迁移笔记/重加密取笔记)
        /// </summary>
        [Auto]
        protected INoteDao NoteDao = default!;

        /// <summary>
        /// 笔记服务，由 [Auto] 注入(独立密码三流程复用其 ReencryptList)
        /// </summary>
        [Auto]
        protected INoteService NoteService = default!;

        /// <inheritdoc />
        public List<Category> GetCategoryList(int userId)
        {
            List<Category> categories = CategoryDao.SelectList(m => m.UserId == userId);
            // 内存排序兜底：DAO 谓词重载不携带排序
            return categories.OrderBy(m => m.SortOrder).ThenBy(m => m.Id).ToList();
        }

        /// <inheritdoc />
        public R SaveCategory(Category category)
        {
            string name = (category.Name ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return R.Failure("分类名不能为空");
            }
            if (CategoryDao.SelectOne(m => m.UserId == category.UserId && m.Name == name && m.Id != category.Id) != null)
            {
                return R.Failure("同名分类已存在");
            }

            if (category.Id > 0)
            {
                Category? existing = CategoryDao.SelectById(category.Id);
                if (existing == null)
                {
                    return R.Failure("分类不存在");
                }
                existing.Name = name;
                existing.UpdateTime = UtcNow;
                return CategoryDao.UpdateById(existing) ? R.Success(category.Id) : R.Failure();
            }

            // 新建分类默认跟随全局(独立密码走 SetCategoryIndependentPassword 流程)
            List<Category> userCategories = CategoryDao.SelectList(m => m.UserId == category.UserId);
            category.Name = name;
            category.IsDefault = 0;
            category.EncryptMode = (int)CategoryEncryptMode.FollowUser;
            category.PasswordHash = null;
            category.KdfSalt = null;
            category.KdfIterations = CryptoTool.KDF_ITERATIONS_DEFAULT;
            category.SortOrder = userCategories.Count == 0 ? 0 : userCategories.Max(m => m.SortOrder) + 1;
            category.CreateTime = UtcNow;
            return CategoryDao.Insert(category) ? R.Success(category.Id) : R.Failure();
        }

        /// <inheritdoc />
        public R RemoveCategory(int userId, int categoryId, bool moveNotesToDefault)
        {
            Category? category = CategoryDao.SelectById(categoryId);
            if (category == null || category.UserId != userId)
            {
                return R.Failure("分类不存在");
            }
            if (category.IsDefault == 1)
            {
                return R.Failure("默认分类不可删除");
            }

            Category? defaultCategory = CategoryDao.SelectOne(m => m.UserId == userId && m.IsDefault == 1);
            if (defaultCategory == null)
            {
                return R.Failure("默认分类缺失，数据异常");
            }

            long noteCount = NoteDao.Count(m => m.UserId == userId && m.CategoryId == categoryId);
            if (noteCount > 0 && !moveNotesToDefault)
            {
                return R.Failure("分类下还有 " + noteCount + " 条笔记，删除前须将笔记移入默认分类");
            }

            BeginTransaction();
            try
            {
                if (noteCount > 0)
                {
                    NoteDao.Update(m => m.UserId == userId && m.CategoryId == categoryId, m => m.CategoryId, defaultCategory.Id);
                }
                CategoryDao.DeleteById(categoryId);
                CommitTransaction();
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                RampageLogger.Error(ex);
                return R.Failure("删除分类失败：" + ex.Message);
            }
            return R.Success();
        }

        /// <inheritdoc />
        public R UpdateCategorySort(int userId, IEnumerable<int> orderedCategoryIds)
        {
            List<Category> categories = CategoryDao.SelectList(m => m.UserId == userId);
            Dictionary<int, Category> categoryMap = categories.ToDictionary(m => m.Id);
            int order = 0;
            foreach (int categoryId in orderedCategoryIds)
            {
                if (categoryMap.TryGetValue(categoryId, out Category? category))
                {
                    category.SortOrder = order++;
                }
            }

            try
            {
                CategoryDao.UpdateBatch(categories);
            }
            catch (Exception ex)
            {
                RampageLogger.Error(ex);
                return R.Failure("调整排序失败：" + ex.Message);
            }
            return R.Success();
        }

        /// <inheritdoc />
        public bool TryUnlockCategory(int categoryId, string password, out byte[]? categoryKey)
        {
            Category? category = CategoryDao.SelectById(categoryId);
            if (category == null || category.EncryptMode != (int)CategoryEncryptMode.IndependentPassword
                || string.IsNullOrEmpty(category.PasswordHash) || string.IsNullOrEmpty(category.KdfSalt))
            {
                categoryKey = null;
                return false;
            }
            if (!SecurityTool.PBKDF2Verify(password, category.PasswordHash))
            {
                categoryKey = null;
                return false;
            }

            categoryKey = CryptoTool.DeriveContentKey(password, category.KdfSalt, category.KdfIterations);
            return true;
        }

        /// <inheritdoc />
        public R SetCategoryIndependentPassword(int userId, int categoryId, string categoryPassword, byte[] userContentKey, IProgress<int>? progress)
        {
            Category? category = CategoryDao.SelectById(categoryId);
            if (category == null || category.UserId != userId)
            {
                return R.Failure("分类不存在");
            }
            if (category.EncryptMode != (int)CategoryEncryptMode.FollowUser)
            {
                return R.Failure("该分类已是独立密码模式");
            }
            if (string.IsNullOrEmpty(categoryPassword) || categoryPassword.Length < 6)
            {
                return R.Failure("独立密码至少 6 位");
            }

            byte[]? categoryKey = null;
            string salt = CryptoTool.GenerateSaltBase64();
            categoryKey = CryptoTool.DeriveContentKey(categoryPassword, salt, CryptoTool.KDF_ITERATIONS_DEFAULT);
            List<Note> notes = NoteDao.GetNoteListByCategory(userId, categoryId);
            progress?.Report(10);

            BeginTransaction();
            try
            {
                category.EncryptMode = (int)CategoryEncryptMode.IndependentPassword;
                category.PasswordHash = SecurityTool.PBKDF2Hash(categoryPassword);
                category.KdfSalt = salt;
                category.KdfIterations = CryptoTool.KDF_ITERATIONS_DEFAULT;
                category.UpdateTime = UtcNow;
                CategoryDao.UpdateById(category);

                if (notes.Count > 0)
                {
                    R reencryptResult = NoteService.ReencryptList(notes, userContentKey, categoryKey, new RangeProgress(progress, 15, 95));
                    if (!reencryptResult.IsSuccess)
                    {
                        RollbackTransaction();
                        return R.Failure("重加密失败，已回滚：" + reencryptResult.Message);
                    }
                }
                CommitTransaction();
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                RampageLogger.Error(ex);
                return R.Failure("设置独立密码失败，已回滚：" + ex.Message);
            }

            progress?.Report(100);
            return R.Success(categoryKey);
        }

        /// <inheritdoc />
        public R ResetCategoryPassword(int userId, int categoryId, string oldCategoryPassword, string newCategoryPassword, IProgress<int>? progress)
        {
            Category? category = CategoryDao.SelectById(categoryId);
            if (category == null || category.UserId != userId)
            {
                return R.Failure("分类不存在");
            }
            if (category.EncryptMode != (int)CategoryEncryptMode.IndependentPassword)
            {
                return R.Failure("该分类不是独立密码模式");
            }
            if (string.IsNullOrEmpty(category.PasswordHash) || !SecurityTool.PBKDF2Verify(oldCategoryPassword, category.PasswordHash))
            {
                return R.Failure("旧独立密码不正确");
            }
            if (string.IsNullOrEmpty(newCategoryPassword) || newCategoryPassword.Length < 6)
            {
                return R.Failure("新独立密码至少 6 位");
            }

            byte[]? oldKey = null;
            byte[]? newKey = null;
            try
            {
                oldKey = CryptoTool.DeriveContentKey(oldCategoryPassword, category.KdfSalt!, category.KdfIterations);
                string newSalt = CryptoTool.GenerateSaltBase64();
                newKey = CryptoTool.DeriveContentKey(newCategoryPassword, newSalt, CryptoTool.KDF_ITERATIONS_DEFAULT);
                List<Note> notes = NoteDao.GetNoteListByCategory(userId, categoryId);
                progress?.Report(10);

                BeginTransaction();
                try
                {
                    category.PasswordHash = SecurityTool.PBKDF2Hash(newCategoryPassword);
                    category.KdfSalt = newSalt;
                    category.KdfIterations = CryptoTool.KDF_ITERATIONS_DEFAULT;
                    category.UpdateTime = UtcNow;
                    CategoryDao.UpdateById(category);

                    if (notes.Count > 0)
                    {
                        R reencryptResult = NoteService.ReencryptList(notes, oldKey, newKey, new RangeProgress(progress, 15, 95));
                        if (!reencryptResult.IsSuccess)
                        {
                            RollbackTransaction();
                            return R.Failure("重加密失败，已回滚：" + reencryptResult.Message);
                        }
                    }
                    CommitTransaction();
                }
                catch (Exception ex)
                {
                    RollbackTransaction();
                    RampageLogger.Error(ex);
                    return R.Failure("重置独立密码失败，已回滚：" + ex.Message);
                }

                progress?.Report(100);
                return R.Success(newKey);
            }
            finally
            {
                CryptoTool.ClearKey(oldKey);
            }
        }

        /// <inheritdoc />
        public R ChangeCategoryToFollowUser(int userId, int categoryId, string categoryPassword, byte[] userContentKey, IProgress<int>? progress)
        {
            Category? category = CategoryDao.SelectById(categoryId);
            if (category == null || category.UserId != userId)
            {
                return R.Failure("分类不存在");
            }
            if (category.EncryptMode != (int)CategoryEncryptMode.IndependentPassword)
            {
                return R.Failure("该分类不是独立密码模式");
            }
            if (string.IsNullOrEmpty(category.PasswordHash) || !SecurityTool.PBKDF2Verify(categoryPassword, category.PasswordHash))
            {
                return R.Failure("独立密码不正确");
            }

            byte[]? categoryKey = null;
            try
            {
                categoryKey = CryptoTool.DeriveContentKey(categoryPassword, category.KdfSalt!, category.KdfIterations);
                List<Note> notes = NoteDao.GetNoteListByCategory(userId, categoryId);
                progress?.Report(10);

                BeginTransaction();
                try
                {
                    category.EncryptMode = (int)CategoryEncryptMode.FollowUser;
                    category.PasswordHash = null;
                    category.KdfSalt = null;
                    category.KdfIterations = CryptoTool.KDF_ITERATIONS_DEFAULT;
                    category.UpdateTime = UtcNow;
                    CategoryDao.UpdateById(category);

                    if (notes.Count > 0)
                    {
                        R reencryptResult = NoteService.ReencryptList(notes, categoryKey, userContentKey, new RangeProgress(progress, 15, 95));
                        if (!reencryptResult.IsSuccess)
                        {
                            RollbackTransaction();
                            return R.Failure("重加密失败，已回滚：" + reencryptResult.Message);
                        }
                    }
                    CommitTransaction();
                }
                catch (Exception ex)
                {
                    RollbackTransaction();
                    RampageLogger.Error(ex);
                    return R.Failure("切换加密模式失败，已回滚：" + ex.Message);
                }

                progress?.Report(100);
                return R.Success();
            }
            finally
            {
                CryptoTool.ClearKey(categoryKey);
            }
        }
    }
}
