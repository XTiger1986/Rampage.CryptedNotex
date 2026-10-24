using Rampage.Data;
using System;

namespace Rampage.CryptedNotex.Model.Po
{
    /// <summary>
    /// 笔记附件(内容加密存储)
    /// </summary>
    [TableMapping("note_attachment")]
    public class NoteAttachment
    {
        /// <summary>
        /// 主键
        /// </summary>
        [FieldMapping(PrimaryKey = true, ColumnName = "id", AutoIncrement = true)]
        public int Id { get; set; }

        /// <summary>
        /// 所属用户 Id
        /// </summary>
        [FieldMapping(ColumnName = "user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// 所属笔记 Id(随笔记彻底删除而级联删除)
        /// </summary>
        [FieldMapping(ColumnName = "note_id")]
        public int NoteId { get; set; }

        /// <summary>
        /// 文件名(明文,原上传文件名)
        /// </summary>
        [FieldMapping(ColumnName = "file_name")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// MIME 类型(明文,用于图标与预览判断)；数据库默认值：application/octet-stream
        /// </summary>
        [FieldMapping(ColumnName = "mime_type")]
        public string MimeType { get; set; } = "application/octet-stream";

        /// <summary>
        /// 原始明文字节数；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "file_size")]
        public int FileSize { get; set; }

        /// <summary>
        /// 附件密文(版本1字节+nonce12字节+密文N字节+tag16字节;密钥为所属笔记分类的内容密钥)
        /// </summary>
        [FieldMapping(ColumnName = "content")]
        public byte[] Content { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 创建时间(UTC)；数据库默认值：CURRENT_TIMESTAMP
        /// </summary>
        /// <remarks>新增记录时由调用方设置 UTC 创建时间；数据库默认值仅在省略该列时生效。</remarks>
        [FieldMapping(ColumnName = "create_time")]
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 修改时间(UTC)
        /// </summary>
        [FieldMapping(ColumnName = "update_time")]
        public DateTime? UpdateTime { get; set; }
    }
}
