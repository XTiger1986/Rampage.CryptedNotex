using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal.Themes
{
    /// <summary>
    /// 把当前主题的金属纹理、斜切光边和徽记画到按钮与面板上。事件只订阅一次，每次重绘读取当前主题。
    /// </summary>
    internal static class ChromePainter
    {
        private static readonly ConditionalWeakTable<Control, object> _hooks = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Button, ButtonState> _buttonStates = new ConditionalWeakTable<Button, ButtonState>();

        /// <summary>
        /// 按钮按下态(悬停态无状态,绘制时按光标位置现场判定)
        /// </summary>
        private sealed class ButtonState
        {
            /// <summary>
            /// 鼠标按下中
            /// </summary>
            public bool Pressed;
        }

        /// <summary>
        /// 列表行悬停色：选中色的半透明洗版,铺在行底色上,金属与扁平主题通用
        /// </summary>
        public static Color RowHoverColor()
        {
            return Color.FromArgb(64, ThemeManager.GetColor("selectionBack"));
        }

        /// <summary>
        /// 按钮是否处于悬停(无状态判定,绘制期按光标与客户区的关系得出)
        /// </summary>
        public static bool IsButtonHover(Button button)
        {
            return button.Enabled && button.ClientRectangle.Contains(button.PointToClient(Cursor.Position));
        }

        /// <summary>
        /// 当前主题是否启用金属质感(metal 不是 none)
        /// </summary>
        public static bool UsesChrome
        {
            get
            {
                ThemeChrome? chrome = ThemeManager.Current.Chrome;
                return chrome != null
                    && !string.IsNullOrWhiteSpace(chrome.Metal)
                    && !string.Equals(chrome.Metal, "none", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 当前主题是否有可用壁纸
        /// </summary>
        public static bool HasWallpaper
        {
            get
            {
                return ChromeAssets.GetWallpaper(ThemeManager.Current.Chrome?.Wallpaper) != null;
            }
        }

        /// <summary>
        /// 父控件是否铺了金属(子标签需要让出底色)
        /// </summary>
        public static bool HostsMetal(Control? control)
        {
            return UsesChrome && control != null && IsSurfaceTag(control.Tag as string);
        }

        /// <summary>
        /// 遍历控件树：补订阅绘制，并按当前主题刷新壁纸与金属底图
        /// </summary>
        /// <param name="root">根控件</param>
        public static void RefreshTree(Control root)
        {
            if (root == null)
            {
                return;
            }

            RefreshOne(root);
            foreach (Control child in root.Controls)
            {
                RefreshTree(child);
            }
        }

        /// <summary>
        /// 右键菜单使用当前主题的底色与文字色
        /// </summary>
        /// <param name="menu">菜单</param>
        public static void StyleMenu(ContextMenuStrip menu)
        {
            menu.Renderer = new ChromeMenuRenderer();
            Color back = ThemeManager.GetColor("panelAltBack");
            Color text = ThemeManager.GetColor("text");
            menu.BackColor = back;
            menu.ForeColor = text;
            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = back;
                item.ForeColor = text;
            }
        }

        /// <summary>
        /// 金属近似纯色(标签底，避免透明标签画出壁纸)
        /// </summary>
        public static Color MetalSolidColor()
        {
            string? metal = ThemeManager.Current.Chrome?.Metal;
            if (string.Equals(metal, "black", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(28, 32, 38);
            }
            if (string.Equals(metal, "silver", StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(214, 220, 226);
            }
            return ThemeManager.GetColor("panelBack");
        }

        /// <summary>
        /// 单个控件刷新底图并保证绘制钩子只挂一次
        /// </summary>
        private static void RefreshOne(Control control)
        {
            EnsureHook(control);
            string? tag = control.Tag as string;

            if (control is Form form)
            {
                if (tag == "theme:wallpaper" && HasWallpaper)
                {
                    form.BackgroundImage = ChromeAssets.GetWallpaper(ThemeManager.Current.Chrome?.Wallpaper);
                    form.BackgroundImageLayout = ImageLayout.Stretch;
                }
                else
                {
                    form.BackgroundImage = null;
                }
            }

            if (tag == "theme:inset")
            {
                control.BackgroundImage = null;
                control.BackColor = HasWallpaper ? Color.Transparent : ThemeManager.GetColor("windowBack");
            }
            else if (IsSurfaceTag(tag) && UsesChrome)
            {
                control.BackgroundImage = ChromeAssets.Metal(ThemeManager.Current.Chrome?.Metal);
                control.BackgroundImageLayout = ImageLayout.Tile;
                control.BackColor = MetalSolidColor();
            }
            else if (IsSurfaceTag(tag))
            {
                control.BackgroundImage = null;
            }

            if (control is Button button)
            {
                Color fill = ButtonFill(button);
                if (UsesChrome)
                {
                    // 金属模式由 OnButtonPaint 全权绘制,FlatAppearance 与底色保持一致避免系统态闪边
                    button.FlatAppearance.MouseOverBackColor = fill;
                    button.FlatAppearance.MouseDownBackColor = fill;
                    button.BackColor = fill;
                    button.ForeColor = ButtonFore(button);
                }
                else
                {
                    // 扁平模式：悬停提亮、按下压暗,运行时派生,不新增主题令牌
                    button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(fill, 0.18f);
                    button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(fill, 0.12f);
                }
            }

            control.Invalidate();
        }

        /// <summary>
        /// 首次遇到控件时订阅自绘
        /// </summary>
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
                // 悬停/按下反馈需要重绘时机：进入/离开/按下/抬起都触发一次失效
                ButtonState state = _buttonStates.GetValue(button, _ => new ButtonState());
                button.MouseEnter += (sender, e) => button.Invalidate();
                button.MouseLeave += (sender, e) =>
                {
                    state.Pressed = false;
                    button.Invalidate();
                };
                button.MouseDown += (sender, e) =>
                {
                    state.Pressed = true;
                    button.Invalidate();
                };
                button.MouseUp += (sender, e) =>
                {
                    state.Pressed = false;
                    button.Invalidate();
                };
            }
            else if (IsSurfaceTag(control.Tag as string))
            {
                EnableTransparent(control);
                control.Paint += OnSurfacePaint;
            }

            // 暗色主题下把系统滚动条/下拉切到 Windows 暗色资源(DarkMode_Explorer),浅色切回默认
            if (control is ListView || control is ListBox || control is TextBox || control is TreeView || control is ComboBox)
            {
                control.HandleCreated += (sender, e) => ApplyScrollbarTheme(control);
                if (control.IsHandleCreated)
                {
                    ApplyScrollbarTheme(control);
                }
            }

            // 输入框聚焦辉光:聚焦时底色向强调色轻微混合,失焦还原
            if (control is TextBox input)
            {
                input.GotFocus += (sender, e) => input.BackColor = InputBackColor(true);
                input.LostFocus += (sender, e) => input.BackColor = InputBackColor(false);
            }
        }

        /// <summary>
        /// 滚动条/下拉的系统主题(暗色主题用 DarkMode_Explorer 资源;失败静默保持系统默认)
        /// </summary>
        private static void ApplyScrollbarTheme(Control control)
        {
            try
            {
                SetWindowTheme(control.Handle, ThemeManager.Current.DarkTitleBar ? "DarkMode_Explorer" : string.Empty, null);
            }
            catch
            {
                // 旧系统无 uxtheme 支持,忽略
            }
        }

        /// <summary>
        /// 输入框底色:聚焦时向强调色混合 28%,给出可见的焦点辉光
        /// </summary>
        public static Color InputBackColor(bool focused)
        {
            Color back = ThemeManager.GetColor("inputBack");
            if (!focused)
            {
                return back;
            }
            Color accent = ThemeManager.GetColor("accent");
            return Color.FromArgb(
                back.R + (accent.R - back.R) * 28 / 100,
                back.G + (accent.G - back.G) * 28 / 100,
                back.B + (accent.B - back.B) * 28 / 100);
        }

        /// <summary>
        /// 允许子标签透明，从而露出平铺的金属纹理
        /// </summary>
        private static void EnableTransparent(Control control)
        {
            typeof(Control).InvokeMember(
                "SetStyle",
                System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                control,
                new object[] { ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true });
        }

        /// <summary>
        /// 金属/强调/危险按钮盖住系统按钮外观
        /// </summary>
        private static void OnButtonPaint(object? sender, PaintEventArgs e)
        {
            if (!UsesChrome || sender is not Button button)
            {
                return;
            }

            Graphics g = e.Graphics;
            Rectangle rect = button.ClientRectangle;
            if (rect.Width <= 1 || rect.Height <= 1)
            {
                return;
            }

            string? tag = button.Tag as string;
            bool accent = tag == "theme:accent" && button.Enabled;
            bool danger = tag == "theme:danger" && button.Enabled;
            bool hover = IsButtonHover(button);
            ButtonState state = _buttonStates.GetValue(button, _ => new ButtonState());
            bool pressed = state.Pressed && hover;
            Color fill = ButtonFill(button);
            if (pressed)
            {
                fill = ControlPaint.Dark(fill, 0.08f);
            }
            else if (hover && (accent || danger))
            {
                fill = ControlPaint.Light(fill, 0.08f);
            }
            Color fore = ButtonFore(button);
            Color glow = GlowColor();

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, rect);
            }
            if (!accent && !danger)
            {
                Image? metal = ChromeAssets.Metal(ThemeManager.Current.Chrome?.Metal);
                if (metal != null)
                {
                    using (TextureBrush texture = new TextureBrush(metal, WrapMode.Tile))
                    {
                        g.FillRectangle(texture, rect);
                    }
                }
            }

            // 光影语言：悬停=顶部高光提亮、普通按钮边缘点亮为主题辉光；按下=高光熄灭下沉
            int highlightAlpha = pressed ? 0 : (hover ? 150 : 110);
            using (Pen highlight = new Pen(Color.FromArgb(accent || danger ? 80 : highlightAlpha, Color.White), highlightAlpha > 0 ? 1F : 0.01F))
            using (Pen shadow = new Pen(Color.FromArgb(pressed ? 120 : 90, Color.Black)))
            using (Pen edge = BuildEdgePen(glow, accent || danger, hover, pressed))
            {
                if (highlightAlpha > 0)
                {
                    g.DrawLine(highlight, 1, 1, rect.Width - 2, 1);
                }
                g.DrawLine(shadow, 1, rect.Height - 2, rect.Width - 2, rect.Height - 2);
                g.DrawRectangle(edge, 0, 0, rect.Width - 1, rect.Height - 1);
            }

            TextRenderer.DrawText(
                g,
                button.Text,
                button.Font,
                rect,
                button.Enabled ? fore : ThemeManager.GetColor("textSecondary"),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (button.Focused && button.Enabled)
            {
                Rectangle focus = rect;
                focus.Inflate(-3, -3);
                ControlPaint.DrawFocusRectangle(g, focus);
            }
        }

        /// <summary>
        /// 金属面板只补光边、徽记和品牌名，底图由 BackgroundImage 平铺
        /// </summary>
        private static void OnSurfacePaint(object? sender, PaintEventArgs e)
        {
            if (sender is not Control control)
            {
                return;
            }

            string? tag = control.Tag as string;
            Graphics g = e.Graphics;
            Rectangle rect = control.ClientRectangle;
            if (rect.Width <= 1 || rect.Height <= 1)
            {
                return;
            }

            if (!UsesChrome)
            {
                if (tag == "theme:card" || tag == "theme:cardEmblem")
                {
                    using (Pen pen = new Pen(ThemeManager.GetColor("border")))
                    {
                        g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
                    }
                }
                if (tag == "theme:cardEmblem")
                {
                    DrawEmblem(g, rect, 72, 16);
                }
                return;
            }

            Color glow = GlowColor();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen highlight = new Pen(Color.FromArgb(90, Color.White)))
            using (Pen glowPen = new Pen(glow, 2F))
            {
                g.DrawLine(highlight, 1, 1, rect.Width - 2, 1);
                if (tag == "theme:card" || tag == "theme:cardEmblem")
                {
                    g.DrawRectangle(glowPen, 1, 1, rect.Width - 3, rect.Height - 3);
                }
                else if (tag == "theme:sidebar")
                {
                    g.DrawLine(glowPen, rect.Width - 2, 10, rect.Width - 2, rect.Height - 10);
                }
                else
                {
                    g.DrawLine(glowPen, 10, rect.Height - 2, rect.Width - 10, rect.Height - 2);
                }
            }

            if (tag == "theme:cardEmblem")
            {
                DrawEmblem(g, rect, 84, 18);
            }
            else if (tag == "theme:brand")
            {
                DrawBrand(g, control, rect);
            }
        }

        /// <summary>
        /// 在矩形顶部居中画徽记
        /// </summary>
        private static void DrawEmblem(Graphics g, Rectangle rect, int size, int top)
        {
            Image? emblem = ChromeAssets.Emblem;
            if (emblem == null)
            {
                return;
            }

            int draw = Math.Min(size, rect.Width - 24);
            if (draw < 16)
            {
                return;
            }

            InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(emblem, new Rectangle((rect.Width - draw) / 2, top, draw, draw));
            g.InterpolationMode = old;
        }

        /// <summary>
        /// 侧栏品牌条：徽记 + 控件 Text
        /// </summary>
        private static void DrawBrand(Graphics g, Control control, Rectangle rect)
        {
            Image? emblem = ChromeAssets.Emblem;
            int size = Math.Min(46, rect.Height - 12);
            int y = Math.Max(6, (rect.Height - size) / 2);
            if (emblem != null && size >= 16)
            {
                InterpolationMode old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(emblem, new Rectangle(12, y, size, size));
                g.InterpolationMode = old;
            }

            int textX = emblem == null ? 12 : 12 + size + 8;
            Rectangle textRect = new Rectangle(textX, 0, Math.Max(0, rect.Width - textX - 8), rect.Height);
            using (Font font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold, GraphicsUnit.Point))
            {
                TextRenderer.DrawText(
                    g,
                    control.Text,
                    font,
                    textRect,
                    ThemeManager.GetColor("text"),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        /// <summary>
        /// 按钮边缘笔：强调/危险按钮常辉光,普通按钮悬停时点亮辉光,按下时辉光转暗
        /// </summary>
        private static Pen BuildEdgePen(Color glow, bool glowing, bool hover, bool pressed)
        {
            if (glowing)
            {
                if (pressed)
                {
                    return new Pen(Color.FromArgb(130, glow), 1F);
                }
                return hover ? new Pen(glow, 2F) : new Pen(glow, 1F);
            }
            if (hover && !pressed)
            {
                return new Pen(glow, 1.4F);
            }
            return new Pen(ThemeManager.GetColor("border"), 1F);
        }

        /// <summary>
        /// 按钮填充色
        /// </summary>
        private static Color ButtonFill(Button button)
        {
            string? tag = button.Tag as string;
            if (!button.Enabled)
            {
                return MetalSolidColor();
            }
            if (tag == "theme:accent")
            {
                return ThemeManager.GetColor("accent");
            }
            if (tag == "theme:danger")
            {
                return ThemeManager.GetColor("danger");
            }
            return MetalSolidColor();
        }

        /// <summary>
        /// 按钮文字色。危险按钮默认白字,主题可通过 dangerText 令牌覆盖。
        /// </summary>
        private static Color ButtonFore(Button button)
        {
            string? tag = button.Tag as string;
            if (tag == "theme:danger")
            {
                return ThemeManager.GetColor("dangerText");
            }
            if (tag == "theme:accent")
            {
                return ThemeManager.GetColor("accentText");
            }
            return ThemeManager.GetColor("text");
        }

        /// <summary>
        /// 当前主题发光色
        /// </summary>
        private static Color GlowColor()
        {
            ThemeChrome? chrome = ThemeManager.Current.Chrome;
            if (chrome != null && ThemeManager.TryParseColor(chrome.Glow, out Color glow))
            {
                return glow;
            }
            return ThemeManager.GetColor("accent");
        }

        /// <summary>
        /// 会铺金属底图的 Tag
        /// </summary>
        private static bool IsSurfaceTag(string? tag)
        {
            return tag == "theme:card"
                || tag == "theme:cardEmblem"
                || tag == "theme:chrome"
                || tag == "theme:brand"
                || tag == "theme:sidebar";
        }

        /// <summary>
        /// 打开控件双缓冲(受保护属性)
        /// </summary>
        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                control,
                new object[] { true });
        }

        /// <summary>
        /// 切换控件系统视觉主题(滚动条/下拉配色)
        /// </summary>
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    }

    /// <summary>
    /// 菜单配色表，跟当前主题走
    /// </summary>
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
