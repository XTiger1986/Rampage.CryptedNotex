namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 用户可见产品名。程序集、命名空间、密文 AAD 与数据库文件名仍用 CryptedNotex，避免已有密文无法解密。
    /// </summary>
    public static class NotexBrand
    {
        /// <summary>
        /// 界面与文档使用的中文产品名
        /// </summary>
        public const string DisplayName = "秘密档案";

        /// <summary>
        /// 关于页上的工程技术标识
        /// </summary>
        public const string TechnicalName = "Rampage.CryptedNotex";

        /// <summary>
        /// 新用户与尚未写入全局设置时的默认主题
        /// </summary>
        public const string DefaultThemeId = "xenomorph";
    }
}
