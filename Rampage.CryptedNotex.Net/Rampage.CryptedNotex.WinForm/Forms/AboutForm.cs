using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
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
        private Panel _emblem = null!;
        private Label _titleLabel = null!;
        private Label _techLabel = null!;
        private Label _infoLabel = null!;
        private Button _selfTestButton = null!;
        private Label _resultLabel = null!;
        private Size _clientSize;

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
        /// 构建界面(控件只创建,位置交给 <see cref="LayoutCard"/> 统一流动排版)
        /// </summary>
        private void BuildUi()
        {
            _emblem = new Panel
            {
                Tag = "theme:card",
                Size = new Size(64, 64)
            };
            _emblem.Paint += (sender, e) => ThemeArtwork.Draw(e.Graphics, _emblem.ClientRectangle, ThemeManager.Current);
            Controls.Add(_emblem);

            _titleLabel = new Label
            {
                Text = NotexBrand.DisplayName,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true
            };
            Controls.Add(_titleLabel);

            _techLabel = new Label
            {
                Text = NotexBrand.TechnicalName,
                Tag = "theme:muted",
                AutoSize = true,
                Font = new Font("Consolas", 9.5F)
            };
            Controls.Add(_techLabel);

            _infoLabel = new Label
            {
                Text = "本地离线加密笔记 · .NET 8 WinForms · SQLite\n" +
                    "加密：PBKDF2-SHA256 派生密钥 + AES-256-GCM(每笔记随机 nonce)\n" +
                    "改密即全量重加密 · 标题明文仅正文加密 · 忘记密码无后门\n" +
                    "框架：Rampage 自研 ORM/DI/日志",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            Controls.Add(_infoLabel);

            _selfTestButton = new Button
            {
                Text = "加密自检",
                Size = new Size(160, 36)
            };
            _selfTestButton.Click += async (sender, e) => await RunSelfTestAsync(_selfTestButton);
            Controls.Add(_selfTestButton);

            _resultLabel = new Label
            {
                Text = "用一组临时密钥做往返、错钥、篡改和域分隔检查。不会读写你的笔记。",
                Tag = "theme:muted",
                AutoSize = true,
                MaximumSize = new Size(420, 0)
            };
            Controls.Add(_resultLabel);

            LayoutCard();
        }

        /// <summary>
        /// 按实测控件高度流动排版(兼容高 DPI 字体放大,标题不再压住技术名);
        /// 自检结果文本变化后也可重调,窗高随内容自适应
        /// </summary>
        private void LayoutCard()
        {
            int x = Scale(24);
            int textX = x + Scale(84);
            _emblem.Size = new Size(Scale(64), Scale(64));
            _emblem.Location = new Point(x, Scale(24));
            _titleLabel.Location = new Point(textX, Scale(24));
            _techLabel.Location = new Point(textX + 2, _titleLabel.Bottom + Scale(4));
            int headerBottom = Math.Max(_emblem.Bottom, _techLabel.Bottom) + Scale(24);

            _infoLabel.MaximumSize = new Size(Scale(472), 0);
            _resultLabel.MaximumSize = new Size(Scale(472), 0);
            _selfTestButton.Size = new Size(Scale(160), Scale(38));
            _infoLabel.Location = new Point(x, headerBottom);
            int y = _infoLabel.Bottom + Scale(18);
            _selfTestButton.Location = new Point(x, y);
            y += _selfTestButton.Height + Scale(12);
            _resultLabel.Location = new Point(x, y);

            _clientSize = new Size(Scale(520), Math.Max(Scale(360), _resultLabel.Bottom + Scale(28)));
            Rectangle work = Screen.FromControl(this).WorkingArea;
            ClientSize = new Size(Math.Min(_clientSize.Width, work.Width - Scale(32)), Math.Min(_clientSize.Height, work.Height - Scale(72)));
            AutoScroll = true;
        }

        private int Scale(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96D);
        }

        /// <summary>
        /// 句柄创建后字体已按实际 DPI 定型,重排一次并钉回计算出的尺寸
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutCard();
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
                LayoutCard();
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
