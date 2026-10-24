using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using Rampage.Model;
using System;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 笔记服务：笔记读写(加解密)、搜索、置顶、回收站与全量重加密原语
    /// </summary>
    /// <remarks>
    /// 内容密钥由调用方显式传入(来自界面层会话)，本服务对密钥来源零感知；
    /// 事务纪律：ReencryptList 不自开事务，由调用方(UserService/CategoryService)包裹。
    /// </remarks>
    public interface INoteService : IBaseService<Note, INoteDao>
    {
        /// <summary>
        /// 获取分类内正常笔记列表(不含正文密文；置顶优先,修改时间倒序)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="categoryId">分类 Id</param>
        /// <returns>笔记列表(不含正文)</returns>
        List<Note> GetNoteList(int userId, int categoryId);

        /// <summary>
        /// 按标题关键字搜索(不含正文密文,修改时间倒序)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="keyword">标题关键字</param>
        /// <returns>笔记列表(不含正文)</returns>
        List<Note> SearchByTitleList(int userId, string keyword);

        /// <summary>
        /// 获取回收站列表(不含正文密文,删除时间倒序)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <returns>回收站笔记列表(不含正文)</returns>
        List<Note> GetTrashList(int userId);

        /// <summary>
        /// 按 Id 取笔记(不含解密,登录后恢复上次打开笔记用)
        /// </summary>
        /// <param name="noteId">笔记 Id</param>
        /// <returns>笔记；不存在返回 null</returns>
        Note? GetNoteById(int noteId);

        /// <summary>
        /// 读取笔记详情并解密正文
        /// </summary>
        /// <param name="noteId">笔记 Id</param>
        /// <param name="contentKey">所属分类的内容密钥</param>
        /// <returns>解密后的详情；笔记不存在返回 null,解密失败(密钥错误/数据损坏)返回 Data 为空的失败 R</returns>
        R<NoteDetailDto> GetNoteDetail(int noteId, byte[] contentKey);

        /// <summary>
        /// 保存笔记(新增或更新)：明文加密为密文 blob、统计字数后写库；成功时实体 Id 已回填
        /// </summary>
        /// <param name="note">笔记(含标题/分类/类型；Id>0 为更新)</param>
        /// <param name="plainContent">正文明文(富文本为 RTF 文档)</param>
        /// <param name="contentKey">所属分类的内容密钥</param>
        /// <returns>操作结果</returns>
        R SaveNote(Note note, string plainContent, byte[] contentKey);

        /// <summary>
        /// 移入回收站(软删除,附件随笔记保留)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="noteIds">笔记 Id 列表</param>
        /// <returns>操作结果</returns>
        R MoveToTrashList(IEnumerable<int> noteIds, int userId);

        /// <summary>
        /// 从回收站恢复(原分类必定存在：删除分类时笔记已强制迁入默认分类)
        /// </summary>
        /// <param name="noteIds">笔记 Id 列表</param>
        /// <param name="userId">用户 Id</param>
        /// <returns>操作结果</returns>
        R RestoreList(IEnumerable<int> noteIds, int userId);

        /// <summary>
        /// 彻底删除(事务内级联删除附件,不可恢复)
        /// </summary>
        /// <param name="noteIds">笔记 Id 列表</param>
        /// <param name="userId">用户 Id</param>
        /// <returns>操作结果</returns>
        R PurgeList(IEnumerable<int> noteIds, int userId);

        /// <summary>
        /// 清空回收站(彻底删除全部回收站笔记及其附件)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <returns>操作结果</returns>
        R EmptyTrash(int userId);

        /// <summary>
        /// 置顶/取消置顶
        /// </summary>
        /// <param name="noteId">笔记 Id</param>
        /// <param name="isPinned">是否置顶</param>
        /// <returns>操作结果</returns>
        R UpdatePinned(int noteId, bool isPinned);

        /// <summary>
        /// 全量重加密原语(不自开事务,由调用方包裹)：逐条旧钥解密、新钥加密笔记正文与全部附件后批量写回
        /// </summary>
        /// <param name="notes">待重加密笔记(含密文,调用方已按加密模式/分类取好)</param>
        /// <param name="oldKey">旧内容密钥</param>
        /// <param name="newKey">新内容密钥</param>
        /// <param name="progress">进度回调(0~100)</param>
        /// <returns>任一条解密失败即返回失败(调用方应回滚)</returns>
        R ReencryptList(List<Note> notes, byte[] oldKey, byte[] newKey, IProgress<int>? progress);
    }
}
