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
        private LinkLabel _addUserLink = null!;
        private Label _passwordLabel = null!;
        private TextBox _passwordBox = null!;
        private Button _loginButton = null!;
        private Button _exitButton = null!;
        private Label _hintLabel = null!;
        private Timer _countdownTimer = null!;
        private bool _loggingIn;
        private AuthArtworkPanel _artwork = null!;
        private Panel _shell = null!;
        private bool _layingOut;
        private int _userHoverIndex = -1;

        /// <summary>
        /// 构造登录窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        public LoginForm(IRampageApplication app, CryptoSession session) : base(app)
        {
            _session = session;
            Text = NotexBrand.DisplayName + " · 登录";
            ClientSize = new Size(940, 660);
            MinimumSize = new Size(560, 480);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BuildUi();
        }

        /// <summary>
        /// 构建主题插画与可滚动表单的双区登录页。
        /// </summary>
        private void BuildUi()
        {
            _shell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24) };
            Controls.Add(_shell);
            _artwork = new AuthArtworkPanel { Dock = DockStyle.Left, Width = 360 };
            _card = new Panel { Dock = DockStyle.Fill, Tag = "theme:card", AutoScroll = true };
            _shell.Controls.Add(_card);
            _shell.Controls.Add(_artwork);
            Panel card = _card;

            _titleLabel = new Label
            {
                Text = "欢迎回来",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true
            };
            card.Controls.Add(_titleLabel);

            _subtitleLabel = new Label
            {
                Text = "选择账户，解锁你的私人档案。",
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
                BorderStyle = BorderStyle.None,
                IntegralHeight = false
            };
            _userList.DrawItem += UserListDrawItem;
            _userList.DoubleClick += (sender, e) => _passwordBox.Focus();
            _userList.MouseMove += (sender, e) =>
            {
                int index = _userList.IndexFromPoint(e.Location);
                if (index != _userHoverIndex)
                {
                    _userHoverIndex = index;
                    _userList.Invalidate();
                }
            };
            _userList.MouseLeave += (sender, e) =>
            {
                if (_userHoverIndex != -1)
                {
                    _userHoverIndex = -1;
                    _userList.Invalidate();
                }
            };
            card.Controls.Add(_userList);

            _addUserLink = new LinkLabel
            {
                Text = "+ 添加账户",
                Tag = "theme:link",
                AutoSize = true,
                TabStop = true,
                AccessibleName = "添加一个新账户"
            };
            _addUserLink.LinkClicked += (sender, e) => OpenCreateUserForm();
            card.Controls.Add(_addUserLink);

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
                Text = "解锁档案",
                Tag = "theme:accent",
                Size = new Size(184, 38),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _loginButton.Click += async (sender, e) => await AttemptLoginAsync();
            card.Controls.Add(_loginButton);
            AcceptButton = _loginButton;

            _exitButton = new Button
            {
                Text = "退出程序",
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
                Text = "请牢记密码。忘记密码后，正文与附件将无法恢复。",
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
                    _hintLabel.Text = "请牢记密码。忘记密码后，正文与附件将无法恢复。";
                }
            };

            _card.SizeChanged += (sender, e) => LayoutCard();
            _hintLabel.TextChanged += (sender, e) => LayoutCard();
            LayoutCard();
        }

        /// <summary>
        /// 按 DPI 与实测字体高度布局，窄窗内仍可滚动访问全部控件。
        /// </summary>
        private void LayoutCard()
        {
            if (_layingOut || _card == null || _card.ClientSize.Width < 100)
            {
                return;
            }
            _layingOut = true;
            _card.SuspendLayout();
            try
            {
                // Discard the previous scroll extent before shrinking the content width.
                _card.AutoScrollMinSize = Size.Empty;
                _card.AutoScrollPosition = Point.Empty;
                int gap = Scale(12);
                int x = Scale(32);
                int width = Math.Max(Scale(220), _card.ClientSize.Width - x * 2 - SystemInformation.VerticalScrollBarWidth);
                int y = Scale(32);
                _titleLabel.Location = new Point(x, y);
                y += _titleLabel.Height + gap;
                _subtitleLabel.MaximumSize = new Size(width, 0);
                _subtitleLabel.Location = new Point(x, y);
                y += _subtitleLabel.Height + Scale(28);
                _userLabel.Location = new Point(x, y);
                y += _userLabel.Height + gap;
                _userList.SetBounds(x, y, width, Scale(144));
                _userList.ItemHeight = Scale(42);
                y += _userList.Height + gap;
                _addUserLink.Location = new Point(x, y);
                y += _addUserLink.Height + Scale(24);
                _passwordLabel.Location = new Point(x, y);
                y += _passwordLabel.Height + gap;
                _passwordBox.Location = new Point(x, y);
                _passwordBox.Width = width;
                y += _passwordBox.Height + Scale(24);
                _loginButton.SetBounds(x, y, width, Scale(42));
                y += _loginButton.Height + Scale(8);
                _exitButton.SetBounds(x, y, width, Scale(36));
                y += _exitButton.Height + Scale(20);
                _hintLabel.MaximumSize = new Size(width, 0);
                _hintLabel.Location = new Point(x, y);
                _card.AutoScrollMinSize = new Size(0, _hintLabel.Bottom + Scale(28));
            }
            finally
            {
                _card.ResumeLayout(true);
                _layingOut = false;
            }
        }

        private int Scale(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96D);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCard();
        }

        private void CenterCard()
        {
            if (_card == null || _artwork == null)
            {
                return;
            }
            _artwork.Visible = ClientSize.Width >= Scale(780);
            _artwork.Width = Math.Max(Scale(280), (int)(ClientSize.Width * 0.42));
            LayoutCard();
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
            bool hovered = !selected && e.Index == _userHoverIndex;
            Color back = selected ? ThemeManager.GetColor("selectionBack") : ThemeManager.GetColor("inputBack");
            Color fore = ThemeManager.GetColor("text");
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            if (hovered)
            {
                using (SolidBrush hoverBrush = new SolidBrush(ChromePainter.RowHoverColor()))
                {
                    e.Graphics.FillRectangle(hoverBrush, e.Bounds);
                }
            }
            UserBriefDto? user = e.Index < _users.Count ? _users[e.Index] : null;
            int left = e.Bounds.X + Scale(14);
            if (selected)
            {
                using (SolidBrush marker = new SolidBrush(ThemeManager.GetColor("accent")))
                {
                    e.Graphics.FillRectangle(marker, e.Bounds.X, e.Bounds.Y + Scale(8), Scale(3), e.Bounds.Height - Scale(16));
                }
            }
            using (Font nameFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold))
            using (Font metaFont = new Font("Microsoft YaHei UI", 8F))
            {
                string displayName = user?.DisplayName ?? _userList.Items[e.Index]?.ToString() ?? string.Empty;
                string detail = user != null && user.LastLoginTime.HasValue
                    ? "上次登录 " + user.LastLoginTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : "首次使用";
                TextRenderer.DrawText(e.Graphics, displayName, nameFont, new Rectangle(left, e.Bounds.Y + Scale(3), e.Bounds.Width - Scale(24), Scale(20)), fore, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, detail, metaFont, new Rectangle(left, e.Bounds.Y + Scale(23), e.Bounds.Width - Scale(24), Scale(17)), ThemeManager.GetColor("textSecondary"), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if ((e.State & DrawItemState.Focus) != 0)
            {
                e.DrawFocusRectangle();
            }
        }

        /// <summary>
        /// 按当前屏幕工作区限制尺寸，再完成表单排版。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            Size available = new Size(Math.Max(1, workArea.Width - Scale(32)), Math.Max(1, workArea.Height - Scale(48)));
            MinimumSize = new Size(Math.Min(MinimumSize.Width, available.Width), Math.Min(MinimumSize.Height, available.Height));
            Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
            CenterCard();
            _passwordBox.Focus();
        }

        /// <summary>
        /// 加载用户列表
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ReloadUsers();
        }

        /// <summary>
        /// 重载用户列表(登录窗打开与添加用户返回后调用)
        /// </summary>
        private void ReloadUsers()
        {
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
        /// 从登录窗添加新用户:复用创建向导,成功即以新用户身份直接进入主界面
        /// </summary>
        private void OpenCreateUserForm()
        {
            using (CreateUserForm form = new CreateUserForm(App, _session, false))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    ReloadUsers();
                }
            }
        }

        /// <summary>
        /// 用户列表显示文本
        /// </summary>
        private static string UserDisplayText(UserBriefDto user)
        {
            string loginTime = user.LastLoginTime.HasValue
                ? "上次登录 " + user.LastLoginTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "首次使用";
            return user.DisplayName + "  ·  " + loginTime;
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
            _loginButton.Text = enabled ? "解锁档案" : "正在验证…";
            _addUserLink.Enabled = enabled;
        }

        /// <summary>
        /// 释放防爆破倒计时资源。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _countdownTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

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
