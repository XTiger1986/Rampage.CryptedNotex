using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal.Themes
{
    /// <summary>
    /// 主题控件绘制与交互反馈。绘制时读取当前主题，事件仅订阅一次。
    /// </summary>
    internal static class ChromePainter
    {
        private static readonly ConditionalWeakTable<Control, object> _hooks = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Button, ButtonState> _buttonStates = new ConditionalWeakTable<Button, ButtonState>();

        private sealed class ButtonState
        {
            public bool Pressed;
            public bool Active;
        }

        /// <summary>
        /// 设置工具按钮的持续选中状态，与鼠标按压状态分开存储，切换主题时仍由当前令牌绘制。
        /// </summary>
        public static void SetButtonActive(Button button, bool active)
        {
            _buttonStates.GetValue(button, _ => new ButtonState()).Active = active;
            Color fill = ButtonFill(button);
            button.BackColor = fill;
            button.FlatAppearance.MouseOverBackColor = fill;
            button.FlatAppearance.MouseDownBackColor = fill;
            button.Invalidate();
        }

        public static Color RowHoverColor()
        {
            return Color.FromArgb(72, ThemeManager.GetColor("selectionBack"));
        }

        public static bool IsButtonHover(Button button)
        {
            return button.Enabled && button.ClientRectangle.Contains(button.PointToClient(Cursor.Position));
        }

        /// <summary>
        /// 保留旧自定义主题的 chrome 配置；新版内置主题使用纯色界面和独立身份图形。
        /// </summary>
        public static bool UsesChrome
        {
            get
            {
                string? metal = ThemeManager.Current.Chrome?.Metal;
                return !string.IsNullOrWhiteSpace(metal) && !string.Equals(metal, "none", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool HasWallpaper
        {
            get { return ChromeAssets.GetWallpaper(ThemeManager.Current.Chrome?.Wallpaper) != null; }
        }

        public static bool HostsMetal(Control? control)
        {
            return UsesChrome && control != null && IsSurfaceTag(control.Tag as string);
        }

        public static Color MetalSolidColor()
        {
            if (UsesChrome)
            {
                return string.Equals(ThemeManager.Current.Chrome?.Metal, "silver", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(214, 220, 226) : Color.FromArgb(28, 32, 38);
            }
            return ThemeManager.GetColor("panelBack");
        }

        public static void RefreshTree(Control root)
        {
            RefreshOne(root);
            foreach (Control child in root.Controls)
            {
                RefreshTree(child);
            }
        }

        public static void StyleMenu(ContextMenuStrip menu)
        {
            menu.Renderer = new ChromeMenuRenderer();
            menu.BackColor = ThemeManager.GetColor("panelAltBack");
            menu.ForeColor = ThemeManager.GetColor("text");
            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = menu.BackColor;
                item.ForeColor = menu.ForeColor;
            }
        }

        private static void RefreshOne(Control control)
        {
            EnsureHook(control);
            string? tag = control.Tag as string;
            if (control is Form form)
            {
                form.BackgroundImage = tag == "theme:wallpaper" && HasWallpaper
                    ? ChromeAssets.GetWallpaper(ThemeManager.Current.Chrome?.Wallpaper) : null;
                form.BackgroundImageLayout = ImageLayout.Stretch;
            }
            else if (tag == "theme:inset")
            {
                control.BackgroundImage = null;
                control.BackColor = HasWallpaper ? Color.Transparent : ThemeManager.GetColor("windowBack");
            }
            else if (IsSurfaceTag(tag))
            {
                control.BackgroundImage = UsesChrome ? ChromeAssets.Metal(ThemeManager.Current.Chrome?.Metal) : null;
                control.BackgroundImageLayout = ImageLayout.Tile;
                if (UsesChrome)
                {
                    control.BackColor = MetalSolidColor();
                }
            }

            if (control is Button button)
            {
                Color fill = ButtonFill(button);
                button.BackColor = fill;
                button.ForeColor = ButtonFore(button);
                button.FlatAppearance.MouseOverBackColor = fill;
                button.FlatAppearance.MouseDownBackColor = fill;
            }

            if (control.IsHandleCreated && HasNativeScrollbars(control))
            {
                ApplyScrollbarTheme(control);
            }
            control.Invalidate();
        }

        private static void EnsureHook(Control control)
        {
            if (_hooks.TryGetValue(control, out _))
            {
                return;
            }
            _hooks.Add(control, new object());
            EnableDoubleBuffer(control);
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.Paint += OnButtonPaint;
                ButtonState state = _buttonStates.GetValue(button, _ => new ButtonState());
                button.MouseEnter += (sender, e) => button.Invalidate();
                button.MouseLeave += (sender, e) =>
                {
                    state.Pressed = false;
                    button.Invalidate();
                };
                button.MouseDown += (sender, e) =>
                {
                    state.Pressed = e.Button == MouseButtons.Left;
                    button.Invalidate();
                };
                button.MouseUp += (sender, e) =>
                {
                    state.Pressed = false;
                    button.Invalidate();
                };
                button.KeyDown += (sender, e) =>
                {
                    if (e.KeyCode == Keys.Space)
                    {
                        state.Pressed = true;
                        button.Invalidate();
                    }
                };
                button.KeyUp += (sender, e) =>
                {
                    state.Pressed = false;
                    button.Invalidate();
                };
                button.GotFocus += (sender, e) => button.Invalidate();
                button.LostFocus += (sender, e) =>
                {
                    state.Pressed = false;
                    button.Invalidate();
                };
                button.EnabledChanged += (sender, e) => button.Invalidate();
            }
            else if (IsSurfaceTag(control.Tag as string))
            {
                control.Paint += OnSurfacePaint;
            }

            if (HasNativeScrollbars(control))
            {
                control.HandleCreated += (sender, e) => ApplyScrollbarTheme(control);
            }

            if (control is TextBox input)
            {
                input.GotFocus += (sender, e) => RefreshInputFocus(input, true);
                input.LostFocus += (sender, e) => RefreshInputFocus(input, false);
            }
        }

        private static void RefreshInputFocus(TextBox input, bool focused)
        {
            string? tag = input.Tag as string;
            if (tag != "theme:editor" && tag != "theme:editorTitle" && tag != "theme:document")
            {
                input.BackColor = InputBackColor(focused);
            }
        }

        private static bool HasNativeScrollbars(Control control)
        {
            return control is ListView || control is ListBox || control is TextBoxBase
                || control is TreeView || control is ComboBox || control is NumericUpDown;
        }

        private static void ApplyScrollbarTheme(Control control)
        {
            if (!control.IsHandleCreated)
            {
                return;
            }
            try
            {
                SetWindowTheme(control.Handle, ThemeManager.Current.DarkTitleBar ? "DarkMode_Explorer" : string.Empty, null);
            }
            catch
            {
                // 旧版 Windows 可继续使用系统滚动条。
            }
        }

        public static Color InputBackColor(bool focused)
        {
            Color back = ThemeManager.GetColor("inputBack");
            return focused ? Blend(back, ThemeManager.GetColor("accent"), 0.08F) : back;
        }

        private static void OnButtonPaint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button || button.Width < 3 || button.Height < 3)
            {
                return;
            }

            Graphics g = e.Graphics;
            Rectangle rect = button.ClientRectangle;
            string? tag = button.Tag as string;
            bool primary = tag == "theme:accent" || tag == "theme:danger";
            bool tool = tag == "theme:tool";
            bool hover = IsButtonHover(button);
            ButtonState state = _buttonStates.GetValue(button, _ => new ButtonState());
            bool pressed = state.Pressed && button.Enabled;
            bool active = state.Active && button.Enabled;
            Color parent = button.Parent?.BackColor ?? ThemeManager.GetColor("panelBack");
            Color fill = ButtonFill(button);
            Color fore = button.Enabled ? ButtonFore(button) : ThemeManager.GetColor("textSecondary");
            if (button.Enabled && (hover || pressed))
            {
                fill = Blend(fill, primary ? fore : ThemeManager.GetColor("accent"), pressed ? 0.15F : 0.07F);
            }

            using (SolidBrush clear = new SolidBrush(parent))
            {
                g.FillRectangle(clear, rect);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF outline = new RectangleF(0.75F, 0.75F, rect.Width - 1.5F, rect.Height - 1.5F);
            using (GraphicsPath shape = ButtonPath(outline))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillPath(brush, shape);
                if (!primary && (!tool || hover || button.Focused || active))
                {
                    Color edge = hover || button.Focused || active ? ThemeManager.GetColor("accent") : ThemeManager.GetColor("border");
                    using (Pen pen = new Pen(edge, ThemeManager.Current.Id == "contrast" ? 2F : 1F))
                    {
                        g.DrawPath(pen, shape);
                    }
                }
                if (button.Focused && button.Enabled)
                {
                    RectangleF focus = outline;
                    focus.Inflate(-3F, -3F);
                    using (GraphicsPath focusPath = ButtonPath(focus))
                    using (Pen focusPen = new Pen(primary ? fore : ThemeManager.GetColor("accent"), 1.5F))
                    {
                        focusPen.DashStyle = DashStyle.Dot;
                        g.DrawPath(focusPen, focusPath);
                    }
                }
            }

            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            ContentAlignment align = button.TextAlign;
            flags |= align == ContentAlignment.MiddleLeft || align == ContentAlignment.TopLeft || align == ContentAlignment.BottomLeft
                ? TextFormatFlags.Left : align == ContentAlignment.MiddleRight || align == ContentAlignment.TopRight || align == ContentAlignment.BottomRight
                ? TextFormatFlags.Right : TextFormatFlags.HorizontalCenter;
            Rectangle textRect = Rectangle.Inflate(rect, -8, -2);
            if (pressed)
            {
                textRect.Offset(0, 1);
            }
            TextRenderer.DrawText(g, button.Text, button.Font, textRect, fore, flags);
        }

        private static GraphicsPath ButtonPath(RectangleF rect)
        {
            GraphicsPath path = new GraphicsPath();
            string shape = ThemeManager.Current.Appearance?.ButtonShape ?? "rounded";
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return path;
            }
            if (shape == "cut")
            {
                float cut = Math.Min(7F, Math.Min(rect.Width, rect.Height) / 4F);
                path.AddPolygon(new[]
                {
                    new PointF(rect.Left + cut, rect.Top), new PointF(rect.Right, rect.Top),
                    new PointF(rect.Right, rect.Bottom - cut), new PointF(rect.Right - cut, rect.Bottom),
                    new PointF(rect.Left, rect.Bottom), new PointF(rect.Left, rect.Top + cut)
                });
            }
            else if (shape == "square")
            {
                path.AddRectangle(rect);
            }
            else
            {
                float diameter = Math.Min(ThemeManager.Current.Id == "alienmorph" || ThemeManager.Current.Id == "violet" ? 18F : 10F, Math.Min(rect.Width, rect.Height));
                path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
                path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
                path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
            }
            return path;
        }

        private static void OnSurfacePaint(object? sender, PaintEventArgs e)
        {
            if (sender is not Control control || control.Width < 2 || control.Height < 2)
            {
                return;
            }
            string? tag = control.Tag as string;
            Rectangle rect = control.ClientRectangle;
            using (Pen border = new Pen(ThemeManager.GetColor("border"), ThemeManager.Current.Id == "contrast" ? 2F : 1F))
            {
                if (tag == "theme:card" || tag == "theme:cardEmblem")
                {
                    e.Graphics.DrawRectangle(border, 0, 0, rect.Width - 1, rect.Height - 1);
                }
                else if (tag == "theme:sidebar")
                {
                    e.Graphics.DrawLine(border, rect.Width - 1, 0, rect.Width - 1, rect.Height);
                }
                else if (tag == "theme:chrome")
                {
                    e.Graphics.DrawLine(border, 0, 0, rect.Width, 0);
                }
            }
            if (tag == "theme:brand")
            {
                DrawBrand(e.Graphics, control, rect);
            }
            else if (tag == "theme:cardEmblem")
            {
                Image? emblem = ChromeAssets.Emblem;
                if (emblem != null)
                {
                    e.Graphics.DrawImage(emblem, new Rectangle((rect.Width - 68) / 2, 18, 68, 68));
                }
            }
        }

        private static void DrawBrand(Graphics g, Control control, Rectangle rect)
        {
            int size = Math.Min(36, rect.Height - 20);
            if (size > 8 && ChromeAssets.Emblem != null)
            {
                g.DrawImage(ChromeAssets.Emblem, new Rectangle(16, (rect.Height - size) / 2, size, size));
            }
            using (Font font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, control.Text, font,
                    new Rectangle(size + 28, 0, Math.Max(0, rect.Width - size - 40), rect.Height),
                    ThemeManager.GetColor("text"),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        private static Color ButtonFill(Button button)
        {
            if (!button.Enabled)
            {
                return ThemeManager.GetColor("panelBack");
            }
            string? tag = button.Tag as string;
            return tag switch
            {
                "theme:accent" => ThemeManager.GetColor("accent"),
                "theme:danger" => ThemeManager.GetColor("danger"),
                "theme:tool" => _buttonStates.GetValue(button, _ => new ButtonState()).Active
                    ? ThemeManager.GetColor("selectionBack")
                    : button.Parent?.BackColor ?? ThemeManager.GetColor("panelBack"),
                _ => ThemeManager.GetColor("panelAltBack")
            };
        }

        private static Color ButtonFore(Button button)
        {
            return (button.Tag as string) switch
            {
                "theme:accent" => ThemeManager.GetColor("accentText"),
                "theme:danger" => ThemeManager.GetColor("dangerText"),
                "theme:tool" => button.ForeColor,
                _ => ThemeManager.GetColor("text")
            };
        }

        private static Color Blend(Color back, Color fore, float amount)
        {
            return Color.FromArgb(
                (int)(back.R + (fore.R - back.R) * amount),
                (int)(back.G + (fore.G - back.G) * amount),
                (int)(back.B + (fore.B - back.B) * amount));
        }

        private static bool IsSurfaceTag(string? tag)
        {
            return tag == "theme:card" || tag == "theme:cardEmblem" || tag == "theme:chrome"
                || tag == "theme:brand" || tag == "theme:sidebar";
        }

        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    }

    internal sealed class ChromeMenuColorTable : ProfessionalColorTable
    {
        /// <summary>
        /// 下拉底
        /// </summary>
        public override Color ToolStripDropDownBackground
        {
            get { return ThemeManager.GetColor("panelAltBack"); }
        }

        /// <summary>
        /// 选中项底
        /// </summary>
        public override Color MenuItemSelected
        {
            get { return ThemeManager.GetColor("selectionBack"); }
        }

        /// <summary>
        /// 选中项边
        /// </summary>
        public override Color MenuItemBorder
        {
            get { return ThemeManager.GetColor("accent"); }
        }

        /// <summary>
        /// 菜单外边
        /// </summary>
        public override Color MenuBorder
        {
            get { return ThemeManager.GetColor("border"); }
        }

        /// <summary>
        /// 图标槽渐变起
        /// </summary>
        public override Color ImageMarginGradientBegin
        {
            get { return ThemeManager.GetColor("panelAltBack"); }
        }

        /// <summary>
        /// 图标槽渐变中
        /// </summary>
        public override Color ImageMarginGradientMiddle
        {
            get { return ThemeManager.GetColor("panelAltBack"); }
        }

        /// <summary>
        /// 图标槽渐变止
        /// </summary>
        public override Color ImageMarginGradientEnd
        {
            get { return ThemeManager.GetColor("panelAltBack"); }
        }

        /// <summary>
        /// 分隔线
        /// </summary>
        public override Color SeparatorDark
        {
            get { return ThemeManager.GetColor("border"); }
        }

        /// <summary>
        /// 分隔线亮边
        /// </summary>
        public override Color SeparatorLight
        {
            get { return ThemeManager.GetColor("panelAltBack"); }
        }
    }

    /// <summary>
    /// 菜单渲染器：文字色跟主题，而不是系统色
    /// </summary>
    internal sealed class ChromeMenuRenderer : ToolStripProfessionalRenderer
    {
        /// <summary>
        /// 构造渲染器
        /// </summary>
        public ChromeMenuRenderer() : base(new ChromeMenuColorTable())
        {
            RoundedEdges = false;
        }

        /// <summary>
        /// 用主题文字色画菜单项
        /// </summary>
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? ThemeManager.GetColor("text") : ThemeManager.GetColor("textSecondary");
            base.OnRenderItemText(e);
        }
    }
}
