using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.Model;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 设置窗体：主题画廊(即时切换)/自动锁定分钟/编辑器字体/显示名修改/修改密码入口
    /// </summary>
    public class SettingsForm : NotexFormBase
    {
        private readonly CryptoSession _session;
        private readonly UserSetting _setting;

        private FlowLayoutPanel _themeGallery = null!;
        private NumericUpDown _lockMinutesBox = null!;
        private ComboBox _fontFamilyBox = null!;
        private NumericUpDown _fontSizeBox = null!;
        private TextBox _displayNameBox = null!;
        private Label _statusLabel = null!;

        /// <summary>
        /// 构造设置窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        /// <param name="setting">当前用户偏好(修改后直接写库并同步此实例)</param>
        public SettingsForm(IRampageApplication app, CryptoSession session, UserSetting setting) : base(app)
        {
            _session = session;
            _setting = setting;
            Text = "设置";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 560);
            BuildUi();
        }

        /// <summary>
        /// 构建界面
        /// </summary>
        private void BuildUi()
        {
            int y = 14;

            Label themeTitle = new Label
            {
                Text = "界面主题",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, y)
            };
            Controls.Add(themeTitle);
            y += 26;

            _themeGallery = new FlowLayoutPanel
            {
                Location = new Point(16, y),
                Size = new Size(448, 180),
                AutoScroll = true
            };
            Controls.Add(_themeGallery);
            y += 190;

            y = AddSection("自动锁定(分钟,0=不锁定)", y);
            _lockMinutesBox = new NumericUpDown
            {
                Location = new Point(16, y),
                Size = new Size(120, 26),
                Minimum = 0,
                Maximum = 240,
                Value = Math.Max(0, Math.Min(240, _setting.AutoLockMinutes))
            };
            Controls.Add(_lockMinutesBox);
            y += 40;

            y = AddSection("编辑器字体(纯文本/Markdown)", y);
            _fontFamilyBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, y),
                Size = new Size(200, 26)
            };
            foreach (string family in new string[] { "Consolas", "Cascadia Mono", "Cascadia Code", "Courier New", "Microsoft YaHei UI", "SimSun" })
            {
                _fontFamilyBox.Items.Add(family);
            }
            _fontFamilyBox.SelectedItem = _setting.EditorFontFamily;
            if (_fontFamilyBox.SelectedIndex < 0)
            {
                _fontFamilyBox.SelectedIndex = 0;
            }
            Controls.Add(_fontFamilyBox);

            _fontSizeBox = new NumericUpDown
            {
                Location = new Point(230, y),
                Size = new Size(70, 26),
                Minimum = 8,
                Maximum = 28,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Value = (decimal)Math.Max(8, Math.Min(28, _setting.EditorFontSize))
            };
            Controls.Add(_fontSizeBox);
            y += 40;

            y = AddSection("显示名", y);
            _displayNameBox = new TextBox
            {
                Text = _session.User?.DisplayName ?? string.Empty,
                Location = new Point(16, y),
                Size = new Size(284, 26),
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            Controls.Add(_displayNameBox);
            y += 40;

            Button changePasswordButton = new Button
            {
                Text = "修改密码(全量重加密)…",
                Location = new Point(16, y),
                Size = new Size(220, 32)
            };
            changePasswordButton.Click += (sender, e) =>
            {
                using (ChangePasswordForm form = new ChangePasswordForm(App, _session))
                {
                    form.ShowDialog(this);
                }
            };
            Controls.Add(changePasswordButton);

            Button saveButton = new Button
            {
                Text = "保存",
                Tag = "theme:accent",
                Location = new Point(16, y + 44),
                Size = new Size(220, 34)
            };
            saveButton.Click += (sender, e) => Save();
            Controls.Add(saveButton);

            Button closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(252, y + 44),
                Size = new Size(212, 34)
            };
            closeButton.Click += (sender, e) => Close();
            Controls.Add(closeButton);

            _statusLabel = new Label
            {
                Text = string.Empty,
                Tag = "theme:muted",
                AutoSize = true,
                Location = new Point(16, y + 92)
            };
            Controls.Add(_statusLabel);
            ClientSize = new Size(480, y + 124);
        }

        /// <summary>
        /// 添加节标题,返回下一行 y
        /// </summary>
        private int AddSection(string title, int y)
        {
            Label label = new Label
            {
                Text = title,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, y)
            };
            Controls.Add(label);
            return y + 26;
        }

        /// <summary>
        /// 加载时填充主题画廊
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildThemeGallery();
        }

        /// <summary>
        /// 主题画廊：每主题一张卡片(名称+三色样+当前高亮),点击即时切换并保存偏好
        /// </summary>
        private void BuildThemeGallery()
        {
            _themeGallery.SuspendLayout();
            _themeGallery.Controls.Clear();
            foreach (Theme theme in ThemeManager.AvailableThemes)
            {
                ThemeCard card = new ThemeCard(theme)
                {
                    Margin = new Padding(0, 0, 10, 8),
                    Size = new Size(210, 68),
                    Selected = theme.Id == ThemeManager.Current.Id
                };
                card.CardClicked += (sender, themeId) =>
                {
                    ThemeManager.ApplyTheme(themeId);
                    _setting.ThemeId = themeId;
                    GetService<IUserSettingService>().SaveUserSetting(_setting);
                    BuildThemeGallery();
                };
                _themeGallery.Controls.Add(card);
            }
            _themeGallery.ResumeLayout();
        }

        /// <summary>
        /// 保存偏好(自动锁定/编辑器字体/显示名)
        /// </summary>
        private void Save()
        {
            _setting.AutoLockMinutes = (int)_lockMinutesBox.Value;
            _setting.EditorFontFamily = (string?)_fontFamilyBox.SelectedItem ?? "Consolas";
            _setting.EditorFontSize = (double)_fontSizeBox.Value;

            R saved = GetService<IUserSettingService>().SaveUserSetting(_setting);
            string status = saved.IsSuccess ? "已保存" : ("保存失败：" + saved.Message);

            string displayName = _displayNameBox.Text.Trim();
            if (displayName.Length > 0 && displayName != _session.User?.DisplayName)
            {
                R rename = GetService<IUserService>().UpdateDisplayName(_session.User!.Id, displayName);
                status = rename.IsSuccess ? status + "，显示名已更新" : status + "，显示名更新失败";
            }
            _statusLabel.Text = status;
        }

        /// <summary>
        /// 主题卡片控件
        /// </summary>
        private class ThemeCard : Control
        {
            private readonly Theme _theme;
            private bool _selected;

            /// <summary>
            /// 卡片被点击(参数为主题 id)
            /// </summary>
            public event EventHandler<string>? CardClicked;

            /// <summary>
            /// 构造主题卡片
            /// </summary>
            public ThemeCard(Theme theme)
            {
                _theme = theme;
                Cursor = Cursors.Hand;
            }

            /// <summary>
            /// 是否为当前主题(边框高亮)
            /// </summary>
            public bool Selected
            {
                get
                {
                    return _selected;
                }
                set
                {
                    _selected = value;
                    Invalidate();
                }
            }

            /// <summary>
            /// 自绘卡片
            /// </summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                string windowBack = _theme.Colors.TryGetValue("windowBack", out string? wb) ? wb : "#FFFFFF";
                string accent = _theme.Colors.TryGetValue("accent", out string? ac) ? ac : "#007ACC";
                string text = _theme.Colors.TryGetValue("text", out string? tx) ? tx : "#202226";
                string glow = _theme.Chrome != null && !string.IsNullOrWhiteSpace(_theme.Chrome.Glow) ? _theme.Chrome.Glow : accent;
                Color backColor = ColorTranslator.FromHtml(windowBack);
                Color accentColor = ColorTranslator.FromHtml(accent);
                Color textColor = ColorTranslator.FromHtml(text);
                Color glowColor = ColorTranslator.FromHtml(glow);

                using (SolidBrush backBrush = new SolidBrush(backColor))
                using (SolidBrush accentBrush = new SolidBrush(accentColor))
                using (SolidBrush textBrush = new SolidBrush(textColor))
                using (Pen borderPen = new Pen(_selected ? glowColor : SystemColors.ControlDark, _selected ? 2F : 1F))
                using (Font font = new Font("Microsoft YaHei UI", 9.5F))
                {
                    g.FillRectangle(backBrush, ClientRectangle);
                    Image? wallpaper = _theme.Chrome == null ? null : ChromeAssets.GetWallpaper(_theme.Chrome.Wallpaper);
                    if (wallpaper != null)
                    {
                        g.DrawImage(wallpaper, new Rectangle(1, 1, Width - 2, 26));
                    }
                    else
                    {
                        g.FillRectangle(accentBrush, new Rectangle(1, 1, Width - 2, 8));
                    }
                    g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
                    g.FillRectangle(accentBrush, new Rectangle(8, 36, 18, 18));
                    g.DrawString(_theme.Name, font, textBrush, new PointF(34, 36));
                }
            }

            /// <summary>
            /// 点击卡片
            /// </summary>
            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                CardClicked?.Invoke(this, _theme.Id);
            }
        }
    }
}
