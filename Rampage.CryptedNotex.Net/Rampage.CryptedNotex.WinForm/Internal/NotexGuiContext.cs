using Microsoft.Extensions.DependencyInjection;
using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.DependencyInjection;
using System;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 应用 GUI 上下文：登录循环(登录/首运行建用户 → 主窗体 → 登出/自动锁定回登录,关闭则退出)
    /// </summary>
    internal sealed class NotexGuiContext : ApplicationContext
    {
        private readonly IRampageApplication _app;
        private readonly CryptoSession _session = new CryptoSession();

        /// <summary>
        /// 构造 GUI 上下文：UI 线程长绑全局作用域(借用不释放),初始化主题后进入登录入口
        /// </summary>
        /// <param name="app">应用上下文</param>
        public NotexGuiContext(IRampageApplication app)
        {
            _app = app;
            RampageScope.UseGlobal();
            Themes.ThemeManager.Initialize();
            ApplyInitialTheme();
            ShowEntry();
        }

        /// <summary>
        /// 登录入口：无用户进首运行创建向导,否则进登录窗体；取消则退出程序
        /// </summary>
        private void ShowEntry()
        {
            IUserService userService = RampageScope.CurrentProvider!.GetRequiredService<IUserService>();
            Form entry = userService.CountUsers() == 0
                ? new Forms.CreateUserForm(_app, _session)
                : new Forms.LoginForm(_app, _session);
            if (entry.ShowDialog() == DialogResult.OK)
            {
                OpenMain();
            }
            else
            {
                _session.Lock();
                ExitThread();
            }
        }

        /// <summary>
        /// 打开主窗体；关闭后按退出原因决定回登录界面还是退出程序
        /// </summary>
        private void OpenMain()
        {
            Notex main = new Notex(_app, _session);
            main.FormClosed += (sender, e) =>
            {
                _session.Lock();
                if (main.ExitReason == MainExitReason.Logout || main.ExitReason == MainExitReason.AutoLocked)
                {
                    ShowEntry();
                }
                else
                {
                    ExitThread();
                }
            };
            main.Show();
        }

        /// <summary>
        /// 登录前的初始主题：全局设置(setting 表)的默认主题
        /// </summary>
        private void ApplyInitialTheme()
        {
            try
            {
                ISettingService settingService = RampageScope.CurrentProvider!.GetRequiredService<ISettingService>();
                AppSettingDto appSetting = settingService.GetSetting<AppSettingDto>();
                Themes.ThemeManager.ApplyTheme(appSetting.DefaultThemeId);
            }
            catch
            {
                // 配置异常时回退内置浅色主题,不阻塞启动
                Themes.ThemeManager.ApplyTheme("light");
            }
        }
    }
}
