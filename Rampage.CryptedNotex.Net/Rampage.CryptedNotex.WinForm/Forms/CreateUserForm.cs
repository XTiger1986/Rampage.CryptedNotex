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

        private TextBox _usernameBox = null!;
        private TextBox _displayNameBox = null!;
        private TextBox _passwordBox = null!;
        private TextBox _confirmBox = null!;
        private Button _createButton = null!;
        private Label _hintLabel = null!;

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
            ClientSize = new Size(560, 800);
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
            Panel card = new Panel
            {
                Size = new Size(460, 720),
                Location = new Point(50, 40),
                Tag = "theme:cardEmblem"
            };
            Controls.Add(card);

            Label titleLabel = new Label
            {
                Text = NotexBrand.DisplayName,
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(28, 116)
            };
            card.Controls.Add(titleLabel);

            Label welcomeLabel = new Label
            {
                Text = "首次使用请创建账户。密码会加密全部笔记。\n忘记密码 = 数据无法恢复，没有后门。",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(404, 0),
                Location = new Point(28, 158)
            };
            card.Controls.Add(welcomeLabel);

            AddField(card, "登录名(英文或数字,至少2个字符)", ref _usernameBox, 250, false);
            AddField(card, "显示名(可留空)", ref _displayNameBox, 322, false);
            AddField(card, "密码(至少6位)", ref _passwordBox, 394, true);
            AddField(card, "确认密码", ref _confirmBox, 466, true);

            _createButton = new Button
            {
                Text = "创建并进入",
                Tag = "theme:accent",
                Location = new Point(28, 552),
                Size = new Size(404, 36),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _createButton.Click += (sender, e) => AttemptCreate();
            card.Controls.Add(_createButton);

            _hintLabel = new Label
            {
                Text = string.Empty,
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(404, 0),
                Location = new Point(28, 600)
            };
            card.Controls.Add(_hintLabel);

            _confirmBox.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AttemptCreate();
                }
            };
        }

        /// <summary>
        /// 句柄创建后的缩放可能改掉构造时的客户区，这里再钉一次
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ClientSize = new Size(560, 800);
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
                Location = new Point(28, y + 22),
                Size = new Size(404, 26),
                Font = new Font("Microsoft YaHei UI", 10F),
                UseSystemPasswordChar = password,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(box);
        }

        /// <summary>
        /// 尝试创建用户
        /// </summary>
        private void AttemptCreate()
        {
            string username = _usernameBox.Text;
            string displayName = _displayNameBox.Text;
            string password = _passwordBox.Text;

            if (password != _confirmBox.Text)
            {
                _hintLabel.Text = "两次输入的密码不一致";
                return;
            }

            _createButton.Enabled = false;
            try
            {
                R<UserBriefDto> result = GetService<IUserService>().CreateUser(username, displayName, password, out byte[]? contentKey);
                if (!result.IsSuccess || result.Data == null || contentKey == null)
                {
                    _hintLabel.Text = result.Message ?? "创建失败";
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
            finally
            {
                _createButton.Enabled = true;
            }
        }
    }
}
