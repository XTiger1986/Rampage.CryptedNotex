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

        private ListBox _userList = null!;
        private TextBox _passwordBox = null!;
        private Button _loginButton = null!;
        private Button _exitButton = null!;
        private Label _hintLabel = null!;
        private Timer _countdownTimer = null!;

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
        /// 构建壁纸底上的金属登录卡片
        /// </summary>
        private void BuildUi()
        {
            Panel card = new Panel
            {
                Size = new Size(440, 560),
                Location = new Point(60, 40),
                Tag = "theme:cardEmblem"
            };
            Controls.Add(card);

            Label titleLabel = new Label
            {
                Text = NotexBrand.DisplayName,
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(28, 118)
            };
            card.Controls.Add(titleLabel);

            Label userLabel = new Label
            {
                Text = "选择用户",
                AutoSize = true,
                Location = new Point(28, 162)
            };
            card.Controls.Add(userLabel);

            _userList = new ListBox
            {
                Location = new Point(28, 186),
                Size = new Size(384, 160),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 32,
                Font = new Font("Microsoft YaHei UI", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            _userList.DrawItem += UserListDrawItem;
            _userList.SelectedIndexChanged += (sender, e) => _passwordBox.Focus();
            card.Controls.Add(_userList);

            Label passwordLabel = new Label
            {
                Text = "密码",
                AutoSize = true,
                Location = new Point(28, 358)
            };
            card.Controls.Add(passwordLabel);

            _passwordBox = new TextBox
            {
                Location = new Point(28, 380),
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
                Location = new Point(28, 424),
                Size = new Size(184, 36),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _loginButton.Click += (sender, e) => AttemptLogin();
            card.Controls.Add(_loginButton);

            _exitButton = new Button
            {
                Text = "退出",
                Location = new Point(228, 424),
                Size = new Size(184, 36),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _exitButton.Click += (sender, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            card.Controls.Add(_exitButton);

            _hintLabel = new Label
            {
                Text = "忘记密码 = 数据无法恢复(无后门)",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(384, 0),
                Location = new Point(28, 476)
            };
            card.Controls.Add(_hintLabel);

            _countdownTimer = new Timer
            {
                Interval = 1000
            };
            _countdownTimer.Tick += (sender, e) => RefreshBlockState();

            _userList.SelectedIndexChanged += (sender, e) => RefreshBlockState();
            _passwordBox.TextChanged += (sender, e) => _hintLabel.Text = "忘记密码 = 数据无法恢复(无后门)";
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
        /// 句柄创建后的缩放可能改掉构造时的客户区，这里再钉一次
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ClientSize = new Size(560, 640);
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
                AttemptLogin();
            }
        }

        /// <summary>
        /// 刷新防爆破锁定状态
        /// </summary>
        private void RefreshBlockState()
        {
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
        /// 尝试登录
        /// </summary>
        private void AttemptLogin()
        {
            UserBriefDto? selected = SelectedUser();
            if (selected == null)
            {
                return;
            }

            if (_tracker.IsBlocked("notex", selected.Username, null, out TimeSpan retryAfter))
            {
                _hintLabel.Text = "失败次数过多，请 " + Math.Ceiling(retryAfter.TotalSeconds) + " 秒后重试";
                return;
            }

            string password = _passwordBox.Text;
            UserBriefDto? user = GetService<IUserService>().VerifyUser(selected.Username, password, out byte[]? contentKey);
            if (user == null || contentKey == null)
            {
                _tracker.RecordFailure("notex", selected.Username, null);
                _hintLabel.Text = "密码错误(忘记密码 = 数据无法恢复)";
                _passwordBox.SelectAll();
                RefreshBlockState();
                return;
            }

            _tracker.Reset("notex", selected.Username);
            _session.Init(user, contentKey);
            ApplyUserTheme(user.Id);
            DialogResult = DialogResult.OK;
            Close();
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
