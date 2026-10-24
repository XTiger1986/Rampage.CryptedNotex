using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.Model;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 修改用户密码窗体：旧/新/确认 + 进度条；后台执行全量重加密,完成后替换会话密钥
    /// </summary>
    public class ChangePasswordForm : NotexFormBase
    {
        private readonly CryptoSession _session;
        private readonly int _userId;

        private TextBox _oldBox = null!;
        private TextBox _newBox = null!;
        private TextBox _confirmBox = null!;
        private Button _okButton = null!;
        private ProgressBar _progressBar = null!;
        private Label _statusLabel = null!;

        /// <summary>
        /// 构造改密窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        public ChangePasswordForm(IRampageApplication app, CryptoSession session) : base(app)
        {
            _session = session;
            _userId = session.User!.Id;
            Text = "修改密码(全部笔记将重新加密)";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 300);
            BuildUi();
        }

        /// <summary>
        /// 构建界面
        /// </summary>
        private void BuildUi()
        {
            int y = 16;
            y = AddPasswordField("旧密码", ref _oldBox, y);
            y = AddPasswordField("新密码(至少6位)", ref _newBox, y);
            y = AddPasswordField("确认新密码", ref _confirmBox, y);

            _okButton = new Button
            {
                Text = "开始修改",
                Tag = "theme:accent",
                Location = new Point(16, y + 6),
                Size = new Size(348, 34)
            };
            _okButton.Click += async (sender, e) => await AttemptChangeAsync();
            Controls.Add(_okButton);

            _progressBar = new ProgressBar
            {
                Location = new Point(16, y + 52),
                Size = new Size(348, 18),
                Minimum = 0,
                Maximum = 100,
                Visible = false
            };
            Controls.Add(_progressBar);

            _statusLabel = new Label
            {
                Text = string.Empty,
                Tag = "theme:muted",
                AutoSize = true,
                Location = new Point(16, y + 78)
            };
            Controls.Add(_statusLabel);
            ClientSize = new Size(380, y + 110);
        }

        /// <summary>
        /// 添加密码行
        /// </summary>
        private int AddPasswordField(string label, ref TextBox box, int y)
        {
            Label fieldLabel = new Label
            {
                Text = label,
                AutoSize = true,
                Location = new Point(16, y)
            };
            Controls.Add(fieldLabel);
            box = new TextBox
            {
                Location = new Point(16, y + 20),
                Size = new Size(348, 26),
                Font = new Font("Microsoft YaHei UI", 10F),
                UseSystemPasswordChar = true
            };
            Controls.Add(box);
            return y + 54;
        }

        /// <summary>
        /// 执行改密(含全量重加密,模态期间禁止其他操作)
        /// </summary>
        private async Task AttemptChangeAsync()
        {
            string oldPassword = _oldBox.Text;
            string newPassword = _newBox.Text;
            if (newPassword.Length < 6)
            {
                _statusLabel.Text = "新密码至少 6 位";
                return;
            }
            if (newPassword != _confirmBox.Text)
            {
                _statusLabel.Text = "两次输入的新密码不一致";
                return;
            }

            _okButton.Enabled = false;
            _progressBar.Visible = true;
            _statusLabel.Text = "正在派生密钥并重新加密笔记…";
            IProgress<int> progress = new Progress<int>(value => _progressBar.Value = Math.Min(value, 100));

            try
            {
                R result = await Task.Run(() => GetService<IUserService>().ChangePassword(_userId, oldPassword, newPassword, progress));
                if (result.IsSuccess)
                {
                    if (result.Data is byte[] newKey)
                    {
                        _session.ReplaceUserKey(newKey);
                    }
                    _statusLabel.Text = "修改完成";
                    MessageBox.Show("密码修改完成，全部笔记已用新密码重新加密。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                _statusLabel.Text = result.Message ?? "修改失败";
                _progressBar.Visible = false;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "修改失败：" + ex.Message;
                _progressBar.Visible = false;
            }
            finally
            {
                _okButton.Enabled = true;
            }
        }
    }
}
