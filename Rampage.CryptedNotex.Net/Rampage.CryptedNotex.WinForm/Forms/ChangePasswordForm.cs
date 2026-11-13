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
        private bool _changing;

        /// <summary>
        /// 构造改密窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        public ChangePasswordForm(IRampageApplication app, CryptoSession session) : base(app)
        {
            _session = session;
            _userId = session.User!.Id;
            Text = "修改密码";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 360);
            BuildUi();
        }

        /// <summary>
        /// 构建界面
        /// </summary>
        private void BuildUi()
        {
            ClientSize = new Size(500, 500);
            DialogBody body = new DialogBody();
            Controls.Add(body);
            body.AddHeading("更新档案密码");
            body.AddHelp("笔记和附件会使用新密钥重新加密。请牢记新密码，并等待操作完成。");
            _oldBox = new TextBox { Font = new Font("Microsoft YaHei UI", 10.5F), UseSystemPasswordChar = true };
            _newBox = new TextBox { Font = new Font("Microsoft YaHei UI", 10.5F), UseSystemPasswordChar = true };
            _confirmBox = new TextBox { Font = new Font("Microsoft YaHei UI", 10.5F), UseSystemPasswordChar = true };
            body.AddField("当前密码", _oldBox);
            body.AddField("新密码（至少 6 位）", _newBox);
            body.AddField("确认新密码", _confirmBox);
            _okButton = UiFactory.Button("更新密码", "theme:accent");
            _okButton.Dock = DockStyle.Top;
            _okButton.Margin = new Padding(0, 0, 0, 16);
            _okButton.Click += async (sender, e) => await AttemptChangeAsync();
            body.Add(_okButton);
            AcceptButton = _okButton;
            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 12, Minimum = 0, Maximum = 100, Visible = false, Margin = new Padding(0, 0, 0, 12) };
            body.Add(_progressBar);
            _statusLabel = new Label { Text = string.Empty, Tag = "theme:muted", AutoSize = true, MaximumSize = new Size(440, 0), Margin = Padding.Empty };
            body.Add(_statusLabel);
            Shown += (sender, e) => { body.FitForm(this); _oldBox.Focus(); };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_changing)
            {
                e.Cancel = true;
                ShowToast("密码正在更新，请等待重新加密完成。", ToastKind.Info);
                return;
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// 执行改密(含全量重加密,模态期间禁止其他操作)
        /// </summary>
        private async Task AttemptChangeAsync()
        {
            if (_changing)
            {
                return;
            }
            string oldPassword = _oldBox.Text;
            string newPassword = _newBox.Text;
            if (newPassword.Length < 6)
            {
                _statusLabel.Text = "新密码至少 6 位";
                ShowToast("新密码至少 6 位", ToastKind.Error);
                return;
            }
            if (newPassword != _confirmBox.Text)
            {
                _statusLabel.Text = "两次输入的新密码不一致";
                ShowToast("两次输入的新密码不一致", ToastKind.Error);
                return;
            }

            _changing = true;
            _okButton.Enabled = false;
            _oldBox.Enabled = false;
            _newBox.Enabled = false;
            _confirmBox.Enabled = false;
            _progressBar.Visible = true;
            _progressBar.Value = 0;
            _statusLabel.Text = "正在派生密钥并重新加密笔记…";
            IProgress<int> progress = new Progress<int>(value =>
            {
                if (IsDisposed)
                {
                    return;
                }
                int percent = Math.Min(Math.Max(value, 0), 100);
                _progressBar.Value = percent;
                _statusLabel.Text = "正在重新加密… " + percent + "%";
            });

            try
            {
                R result = await UiWork.RunAsync(() => GetService<IUserService>().ChangePassword(_userId, oldPassword, newPassword, progress));
                if (result.IsSuccess)
                {
                    if (result.Data is byte[] newKey)
                    {
                        _session.ReplaceUserKey(newKey);
                    }
                    _statusLabel.Text = "修改完成";
                    _changing = false;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                string message = result.Message ?? "修改失败";
                _statusLabel.Text = message;
                ShowToast(message, ToastKind.Error);
                _progressBar.Visible = false;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "修改失败：" + ex.Message;
                ShowToast("修改失败：" + ex.Message, ToastKind.Error);
                _progressBar.Visible = false;
            }
            finally
            {
                _changing = false;
                if (!IsDisposed && DialogResult != DialogResult.OK)
                {
                    _okButton.Enabled = true;
                    _oldBox.Enabled = true;
                    _newBox.Enabled = true;
                    _confirmBox.Enabled = true;
                }
            }
        }
    }
}
