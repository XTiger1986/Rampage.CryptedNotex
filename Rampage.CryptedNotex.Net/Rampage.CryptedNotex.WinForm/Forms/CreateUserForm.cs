using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.WinForm.Internal;
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
            Text = "加密笔记本 · 创建用户";
            ClientSize = new Size(400, 360);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BuildUi();
        }

        /// <summary>
        /// 构建界面
        /// </summary>
        private void BuildUi()
        {
            Label titleLabel = new Label
            {
                Text = "欢迎使用加密笔记本",
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 16)
            };
            Controls.Add(titleLabel);

            Label welcomeLabel = new Label
            {
                Text = "首次使用请创建你的账户。密码用于加密全部笔记：\n忘记密码 = 数据无法恢复，无后门。",
                Tag = "theme:muted",
                AutoSize = true,
                Location = new Point(20, 52)
            };
            Controls.Add(welcomeLabel);

            AddField("登录名(英文/数字,至少2字符)", ref _usernameBox, 96, false);
            AddField("显示名(可留空)", ref _displayNameBox, 140, false);
            AddField("密码(至少6位)", ref _passwordBox, 184, true);
            AddField("确认密码", ref _confirmBox, 228, true);

            _createButton = new Button
            {
                Text = "创建并进入",
                Tag = "theme:accent",
                Location = new Point(20, 274),
                Size = new Size(360, 36),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            _createButton.Click += (sender, e) => AttemptCreate();
            Controls.Add(_createButton);

            _hintLabel = new Label
            {
                Text = string.Empty,
                Tag = "theme:muted",
                AutoSize = true,
                Location = new Point(20, 320)
            };
            Controls.Add(_hintLabel);

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
        /// 添加一个标签+输入框行
        /// </summary>
        private void AddField(string label, ref TextBox box, int y, bool password)
        {
            Label fieldLabel = new Label
            {
                Text = label,
                AutoSize = true,
                Location = new Point(20, y)
            };
            Controls.Add(fieldLabel);

            box = new TextBox
            {
                Location = new Point(20, y + 20),
                Size = new Size(360, 26),
                Font = new Font("Microsoft YaHei UI", 10F),
                UseSystemPasswordChar = password
            };
            Controls.Add(box);
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
