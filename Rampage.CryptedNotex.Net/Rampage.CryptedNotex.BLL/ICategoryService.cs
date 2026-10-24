using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using Rampage.Model;
using System;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 分类服务：分类增删改排序与独立密码三流程(设置/重置/切回跟随全局,均含全量重加密)
    /// </summary>
    public interface ICategoryService : IBaseService<Category, ICategoryDao>
    {
        /// <summary>
        /// 获取用户分类列表(按排序值升序,同序按 Id)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <returns>分类列表</returns>
        List<Category> GetCategoryList(int userId);

        /// <summary>
        /// 保存分类：Id>0 为重命名(校验同用户名称唯一)，否则新建(明文/跟随全局模式)
        /// </summary>
        /// <param name="category">分类(UserId/Name 必填；新建时忽略 Id)</param>
        /// <returns>成功 Data 为分类 Id(int)</returns>
        R SaveCategory(Category category);

        /// <summary>
        /// 删除分类：默认分类拒绝删除；分类下有笔记(含回收站)时须 moveNotesToDefault=true 将笔记迁入默认分类,否则拒绝
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="moveNotesToDefault">是否把笔记迁入默认分类</param>
        /// <returns>操作结果</returns>
        R RemoveCategory(int userId, int categoryId, bool moveNotesToDefault);

        /// <summary>
        /// 按给定顺序重排用户分类(上移/下移后的完整顺序)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="orderedCategoryIds">按新顺序排列的分类 Id 列表</param>
        /// <returns>操作结果</returns>
        R UpdateCategorySort(int userId, IEnumerable<int> orderedCategoryIds);

        /// <summary>
        /// 校验独立密码并派生该分类的内容密钥(解锁用)
        /// </summary>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="password">分类独立密码</param>
        /// <param name="categoryKey">成功时输出分类内容密钥(调用方负责用后清零)</param>
        /// <returns>密码正确返回 true</returns>
        bool TryUnlockCategory(int categoryId, string password, out byte[]? categoryKey);

        /// <summary>
        /// 为跟随全局的分类设置独立密码(encrypt_mode 0→1)：事务内生成分类独立 KDF 材料,并把该分类全部笔记与附件由用户内容密钥重加密为分类密钥
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="categoryPassword">新独立密码</param>
        /// <param name="userContentKey">当前会话的用户内容密钥(旧钥)</param>
        /// <param name="progress">进度回调(0~100)</param>
        /// <returns>成功 Data 为分类内容密钥(byte[])</returns>
        R SetCategoryIndependentPassword(int userId, int categoryId, string categoryPassword, byte[] userContentKey, IProgress<int>? progress);

        /// <summary>
        /// 重置独立密码(encrypt_mode 1 内换密)：旧分类钥重加密为新分类钥
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="oldCategoryPassword">旧独立密码</param>
        /// <param name="newCategoryPassword">新独立密码</param>
        /// <param name="progress">进度回调(0~100)</param>
        /// <returns>成功 Data 为新分类内容密钥(byte[])</returns>
        R ResetCategoryPassword(int userId, int categoryId, string oldCategoryPassword, string newCategoryPassword, IProgress<int>? progress);

        /// <summary>
        /// 切回跟随全局(encrypt_mode 1→0)：分类钥重加密为用户内容密钥并清空独立密码列
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="categoryPassword">当前独立密码</param>
        /// <param name="userContentKey">当前会话的用户内容密钥(新钥)</param>
        /// <param name="progress">进度回调(0~100)</param>
        /// <returns>操作结果</returns>
        R ChangeCategoryToFollowUser(int userId, int categoryId, string categoryPassword, byte[] userContentKey, IProgress<int>? progress);
    }
}
