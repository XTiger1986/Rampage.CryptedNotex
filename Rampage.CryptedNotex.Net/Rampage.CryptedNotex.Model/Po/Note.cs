using Rampage.CryptedNotex.Model.Enums;
using Rampage.Data;
using System;

namespace Rampage.CryptedNotex.Model.Po
{
    /// <summary>
    /// 笔记
    /// </summary>
    [TableMapping("note")]
    public class Note
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
        /// 分类 Id
        /// </summary>
        [FieldMapping(ColumnName = "category_id")]
        public int CategoryId { get; set; }

        /// <summary>
        /// 标题(明文,供 LIKE 搜索;敏感信息请勿写入标题)；数据库默认值：''
        /// </summary>
        [FieldMapping(ColumnName = "title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 内容类型(0=纯文本;1=富文本RTF;2=Markdown)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "content_type")]
        public int ContentType { get; set; }

        /// <summary>
        /// 正文密文(版本1字节+nonce12字节+密文N字节+tag16字节;密钥为所属分类的内容密钥)
        /// </summary>
        [FieldMapping(ColumnName = "content")]
        public byte[]? Content { get; set; }

        /// <summary>
        /// 状态(0=正常;1=回收站)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "status")]
        public int Status { get; set; }

        /// <summary>
        /// 是否置顶(0=否;1=是)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "is_pinned")]
        public int IsPinned { get; set; }

        /// <summary>
        /// 字数(正文非空白字符数,保存时统计)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "word_count")]
        public int WordCount { get; set; }

        /// <summary>
        /// 移入回收站时间(UTC)
        /// </summary>
        [FieldMapping(ColumnName = "delete_time")]
        public DateTime? DeleteTime { get; set; }

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

        /// <summary>
        /// 内容类型枚举视图(映射列仍为 int 的 ContentType,此处仅换算)
        /// </summary>
        [FieldMapping(NotColumn = true)]
        public ContentType ContentTypeValue
        {
            get
            {
                return (ContentType)ContentType;
            }
        }

        /// <summary>
        /// 状态枚举视图(映射列仍为 int 的 Status,此处仅换算)
        /// </summary>
        [FieldMapping(NotColumn = true)]
        public NoteStatus StatusValue
        {
            get
            {
                return (NoteStatus)Status;
            }
        }
    }
}
