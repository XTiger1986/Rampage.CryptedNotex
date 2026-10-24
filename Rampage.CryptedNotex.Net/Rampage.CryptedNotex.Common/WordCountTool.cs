namespace Rampage.CryptedNotex.Common
{
    /// <summary>
    /// 字数统计工具
    /// </summary>
    public static class WordCountTool
    {
        /// <summary>
        /// 统计非空白字符数(三种笔记类型统一口径；富文本按其 PlainText 文本统计而非 RTF 源码)
        /// </summary>
        /// <param name="text">正文本</param>
        /// <returns>非空白字符数；空文本返回 0</returns>
        public static int CountNonWhitespace(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 0;
            foreach (char c in text)
            {
                if (!char.IsWhiteSpace(c))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
