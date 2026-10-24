using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.DAL
{
    /// <summary>
    /// 笔记附件数据访问接口。
    /// </summary>
    public interface INoteAttachmentDao : ISuperDao<NoteAttachment>
    {
        /// <summary>
        /// 查询笔记的附件列表(不含 content 密文列,供附件面板展示)
        /// </summary>
        /// <param name="noteId">笔记 Id</param>
        /// <returns>附件列表(不含内容)</returns>
        List<NoteAttachment> GetAttachmentListWithoutContent(int noteId);

        /// <summary>
        /// 查询笔记的附件列表(含 content 密文列,供解密预览/导出/重加密)
        /// </summary>
        /// <param name="noteId">笔记 Id</param>
        /// <returns>附件列表(含内容密文)</returns>
        List<NoteAttachment> GetAttachmentListByNoteId(int noteId);

        /// <summary>
        /// 删除笔记的全部附件(笔记彻底删除时级联调用)
        /// </summary>
        /// <param name="noteId">笔记 Id</param>
        /// <returns>受影响行数是否大于 0</returns>
        bool RemoveAttachmentListByNoteId(int noteId);
    }
}
