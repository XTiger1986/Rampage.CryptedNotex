using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.DAL
{
    /// <summary>
    /// 笔记附件数据访问实现；原生 SQL 只出现在本层。
    /// </summary>
    public class NoteAttachmentDao : SuperDao<NoteAttachment>, INoteAttachmentDao
    {
        /// <summary>附件列表不含内容的固定列清单(含列别名)。</summary>
        private const string SELECT_COLUMNS_WITHOUT_CONTENT = @"id AS Id, user_id AS UserId, note_id AS NoteId, file_name AS FileName,
                 mime_type AS MimeType, file_size AS FileSize, create_time AS CreateTime, update_time AS UpdateTime";

        /// <inheritdoc />
        public List<NoteAttachment> GetAttachmentListWithoutContent(int noteId)
        {
            return DatabaseClient.SelectList<NoteAttachment>(
                @"SELECT " + SELECT_COLUMNS_WITHOUT_CONTENT + @"
                  FROM note_attachment
                  WHERE note_id = @NoteId
                  ORDER BY id",
                new { NoteId = noteId });
        }

        /// <inheritdoc />
        public List<NoteAttachment> GetAttachmentListByNoteId(int noteId)
        {
            return DatabaseClient.SelectList<NoteAttachment>(
                @"SELECT id AS Id, user_id AS UserId, note_id AS NoteId, file_name AS FileName,
                         mime_type AS MimeType, file_size AS FileSize, content AS Content,
                         create_time AS CreateTime, update_time AS UpdateTime
                  FROM note_attachment
                  WHERE note_id = @NoteId
                  ORDER BY id",
                new { NoteId = noteId });
        }

        /// <inheritdoc />
        public bool RemoveAttachmentListByNoteId(int noteId)
        {
            return DatabaseClient.Execute(
                @"DELETE FROM note_attachment WHERE note_id = @NoteId",
                new { NoteId = noteId }) > 0;
        }
    }
}
