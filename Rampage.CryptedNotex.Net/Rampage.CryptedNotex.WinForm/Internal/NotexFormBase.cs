using Microsoft.Extensions.DependencyInjection;
using Rampage.DependencyInjection;
using System;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 窗体基类：持有应用上下文,提供作用域内服务解析与统一主题应用
    /// </summary>
    public abstract class NotexFormBase : Form
    {
        /// <summary>
        /// 应用上下文
        /// </summary>
        public IRampageApplication App
        {
            get;
            private set;
        }

        /// <summary>
        /// 构造窗体基类
        /// </summary>
        /// <param name="app">应用上下文</param>
        protected NotexFormBase(IRampageApplication app)
        {
            App = app;
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
        }

        /// <summary>
        /// 从当前环境作用域解析服务(NotexGuiContext 启动时已在 UI 线程长绑全局作用域)
        /// </summary>
        /// <typeparam name="T">服务接口类型</typeparam>
        /// <returns>服务实例</returns>
        protected T GetService<T>() where T : class
        {
            return RampageScope.CurrentProvider!.GetRequiredService<T>();
        }

        /// <summary>
        /// 加载时应用当前主题
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Themes.ThemeManager.Apply(this);
        }
    }
}
