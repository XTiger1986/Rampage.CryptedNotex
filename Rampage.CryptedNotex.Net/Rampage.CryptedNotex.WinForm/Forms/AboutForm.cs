using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.WinForm.Internal;
using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 关于窗体：版本/技术栈/加密方案摘要,附加密自检(roundtrip+篡改检测)
    /// </summary>
    public class AboutForm : NotexFormBase
    {
        private Label _resultLabel = null!;

        /// <summary>
        /// 构造关于窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        public AboutForm(IRampageApplication app) : base(app)
        {
            Text = "关于";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 400);
            BuildUi();
        }

        /// <summary>
        /// 构建界面
        /// </summary>
        private void BuildUi()
        {
            PictureBox emblem = new PictureBox
            {
                Tag = "theme:emblem",
                Location = new Point(16, 16),
                Size = new Size(64, 64)
            };
            Controls.Add(emblem);

            Label titleLabel = new Label
            {
                Text = NotexBrand.DisplayName,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(96, 22)
            };
            Controls.Add(titleLabel);

            Label technicalLabel = new Label
            {
                Text = NotexBrand.TechnicalName,
                Tag = "theme:muted",
                AutoSize = true,
                Location = new Point(98, 56)
            };
            Controls.Add(technicalLabel);

            Label infoLabel = new Label
            {
                Text = "本地离线加密笔记 · .NET 8 WinForms · SQLite\n" +
                    "加密：PBKDF2-SHA256 派生密钥 + AES-256-GCM(每笔记随机 nonce)\n" +
                    "改密即全量重加密 · 标题明文仅正文加密 · 忘记密码无后门\n" +
                    "框架：Rampage 自研 ORM/DI/日志",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Location = new Point(20, 104)
            };
            Controls.Add(infoLabel);

            Button selfTestButton = new Button
            {
                Text = "加密自检",
                Location = new Point(20, 214),
                Size = new Size(160, 36)
            };
            selfTestButton.Click += async (sender, e) => await RunSelfTestAsync(selfTestButton);
            Controls.Add(selfTestButton);

            _resultLabel = new Label
            {
                Text = "用一组临时密钥做往返、错钥、篡改和域分隔检查。不会读写你的笔记。",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Location = new Point(20, 266)
            };
            Controls.Add(_resultLabel);
        }

        /// <summary>
        /// 加密自检：roundtrip / 错钥 / 篡改 / 换盐四项。派生在后台跑，避免卡住窗口。
        /// </summary>
        private async Task RunSelfTestAsync(Button button)
        {
            button.Enabled = false;
            _resultLabel.Text = "正在自检…";
            try
            {
                string summary = await RunBusyAsync("正在做加密自检…", RunSelfTestCore);
                if (IsDisposed)
                {
                    return;
                }
                _resultLabel.Text = summary;
                bool passed = summary.StartsWith("全部通过", StringComparison.Ordinal);
                ShowToast(passed ? "加密自检通过" : summary, passed ? ToastKind.Success : ToastKind.Error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    button.Enabled = true;
                }
            }
        }

        /// <summary>
        /// 加密自检本体(可在后台线程调用)
        /// </summary>
        private static string RunSelfTestCore()
        {
            try
            {
                byte[] key = CryptoTool.DeriveContentKey("self-test-密码", CryptoTool.GenerateSaltBase64(), 100000);
                byte[] plain = Encoding.UTF8.GetBytes("自检明文 CryptedNotex ✔ 中文 emoji");
                byte[] blob = CryptoTool.Encrypt(plain, key, CryptoTool.AAD_NOTE);

                byte[] decrypted = CryptoTool.Decrypt(blob, key, CryptoTool.AAD_NOTE);
                bool roundtripOk = Encoding.UTF8.GetString(decrypted) == Encoding.UTF8.GetString(plain);

                bool wrongKeyRejected = false;
                try
                {
                    byte[] wrongKey = CryptoTool.DeriveContentKey("wrong", CryptoTool.GenerateSaltBase64(), 100000);
                    CryptoTool.Decrypt(blob, wrongKey, CryptoTool.AAD_NOTE);
                }
                catch (CryptographicException)
                {
                    wrongKeyRejected = true;
                }

                bool tamperRejected = false;
                try
                {
                    blob[^1] ^= 0xFF;
                    CryptoTool.Decrypt(blob, key, CryptoTool.AAD_NOTE);
                }
                catch (CryptographicException)
                {
                    tamperRejected = true;
                }

                bool aadRejected = false;
                byte[] blob2 = CryptoTool.Encrypt(plain, key, CryptoTool.AAD_ATTACHMENT);
                try
                {
                    CryptoTool.Decrypt(blob2, key, CryptoTool.AAD_NOTE);
                }
                catch (CryptographicException)
                {
                    aadRejected = true;
                }

                CryptoTool.ClearKey(key);
                bool allOk = roundtripOk && wrongKeyRejected && tamperRejected && aadRejected;
                return allOk ? "全部通过：roundtrip/错钥拒绝/篡改拒绝/域分隔拒绝"
                    : "自检未通过 roundtrip=" + roundtripOk + " 错钥拒绝=" + wrongKeyRejected
                    + " 篡改拒绝=" + tamperRejected + " 域分隔拒绝=" + aadRejected;
            }
            catch (Exception ex)
            {
                return "自检异常：" + ex.Message;
            }
        }
    }
}
