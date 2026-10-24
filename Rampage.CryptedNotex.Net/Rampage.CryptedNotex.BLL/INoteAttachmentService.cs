using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using Rampage.Model;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 笔记附件服务：附件加密入库/解密导出/删除
    /// </summary>
    public interface INoteAttachmentService : IBaseService<NoteAttachment, INoteAttachmentDao>
    {
        /// <summary>
        /// 获取笔记附件列表(不含密文,供附件面板)
        /// </summary>
        /// <param name="noteId">笔记 Id</param>
        /// <returns>附件列表(不含内容)</returns>
        List<NoteAttachment> GetAttachmentList(int noteId);

        /// <summary>
        /// 添加附件：明文加密为密文 blob 后入库
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="noteId">笔记 Id</param>
        /// <param name="fileName">文件名(明文)</param>
        /// <param name="mimeType">MIME 类型</param>
        /// <param name="plainBytes">明文字节</param>
        /// <param name="contentKey">所属分类的内容密钥</param>
        /// <returns>成功 Data 为附件 Id(int)</returns>
        R AddAttachment(int userId, int noteId, string fileName, string mimeType, byte[] plainBytes, byte[] contentKey);

        /// <summary>
        /// 删除附件
        /// </summary>
        /// <param name="attachmentId">附件 Id</param>
        /// <returns>操作结果</returns>
        R RemoveAttachment(int attachmentId);

        /// <summary>
        /// 解密附件内容(预览/导出用；写盘由界面层负责)
        /// </summary>
        /// <param name="attachmentId">附件 Id</param>
        /// <param name="contentKey">所属分类的内容密钥</param>
        /// <returns>明文字节；附件不存在或解密失败返回 null</returns>
        byte[]? DecryptAttachment(int attachmentId, byte[] contentKey);
    }
}
