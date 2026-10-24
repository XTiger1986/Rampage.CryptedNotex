using System;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 自动锁定管理器：键盘/鼠标/滚轮活动重置空闲计时,超时(或系统锁定/会话切换)触发锁定回调
    /// </summary>
    /// <remarks>
    /// 计时器在 UI 线程轮询(15 秒),回调已在 UI 线程,可直接操作控件；
    /// 长任务(改密重加密)运行期间应 Suppress(true) 暂停判定。
    /// </remarks>
    internal sealed class AutoLockManager : IMessageFilter, IDisposable
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_XBUTTONDOWN = 0x020B;

        private readonly Timer _timer;
        private DateTime _lastActivityUtc = DateTime.UtcNow;
        private int _autoLockMinutes;
        private bool _suppressed;
        private bool _fired;

        /// <summary>
        /// 触发锁定(UI 线程回调：调用方应 flush 未保存内容→锁定会话→关闭主窗体)
        /// </summary>
        public event Action? AutoLockRequested;

        /// <summary>
        /// 构造并开始监听
        /// </summary>
        public AutoLockManager()
        {
            _timer = new Timer
            {
                Interval = 15000
            };
            _timer.Tick += OnTimerTick;
            Application.AddMessageFilter(this);
            Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
            _timer.Start();
        }

        /// <summary>
        /// 更新自动锁定分钟数(0=关闭)；设置变更即刻生效
        /// </summary>
        /// <param name="minutes">分钟数</param>
        public void UpdateSettings(int minutes)
        {
            _autoLockMinutes = minutes;
            _fired = false;
        }

        /// <summary>
        /// 暂停/恢复判定(长任务运行期间暂停)
        /// </summary>
        /// <param name="suppress">是否暂停</param>
        public void Suppress(bool suppress)
        {
            _suppressed = suppress;
            if (suppress)
            {
                _lastActivityUtc = DateTime.UtcNow;
                _fired = false;
            }
        }

        /// <summary>
        /// 任何键盘/鼠标消息都视为活动
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_KEYDOWN:
                case WM_KEYUP:
                case WM_SYSKEYDOWN:
                case WM_MOUSEMOVE:
                case WM_LBUTTONDOWN:
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                case WM_RBUTTONDOWN:
                case WM_RBUTTONUP:
                case WM_MBUTTONDOWN:
                case WM_MOUSEWHEEL:
                case WM_XBUTTONDOWN:
                    _lastActivityUtc = DateTime.UtcNow;
                    _fired = false;
                    break;
            }
            return false;
        }

        /// <summary>
        /// 轮询判定(定时器在 UI 线程)
        /// </summary>
        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_suppressed || _fired || _autoLockMinutes <= 0)
            {
                return;
            }

            TimeSpan idle = DateTime.UtcNow - _lastActivityUtc;
            if (idle.TotalMinutes >= _autoLockMinutes)
            {
                _fired = true;
                AutoLockRequested?.Invoke();
            }
        }

        /// <summary>
        /// 系统锁定/会话注销同样触发：置空闲为极值,下一轮询即锁定(在 UI 线程触发回调)
        /// </summary>
        private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock
                || e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLogoff)
            {
                _lastActivityUtc = DateTime.MinValue;
            }
        }

        /// <summary>
        /// 停止监听并释放资源
        /// </summary>
        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            Application.RemoveMessageFilter(this);
            Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
    }
}
