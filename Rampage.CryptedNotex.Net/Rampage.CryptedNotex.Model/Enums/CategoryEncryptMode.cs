namespace Rampage.CryptedNotex.Model.Enums
{
    /// <summary>
    /// 分类加密模式；数据库 category.encrypt_mode 列取值：0=跟随全局;1=独立密码
    /// </summary>
    public enum CategoryEncryptMode
    {
        /// <summary>
        /// 跟随全局(0)：内容密钥由用户登录密码派生
        /// </summary>
        FollowUser = 0,

        /// <summary>
        /// 独立密码(1)：内容密钥由分类自己的密码派生，与用户密码无关
        /// </summary>
        IndependentPassword = 1
    }
}
