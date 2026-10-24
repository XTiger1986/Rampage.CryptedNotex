using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.DAL
{
    /// <summary>
    /// 笔记数据访问实现；原生 SQL 只出现在本层。
    /// </summary>
    public class NoteDao : SuperDao<Note>, INoteDao
    {
        /// <summary>笔记列表不含正文的固定列清单(含列别名)。</summary>
        private const string SELECT_COLUMNS_WITHOUT_CONTENT = @"id AS Id, user_id AS UserId, category_id AS CategoryId, title AS Title,
                 content_type AS ContentType, status AS Status, is_pinned AS IsPinned, word_count AS WordCount,
                 delete_time AS DeleteTime, create_time AS CreateTime, update_time AS UpdateTime";

        /// <inheritdoc />
        public List<Note> GetNoteListWithoutContent(int userId, int categoryId, int status)
        {
            return DatabaseClient.SelectList<Note>(
                @"SELECT " + SELECT_COLUMNS_WITHOUT_CONTENT + @"
                  FROM note
                  WHERE user_id = @UserId AND category_id = @CategoryId AND status = @Status
                  ORDER BY is_pinned DESC, update_time DESC",
                new { UserId = userId, CategoryId = categoryId, Status = status });
        }

        /// <inheritdoc />
        public List<Note> SearchByTitleWithoutContent(int userId, string keyword)
        {
            // SQLite 的 LIKE 仅对 ASCII 大小写不敏感：关键字与列都过 LOWER 保证中文不受影响、英文忽略大小写
            string escaped = keyword.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").ToLowerInvariant();
            return DatabaseClient.SelectList<Note>(
                @"SELECT " + SELECT_COLUMNS_WITHOUT_CONTENT + @"
                  FROM note
                  WHERE user_id = @UserId AND status = 0 AND LOWER(title) LIKE '%' || @Keyword || '%' ESCAPE '\'
                  ORDER BY update_time DESC",
                new { UserId = userId, Keyword = escaped });
        }

        /// <inheritdoc />
        public List<Note> GetTrashListWithoutContent(int userId)
        {
            return DatabaseClient.SelectList<Note>(
                @"SELECT " + SELECT_COLUMNS_WITHOUT_CONTENT + @"
                  FROM note
                  WHERE user_id = @UserId AND status = 1
                  ORDER BY delete_time DESC",
                new { UserId = userId });
        }

        /// <inheritdoc />
        public List<Note> GetNoteListForUserEncryptMode(int userId, int encryptMode)
        {
            return DatabaseClient.SelectList<Note>(
                @"SELECT n.id AS Id, n.user_id AS UserId, n.category_id AS CategoryId, n.title AS Title,
                         n.content_type AS ContentType, n.status AS Status, n.is_pinned AS IsPinned, n.word_count AS WordCount,
                         n.delete_time AS DeleteTime, n.create_time AS CreateTime, n.update_time AS UpdateTime, n.content AS Content
                  FROM note n
                  INNER JOIN category c ON c.id = n.category_id
                  WHERE n.user_id = @UserId AND c.encrypt_mode = @EncryptMode",
                new { UserId = userId, EncryptMode = encryptMode });
        }

        /// <inheritdoc />
        public List<Note> GetNoteListByCategory(int userId, int categoryId)
        {
            return DatabaseClient.SelectList<Note>(
                @"SELECT n.id AS Id, n.user_id AS UserId, n.category_id AS CategoryId, n.title AS Title,
                         n.content_type AS ContentType, n.status AS Status, n.is_pinned AS IsPinned, n.word_count AS WordCount,
                         n.delete_time AS DeleteTime, n.create_time AS CreateTime, n.update_time AS UpdateTime, n.content AS Content
                  FROM note n
                  WHERE n.user_id = @UserId AND n.category_id = @CategoryId",
                new { UserId = userId, CategoryId = categoryId });
        }
    }
}
