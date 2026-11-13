using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using Rampage.Model;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 首运行创建用户向导：登录名/显示名/密码；成功即登录进入主界面
    /// </summary>
    public class CreateUserForm : NotexFormBase
    {
        private readonly CryptoSession _session;
        private readonly string _welcomeText;

        private Panel _card = null!;
        private Label _titleLabel = null!;
        private Label _welcomeLabel = null!;
        private TextBox _usernameBox = null!;
        private TextBox _displayNameBox = null!;
        private TextBox _passwordBox = null!;
        private TextBox _confirmBox = null!;
        private Button _createButton = null!;
        private Label _hintLabel = null!;
        private AuthArtworkPanel _artwork = null!;
        private bool _layingOut;
        private readonly List<Label> _fieldLabels = new List<Label>();
        private TextBox[] _fieldBoxes = Array.Empty<TextBox>();

        private bool _creating;

        /// <summary>
        /// 构造创建用户窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        /// <param name="firstRun">true=首运行向导文案;false=登录窗添加用户文案</param>
        public CreateUserForm(IRampageApplication app, CryptoSession session, bool firstRun = true) : base(app)
        {
            _session = session;
            _welcomeText = firstRun
                ? "首次使用请创建账户。密码会加密全部笔记。\n忘记密码 = 数据无法恢复，没有后门。"
                : "为这台设备再添加一个账户。\n每个账户独立密码，笔记互相隔离。";
            Text = NotexBrand.DisplayName + " · 创建用户";
            ClientSize = new Size(940, 700);
            MinimumSize = new Size(560, 480);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BuildUi();
        }

        /// <summary>
        /// 构建主题插画与可滚动的创建账户表单。
        /// </summary>
        private void BuildUi()
        {
            Panel shell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24) };
            Controls.Add(shell);
            _artwork = new AuthArtworkPanel { Dock = DockStyle.Left, Width = 360 };
            _card = new Panel { Dock = DockStyle.Fill, Tag = "theme:card", AutoScroll = true };
            shell.Controls.Add(_card);
            shell.Controls.Add(_artwork);
            Panel card = _card;

            _titleLabel = new Label
            {
                Text = "开启你的档案",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true
            };
            card.Controls.Add(_titleLabel);

            _welcomeLabel = new Label
            {
                Text = _welcomeText,
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(404, 0)
            };
            card.Controls.Add(_welcomeLabel);

            _fieldLabels.Add(AddField(card, "登录名(英文或数字,至少2个字符)", ref _usernameBox, false));
            _fieldLabels.Add(AddField(card, "显示名(可留空)", ref _displayNameBox, false));
            _fieldLabels.Add(AddField(card, "密码(至少6位)", ref _passwordBox, true));
            _fieldLabels.Add(AddField(card, "确认密码", ref _confirmBox, true));
            _fieldBoxes = new TextBox[] { _usernameBox, _displayNameBox, _passwordBox, _confirmBox };

            _createButton = new Button
            {
                Text = "创建并进入",
                Tag = "theme:accent",
                Size = new Size(404, 38),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _createButton.Click += async (sender, e) => await AttemptCreateAsync();
            card.Controls.Add(_createButton);
            AcceptButton = _createButton;

            _hintLabel = new Label
            {
                Text = string.Empty,
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(404, 0)
            };
            card.Controls.Add(_hintLabel);

            _confirmBox.KeyDown += ConfirmBoxKeyDown;

            _card.SizeChanged += (sender, e) => LayoutCard();
            _hintLabel.TextChanged += (sender, e) => LayoutCard();
            LayoutCard();
        }

        /// <summary>
        /// 按 DPI 与实测字体高度布局，避免高缩放下重叠。
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
                int x = Scale(32);
                int width = Math.Max(Scale(220), _card.ClientSize.Width - 2 * x - SystemInformation.VerticalScrollBarWidth);
                int y = Scale(32);
                _titleLabel.Location = new Point(x, y);
                y += _titleLabel.Height + Scale(12);
                _welcomeLabel.MaximumSize = new Size(width, 0);
                _welcomeLabel.Location = new Point(x, y);
                y += _welcomeLabel.Height + Scale(28);
                for (int i = 0; i < _fieldBoxes.Length; i++)
                {
                    Label label = _fieldLabels[i];
                    TextBox box = _fieldBoxes[i];
                    label.Location = new Point(x, y);
                    y += label.Height + Scale(8);
                    box.Location = new Point(x, y);
                    box.Width = width;
                    y += Math.Max(box.Height, box.PreferredSize.Height) + Scale(20);
                }
                y += Scale(4);
                _createButton.SetBounds(x, y, width, Scale(42));
                _hintLabel.MaximumSize = new Size(width, 0);
                _hintLabel.Location = new Point(x, _createButton.Bottom + Scale(14));
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

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            Size available = new Size(Math.Max(1, workArea.Width - Scale(32)), Math.Max(1, workArea.Height - Scale(48)));
            MinimumSize = new Size(Math.Min(MinimumSize.Width, available.Width), Math.Min(MinimumSize.Height, available.Height));
            Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
            CenterCard();
            _usernameBox.Focus();
        }

        /// <summary>
        /// 创建一个标签+输入框行（只创建控件，位置由 <see cref="LayoutCard"/> 统一流动排版）
        /// </summary>
        private Label AddField(Panel parent, string label, ref TextBox box, bool password)
        {
            Label fieldLabel = new Label
            {
                Text = label,
                AutoSize = true
            };
            parent.Controls.Add(fieldLabel);

            box = new TextBox
            {
                Size = new Size(404, 26),
                Font = new Font("Microsoft YaHei UI", 10F),
                UseSystemPasswordChar = password,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(box);
            return fieldLabel;
        }

        /// <summary>
        /// 确认密码框内回车直接创建 / Enter in the confirm box triggers creation directly
        /// </summary>
        private void ConfirmBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = AttemptCreateAsync();
            }
        }

        /// <summary>
        /// 尝试创建用户。密钥派生在后台进行。
        /// </summary>
        private async Task AttemptCreateAsync()
        {
            if (_creating)
            {
                return;
            }

            string username = _usernameBox.Text;
            string displayName = _displayNameBox.Text;
            string password = _passwordBox.Text;

            if (password != _confirmBox.Text)
            {
                _hintLabel.Text = "两次输入的密码不一致";
                ShowToast("两次输入的密码不一致", ToastKind.Error);
                return;
            }

            _creating = true;
            _createButton.Enabled = false;
            foreach (TextBox box in _fieldBoxes)
            {
                box.Enabled = false;
            }
            _createButton.Text = "正在创建…";
            try
            {
                (R<UserBriefDto> result, byte[]? contentKey) = await RunBusyAsync("正在创建账户并派生密钥…", () =>
                {
                    R<UserBriefDto> created = GetService<IUserService>().CreateUser(username, displayName, password, out byte[]? key);
                    return (created, key);
                });
                if (IsDisposed)
                {
                    return;
                }
                if (!result.IsSuccess || result.Data == null || contentKey == null)
                {
                    string message = result.Message ?? "创建失败";
                    _hintLabel.Text = message;
                    ShowToast(message, ToastKind.Error);
                    return;
                }

                _session.Init(result.Data, contentKey);
                try
                {
                    Model.Po.UserSetting setting = GetService<IUserSettingService>().GetUserSetting(result.Data.Id);
                    ThemeManager.ApplyTheme(setting.ThemeId);
                }
                catch
                {
                    // 偏好读取失败保持当前主题
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _hintLabel.Text = "创建失败：" + ex.Message;
                ShowToast("创建失败：" + ex.Message, ToastKind.Error);
            }
            finally
            {
                _creating = false;
                if (!IsDisposed)
                {
                    _createButton.Enabled = true;
                    foreach (TextBox box in _fieldBoxes)
                    {
                        box.Enabled = true;
                    }
                    _createButton.Text = "创建并进入";
                }
            }
        }
    }
}
