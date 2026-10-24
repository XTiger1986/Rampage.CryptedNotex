using Rampage.AppLog;
using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.DependencyInjection;
using Rampage.Hierarchy;
using Rampage.Model;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 笔记附件服务实现
    /// </summary>
    public class NoteAttachmentService : BaseService<NoteAttachment, INoteAttachmentDao>, INoteAttachmentService
    {
        /// <summary>
        /// 笔记附件数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected INoteAttachmentDao NoteAttachmentDao = default!;

        /// <inheritdoc />
        public List<NoteAttachment> GetAttachmentList(int noteId)
        {
            return NoteAttachmentDao.GetAttachmentListWithoutContent(noteId);
        }

        /// <inheritdoc />
        public R AddAttachment(int userId, int noteId, string fileName, string mimeType, byte[] plainBytes, byte[] contentKey)
        {
            NoteAttachment attachment = new NoteAttachment
            {
                UserId = userId,
                NoteId = noteId,
                FileName = string.IsNullOrWhiteSpace(fileName) ? "未命名" : fileName.Trim(),
                MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType,
                FileSize = plainBytes.Length,
                Content = CryptoTool.Encrypt(plainBytes, contentKey, CryptoTool.AAD_ATTACHMENT),
                CreateTime = UtcNow
            };
            return NoteAttachmentDao.Insert(attachment) ? R.Success(attachment.Id) : R.Failure("附件保存失败");
        }

        /// <inheritdoc />
        public R RemoveAttachment(int attachmentId)
        {
            return NoteAttachmentDao.DeleteById(attachmentId) ? R.Success() : R.Failure("附件不存在或已删除");
        }

        /// <inheritdoc />
        public byte[]? DecryptAttachment(int attachmentId, byte[] contentKey)
        {
            NoteAttachment? attachment = NoteAttachmentDao.SelectById(attachmentId);
            if (attachment == null)
            {
                return null;
            }

            try
            {
                return CryptoTool.Decrypt(attachment.Content, contentKey, CryptoTool.AAD_ATTACHMENT);
            }
            catch (CryptographicException ex)
            {
                RampageLogger.Warn("附件解密失败 id=" + attachmentId + "：" + ex.Message);
                return null;
            }
        }
    }
}
