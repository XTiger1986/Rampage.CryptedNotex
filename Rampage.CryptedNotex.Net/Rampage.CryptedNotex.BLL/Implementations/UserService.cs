using Rampage.AppLog;
using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.DependencyInjection;
using Rampage.Hierarchy;
using Rampage.Model;
using Rampage.Utilities;
using System;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 用户服务实现
    /// </summary>
    public class UserService : BaseService<User, IUserDao>, IUserService
    {
        /// <summary>
        /// 默认分类名(创建用户时自动生成,不可删除)
        /// </summary>
        public const string DEFAULT_CATEGORY_NAME = "默认";

        /// <summary>
        /// 用户数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected IUserDao UserDao = default!;

        /// <summary>
        /// 分类数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected ICategoryDao CategoryDao = default!;

        /// <summary>
        /// 用户偏好设置数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected IUserSettingDao UserSettingDao = default!;

        /// <summary>
        /// 笔记数据访问，由 [Auto] 注入(改密时取待重加密笔记)
        /// </summary>
        [Auto]
        protected INoteDao NoteDao = default!;

        /// <summary>
        /// 笔记服务，由 [Auto] 注入(改密全量重加密复用其 ReencryptList)
        /// </summary>
        [Auto]
        protected INoteService NoteService = default!;

        /// <inheritdoc />
        public int CountUsers()
        {
            return (int)UserDao.Count();
        }

        /// <inheritdoc />
        public List<UserBriefDto> GetUserBriefList()
        {
            List<User> users = UserDao.SelectList(m => true);
            // 内存排序兜底：DAO 谓词重载不携带排序
            users.Sort((a, b) => string.Compare(a.Username, b.Username, StringComparison.Ordinal));
            List<UserBriefDto> list = new List<UserBriefDto>();
            foreach (User user in users)
            {
                list.Add(ToBriefDto(user));
            }
            return list;
        }

        /// <inheritdoc />
        public R<UserBriefDto> CreateUser(string username, string displayName, string password, out byte[]? contentKey)
        {
            contentKey = null;
            string name = (username ?? string.Empty).Trim().ToLowerInvariant();
            string shownName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
            if (name.Length < 2)
            {
                return R<UserBriefDto>.Failure("登录名至少 2 个字符");
            }
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                return R<UserBriefDto>.Failure("密码至少 6 位");
            }
            if (UserDao.SelectOne(m => m.Username == name) != null)
            {
                return R<UserBriefDto>.Failure("登录名已存在");
            }

            User user = new User
            {
                Username = name,
                DisplayName = shownName,
                PasswordHash = SecurityTool.PBKDF2Hash(password),
                KdfAlgorithm = "PBKDF2-SHA256",
                KdfIterations = CryptoTool.KDF_ITERATIONS_DEFAULT,
                KdfSalt = CryptoTool.GenerateSaltBase64(),
                CreateTime = UtcNow
            };

            Category defaultCategory = new Category
            {
                UserId = 0,
                Name = DEFAULT_CATEGORY_NAME,
                IsDefault = 1,
                EncryptMode = 0,
                SortOrder = 0,
                CreateTime = UtcNow
            };

            BeginTransaction();
            try
            {
                UserDao.Insert(user);
                defaultCategory.UserId = user.Id;
                CategoryDao.Insert(defaultCategory);
                UserSettingDao.Insert(new UserSetting
                {
                    UserId = user.Id,
                    CreateTime = UtcNow
                });
                CommitTransaction();
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                RampageLogger.Error(ex);
                return R<UserBriefDto>.Failure("创建用户失败：" + ex.Message);
            }

            // 创建即登录：直接派生内容密钥交给调用方
            contentKey = CryptoTool.DeriveContentKey(password, user.KdfSalt, user.KdfIterations);
            return R<UserBriefDto>.Success(ToBriefDto(user));
        }

        /// <inheritdoc />
        public UserBriefDto? VerifyUser(string username, string password, out byte[]? contentKey)
        {
            contentKey = null;
            string name = (username ?? string.Empty).Trim().ToLowerInvariant();
            User? user = UserDao.SelectOne(m => m.Username == name);
            if (user == null || !SecurityTool.PBKDF2Verify(password, user.PasswordHash))
            {
                return null;
            }

            user.LastLoginTime = UtcNow;
            UserDao.UpdateById(user);
            contentKey = CryptoTool.DeriveContentKey(password, user.KdfSalt, user.KdfIterations);
            return ToBriefDto(user);
        }

        /// <inheritdoc />
        public R ChangePassword(int userId, string oldPassword, string newPassword, IProgress<int>? progress)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                return R.Failure("新密码至少 6 位");
            }

            User? user = UserDao.SelectById(userId);
            if (user == null)
            {
                return R.Failure("用户不存在");
            }
            if (!SecurityTool.PBKDF2Verify(oldPassword, user.PasswordHash))
            {
                return R.Failure("旧密码不正确");
            }

            byte[]? oldKey = null;
            byte[]? newKey = null;
            try
            {
                oldKey = CryptoTool.DeriveContentKey(oldPassword, user.KdfSalt, user.KdfIterations);
                string newSalt = CryptoTool.GenerateSaltBase64();
                // 迭代数在改密时升到当前默认值(KDF 参数升级策略)
                newKey = CryptoTool.DeriveContentKey(newPassword, newSalt, CryptoTool.KDF_ITERATIONS_DEFAULT);
                progress?.Report(5);

                BeginTransaction();
                try
                {
                    user.PasswordHash = SecurityTool.PBKDF2Hash(newPassword);
                    user.KdfAlgorithm = "PBKDF2-SHA256";
                    user.KdfIterations = CryptoTool.KDF_ITERATIONS_DEFAULT;
                    user.KdfSalt = newSalt;
                    user.UpdateTime = UtcNow;
                    UserDao.UpdateById(user);

                    List<Note> notes = NoteDao.GetNoteListForUserEncryptMode(userId, (int)CategoryEncryptMode.FollowUser);
                    if (notes.Count > 0)
                    {
                        R reencryptResult = NoteService.ReencryptList(notes, oldKey, newKey, new RangeProgress(progress, 10, 85));
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
                    return R.Failure("修改密码失败，已回滚：" + ex.Message);
                }

                progress?.Report(100);
                return R.Success(newKey, "密码修改完成");
            }
            finally
            {
                CryptoTool.ClearKey(oldKey);
            }
        }

        /// <inheritdoc />
        public R UpdateDisplayName(int userId, string displayName)
        {
            string name = (displayName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return R.Failure("显示名不能为空");
            }

            User? user = UserDao.SelectById(userId);
            if (user == null)
            {
                return R.Failure("用户不存在");
            }

            user.DisplayName = name;
            user.UpdateTime = UtcNow;
            return UserDao.UpdateById(user) ? R.Success() : R.Failure();
        }

        /// <summary>
        /// Po 转登录界面摘要(绝不携带哈希/盐)
        /// </summary>
        private static UserBriefDto ToBriefDto(User user)
        {
            return new UserBriefDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                LastLoginTime = user.LastLoginTime
            };
        }
    }
}
