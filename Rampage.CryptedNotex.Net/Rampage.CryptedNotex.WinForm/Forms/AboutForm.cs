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
            ClientSize = new Size(440, 360);
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
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(92, 20)
            };
            Controls.Add(titleLabel);

            Label technicalLabel = new Label
            {
                Text = NotexBrand.TechnicalName,
                Tag = "theme:muted",
                AutoSize = true,
                Location = new Point(94, 52)
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
                MaximumSize = new Size(400, 0),
                Location = new Point(16, 96)
            };
            Controls.Add(infoLabel);

            Button selfTestButton = new Button
            {
                Text = "加密自检",
                Location = new Point(16, 200),
                Size = new Size(180, 34)
            };
            selfTestButton.Click += (sender, e) => RunSelfTest();
            Controls.Add(selfTestButton);

            _resultLabel = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                MaximumSize = new Size(390, 0),
                Location = new Point(16, 248)
            };
            Controls.Add(_resultLabel);
        }

        /// <summary>
        /// 加密自检：roundtrip / 错钥 / 篡改 / 换盐四项
        /// </summary>
        private void RunSelfTest()
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
                _resultLabel.Text = (allOk ? "全部通过：roundtrip/错钥拒绝/篡改拒绝/域分隔拒绝"
                    : "自检未通过 roundtrip=" + roundtripOk + " 错钥拒绝=" + wrongKeyRejected
                    + " 篡改拒绝=" + tamperRejected + " 域分隔拒绝=" + aadRejected);
            }
            catch (Exception ex)
            {
                _resultLabel.Text = "自检异常：" + ex.Message;
            }
        }
    }
}
