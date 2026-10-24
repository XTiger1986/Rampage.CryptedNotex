using System;

namespace Rampage.CryptedNotex.WinForm
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序主入口。
        /// </summary>
        [STAThread]
        static void Main(string[]? args = null)
        {
            // 相对路径(数据库/日志/主题目录)统一锚定到 exe 所在目录,避免快捷方式"起始位置"不同导致数据散落
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            ApplicationConfiguration.Initialize();

            NotexApplication app = RampageBooter.Run<NotexApplication>(args ?? new string[] { });
            Application.Run(new Internal.NotexGuiContext(app));
        }
    }
}
