using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal.Themes
{
    /// <summary>
    /// 把当前主题的金属纹理、斜切光边和徽记画到按钮与面板上。事件只订阅一次，每次重绘读取当前主题。
    /// </summary>
    internal static class ChromePainter
    {
        private static readonly ConditionalWeakTable<Control, object> _hooks = new ConditionalWeakTable<Control, object>();

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
                button.FlatAppearance.MouseOverBackColor = fill;
                button.FlatAppearance.MouseDownBackColor = fill;
                if (UsesChrome)
                {
                    button.BackColor = fill;
                    button.ForeColor = ButtonFore(button);
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
            }
            else if (IsSurfaceTag(control.Tag as string))
            {
                EnableTransparent(control);
                control.Paint += OnSurfacePaint;
            }
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
            Color fill = ButtonFill(button);
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

            using (Pen highlight = new Pen(Color.FromArgb(accent || danger ? 80 : 110, Color.White)))
            using (Pen shadow = new Pen(Color.FromArgb(90, Color.Black)))
            using (Pen edge = new Pen(accent || danger ? glow : ThemeManager.GetColor("border")))
            {
                g.DrawLine(highlight, 1, 1, rect.Width - 2, 1);
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
        /// 按钮文字色。危险按钮固定白字，避免亮色强调主题把字画成深色。
        /// </summary>
        private static Color ButtonFore(Button button)
        {
            string? tag = button.Tag as string;
            if (tag == "theme:danger")
            {
                return Color.White;
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
