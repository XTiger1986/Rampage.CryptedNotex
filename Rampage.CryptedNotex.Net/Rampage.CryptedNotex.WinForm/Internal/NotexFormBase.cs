using Microsoft.Extensions.DependencyInjection;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using Rampage.DependencyInjection;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 窗体基类：持有应用上下文,提供作用域内服务解析、统一主题、提示条与忙碌状态
    /// </summary>
    public abstract class NotexFormBase : Form
    {
        private ToastHost? _toasts;
        private BusyBanner? _busyBanner;
        private int _busyDepth;
        private bool _designScaleApplied;

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
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = ChromeAssets.AppIcon;
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
        /// 主窗体改用底部状态条，避免顶栏把三栏布局顶下去
        /// </summary>
        protected virtual bool UsesBusyBanner
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 提示条距底边的空隙
        /// </summary>
        protected virtual int ToastBottomMargin
        {
            get
            {
                return 18;
            }
        }

        /// <summary>
        /// 非模态提示。成功与说明会自动消失，失败停留更久，点击可关闭。
        /// </summary>
        /// <param name="message">正文</param>
        /// <param name="kind">种类</param>
        protected void ShowToast(string message, ToastKind kind = ToastKind.Info)
        {
            if (string.IsNullOrWhiteSpace(message) || IsDisposed)
            {
                return;
            }

            EnsureChrome();
            _toasts!.BottomInset = ToastBottomMargin;
            _toasts.ShowToast(message.Trim(), kind);
        }

        /// <summary>
        /// 进入忙碌。嵌套调用按层数收起，用 using 配对。
        /// </summary>
        /// <param name="message">忙碌说明</param>
        /// <returns>结束时收起忙碌状态</returns>
        protected IDisposable BeginBusy(string message)
        {
            EnsureChrome();
            _busyDepth++;
            if (UsesBusyBanner)
            {
                _busyBanner!.ShowBusy(message);
            }
            UseWaitCursor = true;
            return new BusyToken(this);
        }

        /// <summary>
        /// 显示忙碌并在后台执行。工作函数不要碰控件。
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="message">忙碌说明</param>
        /// <param name="work">后台工作</param>
        /// <returns>工作结果</returns>
        protected async Task<T> RunBusyAsync<T>(string message, Func<T> work)
        {
            using (BeginBusy(message))
            {
                return await UiWork.RunAsync(work);
            }
        }

        /// <summary>
        /// 加载时应用当前主题
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            // Programmatic forms build their control tree after the base constructor.
            // Establish the design baseline here so the initial font assignment cannot
            // consume the scale before those controls exist.
            if (!_designScaleApplied)
            {
                _designScaleApplied = true;
                AutoScaleDimensions = new SizeF(96F, 96F);
                PerformAutoScale();
            }
            EnsureChrome();
            base.OnLoad(e);
            Themes.ThemeManager.Apply(this);
        }

        /// <summary>
        /// 尺寸变化时重新摆放提示条
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _toasts?.Relayout();
        }

        /// <summary>
        /// 懒创建提示宿主与忙碌条(忙碌条后加入，显示时才占顶部)
        /// </summary>
        private void EnsureChrome()
        {
            if (_toasts != null)
            {
                return;
            }

            _toasts = new ToastHost(this)
            {
                BottomInset = ToastBottomMargin
            };
            _busyBanner = new BusyBanner
            {
                Dock = DockStyle.Top,
                Visible = false
            };
            Controls.Add(_busyBanner);
        }

        /// <summary>
        /// 结束一层忙碌
        /// </summary>
        private void EndBusy()
        {
            _busyDepth = Math.Max(0, _busyDepth - 1);
            if (_busyDepth == 0)
            {
                _busyBanner?.HideBusy();
                UseWaitCursor = false;
            }
        }

        /// <summary>
        /// BeginBusy 的配对释放
        /// </summary>
        private sealed class BusyToken : IDisposable
        {
            private NotexFormBase? _form;

            /// <summary>
            /// 绑定窗体
            /// </summary>
            public BusyToken(NotexFormBase form)
            {
                _form = form;
            }

            /// <summary>
            /// 收起一层忙碌
            /// </summary>
            public void Dispose()
            {
                _form?.EndBusy();
                _form = null;
            }
        }
    }
}
