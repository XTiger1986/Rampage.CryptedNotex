using Rampage.CryptedNotex.Model.Enums;
using System;

namespace Rampage.CryptedNotex.Model.Dto
{
    /// <summary>
    /// 笔记详情(解密后的正文随行传输,供编辑器加载；仅在内存中使用,不落盘)
    /// </summary>
    public class NoteDetailDto
    {
        /// <summary>
        /// 笔记 Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 分类 Id
        /// </summary>
        public int CategoryId { get; set; }

        /// <summary>
        /// 标题(明文)
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 内容类型
        /// </summary>
        public ContentType ContentType { get; set; }

        /// <summary>
        /// 正文明文(纯文本/Markdown 为文本,富文本为 RTF 文档)
        /// </summary>
        public string PlainContent { get; set; } = string.Empty;

        /// <summary>
        /// 是否置顶
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// 字数(正文非空白字符数)
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// 创建时间(UTC)
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 修改时间(UTC)
        /// </summary>
        public DateTime? UpdateTime { get; set; }
    }
}
