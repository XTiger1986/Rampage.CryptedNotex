using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using Rampage.Model;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 主题工作台与用户偏好。主题即时保存；其他偏好由保存按钮提交。
    /// </summary>
    public class SettingsForm : NotexFormBase
    {
        private readonly CryptoSession _session;
        private readonly UserSetting _setting;
        private FlowLayoutPanel _themeGallery = null!;
        private TableLayoutPanel _preferences = null!;
        private NumericUpDown _lockMinutesBox = null!;
        private ComboBox _fontFamilyBox = null!;
        private NumericUpDown _fontSizeBox = null!;
        private CheckBox _minimizeTrayBox = null!;
        private TextBox _displayNameBox = null!;
        private Label _statusLabel = null!;
        private Label _themeLabel = null!;
        private bool _sizingCards;

        public SettingsForm(IRampageApplication app, CryptoSession session, UserSetting setting) : base(app)
        {
            _session = session;
            _setting = setting;
            Text = "外观与偏好";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1080, 760);
            MinimumSize = new Size(820, 540);
            BuildUi();
            ThemeManager.CurrentChanged += OnThemeChanged;
            Disposed += (sender, e) => ThemeManager.CurrentChanged -= OnThemeChanged;
        }

        private void BuildUi()
        {
            Panel heading = new Panel { Dock = DockStyle.Top, Height = 100, Padding = new Padding(28, 20, 28, 12) };
            Label title = new Label { Text = "让记录，成为你的风格。", Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold), Dock = DockStyle.Top, AutoSize = true };
            Label intro = new Label { Text = "11 种独立视觉语言。点击主题即可应用并保存。", Tag = "theme:muted", Dock = DockStyle.Bottom, AutoSize = true };
            heading.Controls.Add(title);
            heading.Controls.Add(intro);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(28, 16, 28, 16), Tag = "theme:chrome" };
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            Button save = UiFactory.Button("保存偏好", "theme:accent");
            save.MinimumSize = new Size(120, 38);
            save.Click += (sender, e) => Save();
            Button close = UiFactory.Button("完成");
            close.MinimumSize = new Size(88, 38);
            close.Click += (sender, e) => Close();
            actions.Controls.Add(save);
            actions.Controls.Add(close);
            _statusLabel = new Label { Text = "主题会即时保存，其他更改请点击「保存偏好」。", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "theme:muted", AutoEllipsis = true };
            footer.Controls.Add(_statusLabel);
            footer.Controls.Add(actions);
            AcceptButton = save;
            CancelButton = close;

            TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(28, 0, 28, 0) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Panel galleryHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 20, 0) };
            _themeLabel = new Label { Dock = DockStyle.Top, Height = 38, Text = "界面主题", Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold) };
            _themeGallery = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 0, 0, 8) };
            _themeGallery.SizeChanged += (sender, e) => SizeThemeCards();
            galleryHost.Controls.Add(_themeGallery);
            galleryHost.Controls.Add(_themeLabel);
            Panel preferencesHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Tag = "theme:card", Margin = Padding.Empty };
            _preferences = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(20, 16, 20, 24), Margin = Padding.Empty };
            _preferences.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            preferencesHost.Controls.Add(_preferences);
            preferencesHost.SizeChanged += (sender, e) =>
            {
                int width = Math.Max(120, preferencesHost.ClientSize.Width - preferencesHost.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                _preferences.Width = width;
                foreach (Label label in _preferences.Controls.OfType<Label>())
                {
                    label.MaximumSize = new Size(Math.Max(80, width - _preferences.Padding.Horizontal), 0);
                }
            };
            body.Controls.Add(galleryHost, 0, 0);
            body.Controls.Add(preferencesHost, 1, 0);
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(heading);

            AddSection("编辑器", "纯文本与 Markdown 的阅读设置");
            _fontFamilyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, AccessibleName = "编辑器字体" };
            foreach (string family in new[] { "Consolas", "Cascadia Mono", "Cascadia Code", "Courier New", "Microsoft YaHei UI", "SimSun" })
            {
                _fontFamilyBox.Items.Add(family);
            }
            if (!_fontFamilyBox.Items.Contains(_setting.EditorFontFamily))
            {
                _fontFamilyBox.Items.Add(_setting.EditorFontFamily);
            }
            _fontFamilyBox.SelectedItem = _setting.EditorFontFamily;
            AddField("字体", _fontFamilyBox);
            _fontSizeBox = new NumericUpDown { Minimum = 8, Maximum = 28, DecimalPlaces = 1, Increment = 0.5M, Value = (decimal)Math.Clamp(_setting.EditorFontSize, 8, 28), Dock = DockStyle.Top, AccessibleName = "编辑器字号" };
            AddField("字号 / pt", _fontSizeBox);

            AddSection("账户与安全", "让私人档案保持私密");
            _lockMinutesBox = new NumericUpDown { Minimum = 0, Maximum = 240, Value = Math.Clamp(_setting.AutoLockMinutes, 0, 240), Dock = DockStyle.Top, AccessibleName = "自动锁定分钟数" };
            AddField("自动锁定 / 分钟", _lockMinutesBox, "闲置后返回登录页；0 表示不自动锁定。");
            _minimizeTrayBox = new CheckBox { Text = "最小化到系统托盘", AutoSize = true, Checked = _setting.MinimizeToTray == 1, Margin = new Padding(0, 8, 0, 8) };
            AddRow(_minimizeTrayBox);
            AddHelp("双击托盘图标恢复窗口，驻留期间仍会自动锁定。");
            _displayNameBox = new TextBox { Text = _session.User?.DisplayName ?? string.Empty, Dock = DockStyle.Top, AccessibleName = "显示名" };
            AddField("显示名", _displayNameBox);
            Button changePassword = UiFactory.Button("修改密码…");
            changePassword.Dock = DockStyle.Top;
            changePassword.Margin = new Padding(0, 8, 0, 8);
            changePassword.Click += (sender, e) =>
            {
                using (ChangePasswordForm form = new ChangePasswordForm(App, _session))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        ShowToast("密码已更新，笔记和附件已重新加密。", ToastKind.Success);
                    }
                }
            };
            AddRow(changePassword);
            AddHelp("修改密码时会重新加密全部笔记与附件。");
        }

        private void AddSection(string title, string subtitle)
        {
            AddRow(new Label { Text = title, Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, _preferences.RowCount == 0 ? 0 : 20, 0, 8) });
            AddHelp(subtitle);
        }

        private void AddField(string title, Control control, string? help = null)
        {
            AddRow(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 14, 0, 7) });
            control.Margin = new Padding(0, 0, 0, 4);
            AddRow(control);
            if (help != null)
            {
                AddHelp(help);
            }
        }

        private void AddHelp(string text)
        {
            AddRow(new Label { Text = text, Tag = "theme:muted", AutoSize = true, MaximumSize = new Size(248, 0), Margin = new Padding(0, 2, 0, 4) });
        }

        private void AddRow(Control control)
        {
            int row = _preferences.RowCount++;
            _preferences.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _preferences.Controls.Add(control, 0, row);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildThemeGallery();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            Size available = new Size(Math.Max(1, workArea.Width - Scale(32)), Math.Max(1, workArea.Height - Scale(48)));
            MinimumSize = new Size(Math.Min(MinimumSize.Width, available.Width), Math.Min(MinimumSize.Height, available.Height));
            Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
            SizeThemeCards();
        }

        private void OnThemeChanged(Theme theme)
        {
            if (IsDisposed)
            {
                return;
            }
            ThemeManager.Apply(this);
            foreach (ThemeCard card in _themeGallery.Controls.OfType<ThemeCard>())
            {
                card.Selected = card.ThemeId == theme.Id;
            }
            _themeLabel.Text = "界面主题  /  " + theme.Name;
        }

        private void BuildThemeGallery()
        {
            _themeGallery.SuspendLayout();
            foreach (Theme theme in ThemeManager.AvailableThemes)
            {
                ThemeCard card = new ThemeCard(theme) { Selected = theme.Id == ThemeManager.Current.Id };
                card.CardClicked += (sender, themeId) => SelectTheme(themeId);
                _themeGallery.Controls.Add(card);
            }
            _themeGallery.ResumeLayout();
            _themeLabel.Text = "界面主题  /  " + ThemeManager.Current.Name;
            SizeThemeCards();
        }

        private void SizeThemeCards()
        {
            if (_sizingCards || _themeGallery == null || _themeGallery.ClientSize.Width < 80)
            {
                return;
            }
            _sizingCards = true;
            try
            {
                int gap = Scale(12);
                int available = _themeGallery.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Scale(4);
                int columns = Math.Clamp(available / Scale(202), 1, 3);
                int width = Math.Max(Scale(160), (available - gap * (columns - 1)) / columns);
                int index = 0;
                foreach (ThemeCard card in _themeGallery.Controls.OfType<ThemeCard>())
                {
                    card.Size = new Size(width, Scale(194));
                    card.Margin = new Padding(0, 0, ++index % columns == 0 ? 0 : gap, gap);
                }
            }
            finally
            {
                _sizingCards = false;
            }
        }

        private int Scale(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96D);
        }

        private void SelectTheme(string themeId)
        {
            if (_setting.ThemeId == themeId && ThemeManager.Current.Id == themeId)
            {
                return;
            }
            string previousTheme = ThemeManager.Current.Id;
            try
            {
                UserSetting draft = CopySetting();
                draft.ThemeId = themeId;
                ThemeManager.ApplyTheme(themeId);
                R saved = GetService<IUserSettingService>().SaveUserSetting(draft);
                if (!saved.IsSuccess)
                {
                    ThemeManager.ApplyTheme(previousTheme);
                    ShowToast("主题未保存：" + saved.Message, ToastKind.Error);
                    return;
                }
                _setting.Id = draft.Id;
                _setting.ThemeId = themeId;
                _setting.UpdateTime = draft.UpdateTime;
                _statusLabel.Text = "已切换为「" + ThemeManager.Current.Name + "」，主题偏好已保存。";
            }
            catch (Exception ex)
            {
                ThemeManager.ApplyTheme(previousTheme);
                ShowToast("主题未保存：" + ex.Message, ToastKind.Error);
            }
        }

        private UserSetting CopySetting()
        {
            return new UserSetting
            {
                Id = _setting.Id,
                UserId = _setting.UserId,
                ThemeId = _setting.ThemeId,
                AutoLockMinutes = _setting.AutoLockMinutes,
                MinimizeToTray = _setting.MinimizeToTray,
                EditorFontFamily = _setting.EditorFontFamily,
                EditorFontSize = _setting.EditorFontSize,
                LastNoteId = _setting.LastNoteId,
                CreateTime = _setting.CreateTime,
                UpdateTime = _setting.UpdateTime
            };
        }

        private void Save()
        {
            try
            {
                UserSetting draft = CopySetting();
                draft.AutoLockMinutes = (int)_lockMinutesBox.Value;
                draft.MinimizeToTray = _minimizeTrayBox.Checked ? 1 : 0;
                draft.EditorFontFamily = (string?)_fontFamilyBox.SelectedItem ?? _setting.EditorFontFamily;
                draft.EditorFontSize = (double)_fontSizeBox.Value;
                R saved = GetService<IUserSettingService>().SaveUserSetting(draft);
                if (!saved.IsSuccess)
                {
                    _statusLabel.Text = "保存失败：" + saved.Message;
                    ShowToast(_statusLabel.Text, ToastKind.Error);
                    return;
                }
                _setting.Id = draft.Id;
                _setting.AutoLockMinutes = draft.AutoLockMinutes;
                _setting.MinimizeToTray = draft.MinimizeToTray;
                _setting.EditorFontFamily = draft.EditorFontFamily;
                _setting.EditorFontSize = draft.EditorFontSize;
                _setting.UpdateTime = draft.UpdateTime;
                string status = "偏好已保存";
                ToastKind kind = ToastKind.Success;
                string displayName = _displayNameBox.Text.Trim();
                if (displayName.Length > 0 && displayName != _session.User?.DisplayName)
                {
                    R rename = GetService<IUserService>().UpdateDisplayName(_session.User!.Id, displayName);
                    if (rename.IsSuccess)
                    {
                        _session.User.DisplayName = displayName;
                        status += "，显示名已更新";
                    }
                    else
                    {
                        status += "；显示名未更新：" + rename.Message;
                        kind = ToastKind.Error;
                    }
                }
                _statusLabel.Text = status;
                ShowToast(status, kind);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "保存失败：" + ex.Message;
                ShowToast(_statusLabel.Text, ToastKind.Error);
            }
        }

        /// <summary>
        /// 真实三栏工作区微缩预览，支持 Tab、Enter、空格及辅助技术。
        /// </summary>
        private sealed class ThemeCard : Control
        {
            private readonly Theme _theme;
            private bool _selected;
            private bool _hover;
            public event EventHandler<string>? CardClicked;
            public string ThemeId => _theme.Id;

            public ThemeCard(Theme theme)
            {
                _theme = theme;
                Cursor = Cursors.Hand;
                TabStop = true;
                AccessibleRole = AccessibleRole.RadioButton;
                AccessibleName = theme.Name + "主题";
                AccessibleDescription = theme.Description;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
                MouseEnter += (sender, e) => { _hover = true; Invalidate(); };
                MouseLeave += (sender, e) => { _hover = false; Invalidate(); };
                GotFocus += (sender, e) => Invalidate();
                LostFocus += (sender, e) => Invalidate();
            }

            public bool Selected
            {
                get => _selected;
                set
                {
                    _selected = value;
                    AccessibleName = _theme.Name + "主题" + (value ? "，已选择" : string.Empty);
                    Invalidate();
                }
            }

            private Color ColorFor(string key, string fallback)
            {
                return ThemeManager.TryParseColor(_theme.Colors.TryGetValue(key, out string? value) ? value : fallback, out Color color) ? color : Color.White;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                float scale = DeviceDpi / 96F;
                int pad = (int)(12 * scale);
                Color back = ColorFor("panelBack", "#FFFFFF");
                Color accent = ColorFor("accent", "#007ACC");
                Color text = ColorFor("text", "#202226");
                Color muted = ColorFor("textSecondary", "#60646A");
                Color border = ColorFor("border", "#CCD0D8");
                using (SolidBrush brush = new SolidBrush(back))
                {
                    g.FillRectangle(brush, ClientRectangle);
                }
                Rectangle preview = new Rectangle(pad, pad, Width - 2 * pad, (int)(104 * scale));
                Fill(g, preview, ColorFor("editorBack", "#FFFFFF"));
                Rectangle sidebar = new Rectangle(preview.X, preview.Y, (int)(preview.Width * 0.25), preview.Height);
                ThemeArtwork.Draw(g, sidebar, _theme);
                Rectangle list = new Rectangle(sidebar.Right, preview.Y, (int)(preview.Width * 0.29), preview.Height);
                Fill(g, list, ColorFor("panelAltBack", "#F1F3F5"));
                Fill(g, new Rectangle(list.X + 4, list.Y + 17, list.Width - 8, 22), ColorFor("selectionBack", "#DFEAF5"));
                using (Pen pen = new Pen(border, Math.Max(1F, scale)))
                using (Pen accentPen = new Pen(accent, Math.Max(2F, scale * 2)))
                {
                    g.DrawRectangle(pen, preview);
                    for (int row = 0; row < 4; row++)
                    {
                        int lineY = list.Y + (int)((25 + row * 20) * scale);
                        g.DrawLine(pen, list.X + 8, lineY, list.Right - 8, lineY);
                    }
                    int editorX = list.Right + (int)(10 * scale);
                    int editorRight = preview.Right - (int)(10 * scale);
                    g.DrawLine(accentPen, editorX, preview.Y + (int)(22 * scale), editorRight - (int)(12 * scale), preview.Y + (int)(22 * scale));
                    for (int row = 0; row < 5; row++)
                    {
                        int lineY = preview.Y + (int)((42 + row * 9) * scale);
                        int right = row % 3 == 2 ? editorRight - (int)(12 * scale) : editorRight;
                        g.DrawLine(pen, editorX, lineY, right, lineY);
                    }
                }
                using (Font name = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold))
                using (Font description = new Font("Microsoft YaHei UI", 8.5F))
                {
                    int y = preview.Bottom + (int)(12 * scale);
                    TextRenderer.DrawText(g, _theme.Name, name, new Rectangle(pad, y, Width - 2 * pad - (int)(48 * scale), (int)(24 * scale)), text, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    if (_selected)
                    {
                        TextRenderer.DrawText(g, "已选", description, new Rectangle(Width - pad - (int)(40 * scale), y + (int)(2 * scale), (int)(40 * scale), (int)(24 * scale)), accent, TextFormatFlags.Right | TextFormatFlags.NoPrefix);
                    }
                    TextRenderer.DrawText(g, _theme.Description ?? ThemeArtwork.GetStyleName(_theme), description, new Rectangle(pad, y + (int)(29 * scale), Width - 2 * pad, (int)(28 * scale)), muted, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }
                using (Pen outline = new Pen(_selected || _hover || Focused ? accent : border, _selected ? 2 * scale : scale))
                {
                    g.DrawRectangle(outline, 1, 1, Width - 3, Height - 3);
                }
                if (Focused)
                {
                    ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -4, -4), text, back);
                }
            }

            private static void Fill(Graphics graphics, Rectangle bounds, Color color)
            {
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return;
                }
                using (SolidBrush brush = new SolidBrush(color))
                {
                    graphics.FillRectangle(brush, bounds);
                }
            }

            protected override void OnClick(EventArgs e)
            {
                Focus();
                base.OnClick(e);
                CardClicked?.Invoke(this, _theme.Id);
            }

            protected override bool IsInputKey(Keys keyData)
            {
                Keys key = keyData & Keys.KeyCode;
                return key == Keys.Enter || key == Keys.Space || base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    OnClick(EventArgs.Empty);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                base.OnKeyDown(e);
            }
        }
    }
}
