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

        private Panel _card = null!;
        private TextBox _usernameBox = null!;
        private TextBox _displayNameBox = null!;
        private TextBox _passwordBox = null!;
        private TextBox _confirmBox = null!;
        private Button _createButton = null!;
        private Label _hintLabel = null!;

        private bool _creating;

        /// <summary>
        /// 构造创建用户窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        public CreateUserForm(IRampageApplication app, CryptoSession session) : base(app)
        {
            _session = session;
            Text = NotexBrand.DisplayName + " · 创建用户";
            Tag = "theme:wallpaper";
            ClientSize = new Size(560, 720);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BuildUi();
        }

        /// <summary>
        /// 构建壁纸底上的金属创建卡片
        /// </summary>
        private void BuildUi()
        {
            _card = new Panel
            {
                Size = new Size(460, 640),
                Location = new Point(50, 40),
                Tag = "theme:cardEmblem"
            };
            Controls.Add(_card);
            Panel card = _card;

            Label titleLabel = new Label
            {
                Text = NotexBrand.DisplayName,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(28, 108)
            };
            card.Controls.Add(titleLabel);

            Label welcomeLabel = new Label
            {
                Text = "首次使用请创建账户。密码会加密全部笔记。\n忘记密码 = 数据无法恢复，没有后门。",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(404, 0),
                Location = new Point(28, 148)
            };
            card.Controls.Add(welcomeLabel);

            AddField(card, "登录名(英文或数字,至少2个字符)", ref _usernameBox, 230, false);
            AddField(card, "显示名(可留空)", ref _displayNameBox, 302, false);
            AddField(card, "密码(至少6位)", ref _passwordBox, 374, true);
            AddField(card, "确认密码", ref _confirmBox, 446, true);

            _createButton = new Button
            {
                Text = "创建并进入",
                Tag = "theme:accent",
                Location = new Point(28, 530),
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
                MaximumSize = new Size(404, 0),
                Location = new Point(28, 580)
            };
            card.Controls.Add(_hintLabel);

            _confirmBox.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _ = AttemptCreateAsync();
                }
            };
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
        /// 把创建卡片摆到客户区中央
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
        /// 句柄创建后的缩放可能改掉构造时的客户区，这里再钉一次
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ClientSize = new Size(560, 720);
            CenterCard();
        }

        /// <summary>
        /// 添加一个标签+输入框行
        /// </summary>
        private void AddField(Control parent, string label, ref TextBox box, int y, bool password)
        {
            Label fieldLabel = new Label
            {
                Text = label,
                AutoSize = true,
                Location = new Point(28, y)
            };
            parent.Controls.Add(fieldLabel);

            box = new TextBox
            {
                Location = new Point(28, y + 24),
                Size = new Size(404, 26),
                Font = new Font("Microsoft YaHei UI", 10F),
                UseSystemPasswordChar = password,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(box);
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
                    _createButton.Text = "创建并进入";
                }
            }
        }
    }
}
