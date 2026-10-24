namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 主窗体退出原因：决定关闭后回登录界面还是退出程序
    /// </summary>
    public enum MainExitReason
    {
        /// <summary>
        /// 正常关闭(退出程序)
        /// </summary>
        Close = 0,

        /// <summary>
        /// 用户登出(回登录界面)
        /// </summary>
        Logout = 1,

        /// <summary>
        /// 自动锁定(回登录界面)
        /// </summary>
        AutoLocked = 2
    }
}
