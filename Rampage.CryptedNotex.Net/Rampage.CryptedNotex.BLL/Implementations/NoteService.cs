using Rampage.AppLog;
using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.Data.Query;
using Rampage.DependencyInjection;
using Rampage.Hierarchy;
using Rampage.Model;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 笔记服务实现
    /// </summary>
    public class NoteService : BaseService<Note, INoteDao>, INoteService
    {
        /// <summary>
        /// 笔记数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected INoteDao NoteDao = default!;

        /// <summary>
        /// 笔记附件数据访问，由 [Auto] 注入(重加密/彻底删除级联)
        /// </summary>
        [Auto]
        protected INoteAttachmentDao NoteAttachmentDao = default!;

        /// <inheritdoc />
        public List<Note> GetNoteList(int userId, int categoryId)
        {
            return NoteDao.GetNoteListWithoutContent(userId, categoryId, (int)NoteStatus.Normal);
        }

        /// <inheritdoc />
        public List<Note> SearchByTitleList(int userId, string keyword)
        {
            return NoteDao.SearchByTitleWithoutContent(userId, keyword ?? string.Empty);
        }

        /// <inheritdoc />
        public List<Note> GetTrashList(int userId)
        {
            return NoteDao.GetTrashListWithoutContent(userId);
        }

        /// <inheritdoc />
        public Note? GetNoteById(int noteId)
        {
            return NoteDao.SelectById(noteId);
        }

        /// <inheritdoc />
        public R<NoteDetailDto> GetNoteDetail(int noteId, byte[] contentKey)
        {
            Note? note = NoteDao.SelectById(noteId);
            if (note == null)
            {
                return R<NoteDetailDto>.Failure("笔记不存在");
            }

            string plainContent = string.Empty;
            if (note.Content != null && note.Content.Length > 0)
            {
                try
                {
                    plainContent = Encoding.UTF8.GetString(CryptoTool.Decrypt(note.Content, contentKey, CryptoTool.AAD_NOTE));
                }
                catch (CryptographicException ex)
                {
                    // tag 校验失败=密钥错误或数据被篡改，记 Warning 不抛出，由界面友好提示
                    RampageLogger.Warn("笔记解密失败 id=" + noteId + "：" + ex.Message);
                    return R<NoteDetailDto>.Failure("该内容无法解密（密码变更未完成或数据损坏）");
                }
            }

            NoteDetailDto dto = new NoteDetailDto
            {
                Id = note.Id,
                CategoryId = note.CategoryId,
                Title = note.Title,
                ContentType = note.ContentTypeValue,
                PlainContent = plainContent,
                IsPinned = note.IsPinned == 1,
                WordCount = note.WordCount,
                CreateTime = note.CreateTime,
                UpdateTime = note.UpdateTime
            };
            return R<NoteDetailDto>.Success(dto);
        }

        /// <inheritdoc />
        public R SaveNote(Note note, string plainContent, byte[] contentKey)
        {
            note.Title = (note.Title ?? string.Empty).Trim();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainContent ?? string.Empty);
            note.Content = CryptoTool.Encrypt(plainBytes, contentKey, CryptoTool.AAD_NOTE);
            note.WordCount = WordCountTool.CountNonWhitespace(plainContent);

            if (note.Id > 0)
            {
                note.UpdateTime = UtcNow;
                return NoteDao.UpdateById(note) ? R.Success() : R.Failure("保存失败");
            }

            note.Status = (int)NoteStatus.Normal;
            note.CreateTime = UtcNow;
            return NoteDao.Insert(note) ? R.Success() : R.Failure("保存失败");
        }

        /// <inheritdoc />
        public R MoveToTrashList(IEnumerable<int> noteIds, int userId)
        {
            foreach (int noteId in noteIds)
            {
                NoteDao.UpdateById(noteId, m => m.Status, (int)NoteStatus.Trashed, m => m.DeleteTime, UtcNow);
            }
            return R.Success();
        }

        /// <inheritdoc />
        public R RestoreList(IEnumerable<int> noteIds, int userId)
        {
            foreach (int noteId in noteIds)
            {
                NoteDao.UpdateById(noteId, new UpdateFields<Note>()
                    .Set(m => m.Status, (int)NoteStatus.Normal)
                    .Set(m => m.DeleteTime, (DateTime?)null));
            }
            return R.Success();
        }

        /// <inheritdoc />
        public R PurgeList(IEnumerable<int> noteIds, int userId)
        {
            BeginTransaction();
            try
            {
                foreach (int noteId in noteIds)
                {
                    NoteAttachmentDao.RemoveAttachmentListByNoteId(noteId);
                    NoteDao.DeleteById(noteId);
                }
                CommitTransaction();
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                RampageLogger.Error(ex);
                return R.Failure("彻底删除失败：" + ex.Message);
            }
            return R.Success();
        }

        /// <inheritdoc />
        public R EmptyTrash(int userId)
        {
            List<int> noteIds = new List<int>();
            foreach (Note note in NoteDao.GetTrashListWithoutContent(userId))
            {
                noteIds.Add(note.Id);
            }
            if (noteIds.Count == 0)
            {
                return R.Success();
            }
            return PurgeList(noteIds, userId);
        }

        /// <inheritdoc />
        public R UpdatePinned(int noteId, bool isPinned)
        {
            return NoteDao.UpdateById(noteId, m => m.IsPinned, isPinned ? 1 : 0) ? R.Success() : R.Failure();
        }

        /// <inheritdoc />
        public R ReencryptList(List<Note> notes, byte[] oldKey, byte[] newKey, IProgress<int>? progress)
        {
            List<NoteAttachment> changedAttachments = new List<NoteAttachment>();
            try
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    Note note = notes[i];
                    if (note.Content != null && note.Content.Length > 0)
                    {
                        byte[] plain = CryptoTool.Decrypt(note.Content, oldKey, CryptoTool.AAD_NOTE);
                        note.Content = CryptoTool.Encrypt(plain, newKey, CryptoTool.AAD_NOTE);
                    }

                    foreach (NoteAttachment attachment in NoteAttachmentDao.GetAttachmentListByNoteId(note.Id))
                    {
                        byte[] plain = CryptoTool.Decrypt(attachment.Content, oldKey, CryptoTool.AAD_ATTACHMENT);
                        attachment.Content = CryptoTool.Encrypt(plain, newKey, CryptoTool.AAD_ATTACHMENT);
                        changedAttachments.Add(attachment);
                    }

                    progress?.Report((i + 1) * 100 / notes.Count);
                }
            }
            catch (CryptographicException ex)
            {
                RampageLogger.Error(ex);
                return R.Failure("存在无法用旧密钥解密的数据（id=" + ex.Message + "）");
            }

            if (notes.Count > 0)
            {
                NoteDao.UpdateBatch(notes);
            }
            if (changedAttachments.Count > 0)
            {
                NoteAttachmentDao.UpdateBatch(changedAttachments);
            }
            return R.Success();
        }
    }
}
