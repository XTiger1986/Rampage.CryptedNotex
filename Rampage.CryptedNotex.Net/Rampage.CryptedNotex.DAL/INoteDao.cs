using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.DAL
{
    /// <summary>
    /// 笔记数据访问接口。
    /// </summary>
    public interface INoteDao : ISuperDao<Note>
    {
        /// <summary>
        /// 查询分类内笔记列表(不含 content 密文列,供列表展示；置顶优先,修改时间倒序)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="status">笔记状态(0=正常;1=回收站)</param>
        /// <returns>笔记列表(不含正文)</returns>
        List<Note> GetNoteListWithoutContent(int userId, int categoryId, int status);

        /// <summary>
        /// 按标题关键字搜索当前用户正常状态笔记(不含 content 密文列,修改时间倒序；大小写不敏感,LIKE 通配符已转义)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="keyword">标题关键字(原始输入,转义在本方法内完成)</param>
        /// <returns>笔记列表(不含正文)</returns>
        List<Note> SearchByTitleWithoutContent(int userId, string keyword);

        /// <summary>
        /// 查询回收站笔记列表(不含 content 密文列,删除时间倒序)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <returns>回收站笔记列表(不含正文)</returns>
        List<Note> GetTrashListWithoutContent(int userId);

        /// <summary>
        /// 查询指定用户在指定加密模式分类下的全部笔记(含 content 密文列,含回收站,供改密全量重加密)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="encryptMode">分类加密模式(0=跟随全局;1=独立密码)</param>
        /// <returns>笔记列表(含正文密文)</returns>
        List<Note> GetNoteListForUserEncryptMode(int userId, int encryptMode);

        /// <summary>
        /// 查询指定分类下全部笔记(含 content 密文列,含回收站,供分类改密/切模式重加密)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="categoryId">分类 Id</param>
        /// <returns>笔记列表(含正文密文)</returns>
        List<Note> GetNoteListByCategory(int userId, int categoryId);
    }
}
