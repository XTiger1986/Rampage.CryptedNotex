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
            ClientSize = new Size(520, 620);
            BuildUi();
            // 点卡片即时换主题时,背后的主窗体实时变色,本窗体也要跟着刷,不能等重开
            ThemeManager.CurrentChanged += OnThemeChanged;
            Disposed += (sender, e) => ThemeManager.CurrentChanged -= OnThemeChanged;
        }

        /// <summary>
        /// 主题切换后重刷本窗体(推迟到下一拍,避开切换事件的调用栈)
        /// </summary>
        private void OnThemeChanged(Theme theme)
        {
            if (IsDisposed)
            {
                return;
            }
            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(() => ThemeManager.Apply(this)));
                }
                catch (InvalidOperationException)
                {
                    // 句柄正在销毁
                }
            }
            else
            {
                ThemeManager.Apply(this);
            }
        }

        /// <summary>
        /// 构建界面：界面主题/编辑器/账户与安全三段,行高按实测值流动(兼容高 DPI 字体放大)
        /// </summary>
        private void BuildUi()
        {
            int x = 20;
            int width = 480;
            int y = 20;

            // ── 界面主题 ──
            y = AddSectionHeader("界面主题", y, width);
            _themeGallery = new FlowLayoutPanel
            {
                Location = new Point(x, y),
                Size = new Size(width, 300),
                AutoScroll = true
            };
            Controls.Add(_themeGallery);
            y += 312;

            // ── 编辑器 ──
            y = AddSectionHeader("编辑器", y, width);
            Label fontLabel = new Label
            {
                Text = "纯文本/Markdown 字体",
                AutoSize = true,
                Location = new Point(x, y)
            };
            Controls.Add(fontLabel);
            y += fontLabel.Height + 6;

            _fontFamilyBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(x, y),
                Size = new Size(220, 28)
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
                Location = new Point(x + 236, y),
                Size = new Size(72, 28),
                Minimum = 8,
                Maximum = 28,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Value = (decimal)Math.Max(8, Math.Min(28, _setting.EditorFontSize))
            };
            Controls.Add(_fontSizeBox);
            y += RowHeight(_fontSizeBox) + 16;

            // ── 账户与安全 ──
            y = AddSectionHeader("账户与安全", y, width);
            Label lockLabel = new Label
            {
                Text = "自动锁定(分钟,0=不锁定)",
                AutoSize = true,
                Location = new Point(x, y)
            };
            Controls.Add(lockLabel);
            y += lockLabel.Height + 6;
            _lockMinutesBox = new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(96, 28),
                Minimum = 0,
                Maximum = 240,
                Value = Math.Max(0, Math.Min(240, _setting.AutoLockMinutes))
            };
            Controls.Add(_lockMinutesBox);
            y += RowHeight(_lockMinutesBox) + 16;

            Label displayLabel = new Label
            {
                Text = "显示名",
                AutoSize = true,
                Location = new Point(x, y)
            };
            Controls.Add(displayLabel);
            y += displayLabel.Height + 6;
            _displayNameBox = new TextBox
            {
                Text = _session.User?.DisplayName ?? string.Empty,
                Location = new Point(x, y),
                Size = new Size(316, 28),
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            Controls.Add(_displayNameBox);
            y += RowHeight(_displayNameBox) + 16;

            Button changePasswordButton = new Button
            {
                Text = "修改密码(全量重加密)…",
                Location = new Point(x, y),
                Size = new Size(240, 36)
            };
            changePasswordButton.Click += (sender, e) =>
            {
                using (ChangePasswordForm form = new ChangePasswordForm(App, _session))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        ShowToast("密码已更新，全部笔记已用新密码重新加密。", ToastKind.Success);
                    }
                }
            };
            Controls.Add(changePasswordButton);
            y += 48;

            Button saveButton = new Button
            {
                Text = "保存",
                Tag = "theme:accent",
                Location = new Point(x, y),
                Size = new Size(240, 36)
            };
            saveButton.Click += (sender, e) => Save();
            Controls.Add(saveButton);

            Button closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(276, y),
                Size = new Size(224, 36)
            };
            closeButton.Click += (sender, e) => Close();
            Controls.Add(closeButton);
            y += 48;

            _statusLabel = new Label
            {
                Text = string.Empty,
                Tag = "theme:muted",
                AutoSize = true,
                Location = new Point(x, y)
            };
            Controls.Add(_statusLabel);
            ClientSize = new Size(520, y + 40);
            AcceptButton = saveButton;
            CancelButton = closeButton;
        }

        /// <summary>
        /// 节标题 + 主题色细分隔线,返回内容起始 y
        /// </summary>
        private int AddSectionHeader(string title, int y, int width)
        {
            Label label = new Label
            {
                Text = title,
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, y)
            };
            Controls.Add(label);

            Panel line = new Panel
            {
                Location = new Point(20, y + label.Height + 4),
                Size = new Size(width, 2)
            };
            line.Paint += (sender, e) =>
            {
                using (Pen pen = new Pen(ThemeManager.GetColor("border")))
                {
                    e.Graphics.DrawLine(pen, 0, 0, line.Width, 0);
                }
            };
            Controls.Add(line);
            return y + label.Height + 16;
        }

        /// <summary>
        /// 控件行高(实际高与 PreferredHeight 取大,高 DPI 下输入框会随字体变高)
        /// </summary>
        private static int RowHeight(Control control)
        {
            return Math.Max(control.Height, control.PreferredSize.Height);
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
                    Margin = new Padding(0, 0, 12, 10),
                    // 210×2+间距=444:即使竖向滚动条(150% 缩放下约 26px)吃掉宽度,两列也不会溢出横向滚动条
                    Size = new Size(210, 86),
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
            ToastKind kind = saved.IsSuccess ? ToastKind.Success : ToastKind.Error;

            string displayName = _displayNameBox.Text.Trim();
            if (displayName.Length > 0 && displayName != _session.User?.DisplayName)
            {
                R rename = GetService<IUserService>().UpdateDisplayName(_session.User!.Id, displayName);
                if (rename.IsSuccess)
                {
                    status = status + "，显示名已更新";
                }
                else
                {
                    status = status + "，显示名更新失败";
                    kind = ToastKind.Error;
                }
            }
            _statusLabel.Text = status;
            ShowToast(status, kind);
        }

        /// <summary>
        /// 主题卡片控件
        /// </summary>
        private class ThemeCard : Control
        {
            private readonly Theme _theme;
            private bool _selected;
            private bool _hover;

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
                MouseEnter += (sender, e) => SetHover(true);
                MouseLeave += (sender, e) => SetHover(false);
            }

            /// <summary>
            /// 悬停态变化时重绘
            /// </summary>
            private void SetHover(bool hover)
            {
                if (_hover == hover)
                {
                    return;
                }
                _hover = hover;
                Invalidate();
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
            /// 自绘卡片:壁纸/强调色条 + 名称 + 身份描述 + 三色点
            /// </summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                string windowBack = _theme.Colors.TryGetValue("windowBack", out string? wb) ? wb : "#FFFFFF";
                string accent = _theme.Colors.TryGetValue("accent", out string? ac) ? ac : "#007ACC";
                string text = _theme.Colors.TryGetValue("text", out string? tx) ? tx : "#202226";
                string textSecondary = _theme.Colors.TryGetValue("textSecondary", out string? ts) ? ts : text;
                string glow = _theme.Chrome != null && !string.IsNullOrWhiteSpace(_theme.Chrome.Glow) ? _theme.Chrome.Glow : accent;
                Color backColor = ColorTranslator.FromHtml(windowBack);
                Color accentColor = ColorTranslator.FromHtml(accent);
                Color textColor = ColorTranslator.FromHtml(text);
                Color mutedColor = ColorTranslator.FromHtml(textSecondary);
                Color glowColor = ColorTranslator.FromHtml(glow);

                using (SolidBrush backBrush = new SolidBrush(backColor))
                using (SolidBrush accentBrush = new SolidBrush(accentColor))
                using (Pen borderPen = BuildBorderPen(glowColor))
                using (Font nameFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold))
                using (Font descFont = new Font("Microsoft YaHei UI", 8.25F))
                {
                    g.FillRectangle(backBrush, ClientRectangle);
                    Image? wallpaper = _theme.Chrome == null ? null : ChromeAssets.GetWallpaper(_theme.Chrome.Wallpaper);
                    if (wallpaper != null)
                    {
                        g.DrawImage(wallpaper, new Rectangle(1, 1, Width - 2, 22));
                    }
                    else
                    {
                        g.FillRectangle(accentBrush, new Rectangle(1, 1, Width - 2, 6));
                    }

                    TextRenderer.DrawText(
                        g,
                        _theme.Name,
                        nameFont,
                        new Rectangle(10, 28, Width - 78, 20),
                        textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(
                        g,
                        _theme.Description ?? string.Empty,
                        descFont,
                        new Rectangle(10, 50, Width - 78, 18),
                        mutedColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    DrawIdentityDot(g, Width - 64, 33, accentColor);
                    DrawIdentityDot(g, Width - 48, 33, PaletteColor(0, accentColor));
                    DrawIdentityDot(g, Width - 32, 33, PaletteColor(1, accentColor));

                    g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
                }
            }

            /// <summary>
            /// 分类色板取色(越界或非法回退强调色)
            /// </summary>
            private Color PaletteColor(int index, Color fallback)
            {
                if (_theme.CategoryPalette.Count > index && ThemeManager.TryParseColor(_theme.CategoryPalette[index], out Color color))
                {
                    return color;
                }
                return fallback;
            }

            /// <summary>
            /// 10px 身份色点
            /// </summary>
            private static void DrawIdentityDot(Graphics g, int x, int y, Color color)
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, x, y, 10, 10);
                }
            }

            /// <summary>
            /// 边框：选中=辉光 2px,悬停=辉光 1.5px,常态=主题边框令牌 1px
            /// </summary>
            private Pen BuildBorderPen(Color glowColor)
            {
                if (_selected)
                {
                    return new Pen(glowColor, 2F);
                }
                if (_hover)
                {
                    return new Pen(glowColor, 1.5F);
                }
                return new Pen(ThemeManager.GetColor("border"), 1F);
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
