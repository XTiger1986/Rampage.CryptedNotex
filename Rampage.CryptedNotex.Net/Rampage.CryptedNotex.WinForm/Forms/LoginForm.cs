using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using Rampage.Security;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 登录窗体：选择用户→输密码→防爆破计数；登录成功初始化加密会话并切到用户个人主题
    /// </summary>
    public class LoginForm : NotexFormBase
    {
        private readonly CryptoSession _session;
        private readonly ILoginAttemptTracker _tracker = MemoryLoginAttemptTracker.Default;
        private List<UserBriefDto> _users = new List<UserBriefDto>();

        private Panel _card = null!;
        private Label _titleLabel = null!;
        private Label _subtitleLabel = null!;
        private Label _userLabel = null!;
        private ListBox _userList = null!;
        private Label _passwordLabel = null!;
        private TextBox _passwordBox = null!;
        private Button _loginButton = null!;
        private Button _exitButton = null!;
        private Label _hintLabel = null!;
        private Timer _countdownTimer = null!;
        private bool _loggingIn;
        private Size _clientSize;

        /// <summary>
        /// 构造登录窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        public LoginForm(IRampageApplication app, CryptoSession session) : base(app)
        {
            _session = session;
            Text = NotexBrand.DisplayName + " · 登录";
            Tag = "theme:wallpaper";
            ClientSize = new Size(560, 640);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BuildUi();
        }

        /// <summary>
        /// 构建壁纸底上的金属登录卡片（控件只创建，位置交给 <see cref="LayoutCard"/> 统一流动排版）
        /// </summary>
        private void BuildUi()
        {
            _card = new Panel
            {
                Size = new Size(440, 560),
                Tag = "theme:cardEmblem"
            };
            Controls.Add(_card);
            Panel card = _card;

            _titleLabel = new Label
            {
                Text = NotexBrand.DisplayName,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true
            };
            card.Controls.Add(_titleLabel);

            _subtitleLabel = new Label
            {
                Text = "本地加密笔记，只存在这台设备上",
                Tag = "theme:muted",
                AutoSize = true
            };
            card.Controls.Add(_subtitleLabel);

            _userLabel = new Label
            {
                Text = "选择用户",
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            card.Controls.Add(_userLabel);

            _userList = new ListBox
            {
                Size = new Size(384, 148),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 32,
                Font = new Font("Microsoft YaHei UI", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            _userList.DrawItem += UserListDrawItem;
            _userList.SelectedIndexChanged += (sender, e) => _passwordBox.Focus();
            card.Controls.Add(_userList);

            _passwordLabel = new Label
            {
                Text = "密码",
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            card.Controls.Add(_passwordLabel);

            _passwordBox = new TextBox
            {
                Size = new Size(384, 28),
                Font = new Font("Microsoft YaHei UI", 10.5F),
                UseSystemPasswordChar = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            _passwordBox.KeyDown += PasswordBoxKeyDown;
            card.Controls.Add(_passwordBox);

            _loginButton = new Button
            {
                Text = "登录",
                Tag = "theme:accent",
                Size = new Size(184, 38),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _loginButton.Click += async (sender, e) => await AttemptLoginAsync();
            card.Controls.Add(_loginButton);
            AcceptButton = _loginButton;

            _exitButton = new Button
            {
                Text = "退出",
                Size = new Size(184, 36),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _exitButton.Click += (sender, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            card.Controls.Add(_exitButton);
            CancelButton = _exitButton;

            _hintLabel = new Label
            {
                Text = "忘记密码 = 数据无法恢复(无后门)",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(384, 0)
            };
            card.Controls.Add(_hintLabel);

            _countdownTimer = new Timer
            {
                Interval = 1000
            };
            _countdownTimer.Tick += (sender, e) => RefreshBlockState();

            _userList.SelectedIndexChanged += (sender, e) => RefreshBlockState();
            _passwordBox.TextChanged += (sender, e) =>
            {
                if (!_loggingIn)
                {
                    _hintLabel.Text = "忘记密码 = 数据无法恢复(无后门)";
                }
            };

            LayoutCard();
        }

        /// <summary>
        /// 按实测控件高度自上而下流动排版登录卡片。
        /// 字体随系统 DPI 放大而坐标不缩放时（本机 150% 缩放），写死的紧凑间距会让标题压住副标题，
        /// 改为逐行累计实测高度即可在任意缩放下不叠字；100% 缩放时各位置与原设计值一致。
        /// Flow the login card top-down using measured control heights: with fonts scaled by DPI but
        /// coordinates not (this machine runs 150%), hardcoded tight gaps made the title overlap the
        /// subtitle; accumulating measured heights keeps the layout clean at any scale while staying
        /// pixel-identical to the original design at 100%.
        /// </summary>
        private void LayoutCard()
        {
            int x = 28;
            int y = 108;
            _titleLabel.Location = new Point(x, y);
            y += _titleLabel.Height + 2;
            _subtitleLabel.Location = new Point(30, y);
            y += _subtitleLabel.Height + 17;
            _userLabel.Location = new Point(x, y);
            y += _userLabel.Height + 7;
            _userList.Location = new Point(x, y);
            y += _userList.Height + 12;
            _passwordLabel.Location = new Point(x, y);
            y += _passwordLabel.Height + 7;
            _passwordBox.Location = new Point(x, y);
            y += Math.Max(_passwordBox.Height, _passwordBox.PreferredSize.Height) + 20;
            _loginButton.Location = new Point(x, y);
            _exitButton.Location = new Point(228, y);
            y += _loginButton.Height + 14;
            _hintLabel.Location = new Point(x, y);
            y += _hintLabel.Height + 33;

            _card.Size = new Size(440, Math.Max(560, y));
            _clientSize = new Size(560, Math.Max(640, _card.Height + 80));
            ClientSize = _clientSize;
        }

        /// <summary>
        /// 卡片在壁纸上居中
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCard();
        }

        /// <summary>
        /// 把登录卡片摆到客户区中央
        /// </summary>
        private void CenterCard()
        {
            if (_card == null)
            {
                return;
            }
            _card.Location = new Point(
                Math.Max(16, (ClientSize.Width - _card.Width) / 2),
                Math.Max(16, (ClientSize.Height - _card.Height) / 2));
        }

        /// <summary>
        /// 用户列表自绘，行高与主题色一致
        /// </summary>
        private void UserListDrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = selected ? ThemeManager.GetColor("selectionBack") : ThemeManager.GetColor("inputBack");
            Color fore = ThemeManager.GetColor("text");
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            string text = _userList.Items[e.Index]?.ToString() ?? string.Empty;
            Rectangle textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                e.Font ?? _userList.Font,
                textRect,
                fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>
        /// 句柄创建后的缩放可能改掉构造时的客户区；此时字体已按实际 DPI 定型，重排一次并钉回计算出的尺寸
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutCard();
            CenterCard();
        }

        /// <summary>
        /// 加载用户列表
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _users = GetService<IUserService>().GetUserBriefList();
            _userList.BeginUpdate();
            _userList.Items.Clear();
            foreach (UserBriefDto user in _users)
            {
                _userList.Items.Add(UserDisplayText(user));
            }
            _userList.EndUpdate();
            if (_users.Count > 0)
            {
                _userList.SelectedIndex = 0;
            }
            RefreshBlockState();
        }

        /// <summary>
        /// 用户列表显示文本
        /// </summary>
        private static string UserDisplayText(UserBriefDto user)
        {
            string loginTime = user.LastLoginTime.HasValue
                ? "上次登录 " + user.LastLoginTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "首次使用";
            return user.DisplayName + "  (" + loginTime + ")";
        }

        /// <summary>
        /// 回车登录
        /// </summary>
        private void PasswordBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = AttemptLoginAsync();
            }
        }

        /// <summary>
        /// 刷新防爆破锁定状态
        /// </summary>
        private void RefreshBlockState()
        {
            if (_loggingIn)
            {
                _loginButton.Enabled = false;
                return;
            }

            UserBriefDto? selected = SelectedUser();
            if (selected == null)
            {
                _loginButton.Enabled = false;
                return;
            }

            if (_tracker.IsBlocked("notex", selected.Username, null, out TimeSpan retryAfter))
            {
                _loginButton.Enabled = false;
                _hintLabel.Text = "失败次数过多，请 " + Math.Ceiling(retryAfter.TotalSeconds) + " 秒后重试";
                _countdownTimer.Start();
            }
            else
            {
                _loginButton.Enabled = true;
                _countdownTimer.Stop();
            }
        }

        /// <summary>
        /// 当前选中用户
        /// </summary>
        private UserBriefDto? SelectedUser()
        {
            int index = _userList.SelectedIndex;
            return index >= 0 && index < _users.Count ? _users[index] : null;
        }

        /// <summary>
        /// 尝试登录。口令派生在后台进行，界面保持可重绘。
        /// </summary>
        private async Task AttemptLoginAsync()
        {
            if (_loggingIn)
            {
                return;
            }

            UserBriefDto? selected = SelectedUser();
            if (selected == null)
            {
                ShowToast("请先选择用户", ToastKind.Info);
                return;
            }

            if (_tracker.IsBlocked("notex", selected.Username, null, out TimeSpan retryAfter))
            {
                string blocked = "失败次数过多，请 " + Math.Ceiling(retryAfter.TotalSeconds) + " 秒后重试";
                _hintLabel.Text = blocked;
                ShowToast(blocked, ToastKind.Error);
                return;
            }

            string password = _passwordBox.Text;
            string username = selected.Username;
            _loggingIn = true;
            SetLoginInputsEnabled(false);
            try
            {
                (UserBriefDto? user, byte[]? contentKey) = await RunBusyAsync("正在验证密码…", () =>
                {
                    UserBriefDto? verified = GetService<IUserService>().VerifyUser(username, password, out byte[]? key);
                    return (verified, key);
                });
                if (IsDisposed)
                {
                    return;
                }
                if (user == null || contentKey == null)
                {
                    _tracker.RecordFailure("notex", username, null);
                    _hintLabel.Text = "密码错误(忘记密码 = 数据无法恢复)";
                    ShowToast("密码错误。忘记密码就无法恢复数据。", ToastKind.Error);
                    _passwordBox.SelectAll();
                    _passwordBox.Focus();
                    RefreshBlockState();
                    return;
                }

                _tracker.Reset("notex", username);
                _session.Init(user, contentKey);
                ApplyUserTheme(user.Id);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ShowToast("登录失败：" + ex.Message, ToastKind.Error);
            }
            finally
            {
                _loggingIn = false;
                if (!IsDisposed)
                {
                    SetLoginInputsEnabled(true);
                    RefreshBlockState();
                }
            }
        }

        /// <summary>
        /// 验证期间锁住输入，避免重复提交
        /// </summary>
        private void SetLoginInputsEnabled(bool enabled)
        {
            _userList.Enabled = enabled;
            _passwordBox.Enabled = enabled;
            _exitButton.Enabled = enabled;
            _loginButton.Text = enabled ? "登录" : "正在验证…";
        }

        /// <summary>
        /// 登录成功后切换到用户个人主题(主窗体加载时即为其偏好样式)
        /// </summary>
        private void ApplyUserTheme(int userId)
        {
            try
            {
                Model.Po.UserSetting setting = GetService<IUserSettingService>().GetUserSetting(userId);
                ThemeManager.ApplyTheme(setting.ThemeId);
            }
            catch
            {
                // 偏好读取失败保持当前主题
            }
        }
    }
}
