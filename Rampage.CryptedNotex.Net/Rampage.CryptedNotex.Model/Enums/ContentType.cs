namespace Rampage.CryptedNotex.Model.Enums
{
    /// <summary>
    /// 笔记内容类型；数据库 note.content_type 列取值：0=纯文本;1=富文本RTF;2=Markdown
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// 纯文本(0)
        /// </summary>
        PlainText = 0,

        /// <summary>
        /// 富文本RTF(1)
        /// </summary>
        RichText = 1,

        /// <summary>
        /// Markdown(2)
        /// </summary>
        Markdown = 2
    }
}
