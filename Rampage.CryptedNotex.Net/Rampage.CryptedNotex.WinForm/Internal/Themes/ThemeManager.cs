using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal.Themes
{
    /// <summary>
    /// 主题管理器：加载内嵌与 exe 旁 themes\ 自定义主题,递归应用配色到控件树,支持实时切换
    /// </summary>
    /// <remarks>
    /// 颜色键约定：
    /// windowBack 窗体底 / panelBack 面板底 / panelAltBack 列表底 / border 边框 /
    /// inputBack 输入底 / inputBorder 输入边框 / text 主文本 / textSecondary 次文本 /
    /// accent 强调色 / accentText 强调色上的文本 / selectionBack 选中底 /
    /// pinnedMark 置顶标记 / trashTint 回收站色 / warning 警示 / danger 危险 / link 链接 /
    /// editorBack 编辑器底 / editorText 编辑器文本 / previewBack 预览底。
    /// 控件 Tag 约定："theme:accent" 强调按钮、"theme:muted" 次要文本、"theme:danger" 危险按钮。
    /// </remarks>
    public static class ThemeManager
    {
        /// <summary>
        /// 内嵌主题资源名前缀
        /// </summary>
        private const string THEME_RESOURCE_PFX = "Rampage.CryptedNotex.WinForm.Internal.Themes.";

        private static readonly Dictionary<string, Theme> _themes = new Dictionary<string, Theme>();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        /// <summary>
        /// 当前主题(初始化前为内置浅色)
        /// </summary>
        public static Theme Current
        {
            get;
            private set;
        } = new Theme();

        /// <summary>
        /// 可用主题列表(按显示名排序)
        /// </summary>
        public static List<Theme> AvailableThemes
        {
            get
            {
                return _themes.Values.OrderBy(m => m.Name).ToList();
            }
        }

        /// <summary>
        /// 当前主题切换事件(参数为新主题)
        /// </summary>
        public static event Action<Theme>? CurrentChanged;

        /// <summary>
        /// 初始化：加载内嵌主题,再以 exe 旁 themes\*.json 按 id 覆盖或追加；至少保证 light 主题存在
        /// </summary>
        public static void Initialize()
        {
            _themes.Clear();
            foreach (string resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(name => name.StartsWith(THEME_RESOURCE_PFX, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal))
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!)
                {
                    LoadThemeFromStream(stream, resourceName);
                }
            }

            string themeDirectory = Path.Combine(AppContext.BaseDirectory, "themes");
            if (Directory.Exists(themeDirectory))
            {
                foreach (string file in Directory.GetFiles(themeDirectory, "*.json"))
                {
                    using (Stream stream = File.OpenRead(file))
                    {
                        LoadThemeFromStream(stream, file);
                    }
                }
            }

            if (!_themes.ContainsKey("light"))
            {
                _themes["light"] = new Theme();
            }
        }

        /// <summary>
        /// 切换当前主题(找不到 id 时回退 light)；持久化由调用方负责
        /// </summary>
        /// <param name="themeId">主题 id</param>
        public static void ApplyTheme(string themeId)
        {
            if (!_themes.TryGetValue(themeId, out Theme? theme))
            {
                theme = _themes["light"];
            }
            Current = theme;
            CurrentChanged?.Invoke(theme);
        }

        /// <summary>
        /// 取当前主题颜色(键缺失时回退浅色主题默认值)
        /// </summary>
        /// <param name="key">颜色键</param>
        /// <returns>颜色值</returns>
        public static Color GetColor(string key)
        {
            if (Current.Colors.TryGetValue(key, out string? hex) && TryParseColor(hex, out Color color))
            {
                return color;
            }
            return DefaultColor(key);
        }

        /// <summary>
        /// 取当前主题字体
        /// </summary>
        /// <param name="key">字体键(ui/listTitle)</param>
        /// <returns>字体实例</returns>
        public static Font GetFont(string key)
        {
            ThemeFont font = Current.Fonts != null && Current.Fonts.TryGetValue(key, out ThemeFont? defined) ? defined : new ThemeFont();
            return new Font(font.Family, (float)font.Size, FontStyle.Regular, GraphicsUnit.Point);
        }

        /// <summary>
        /// 分类色板取色(按分类序号取模；空色板回退强调色)
        /// </summary>
        /// <param name="index">分类序号</param>
        /// <returns>分类颜色</returns>
        public static Color GetCategoryColor(int index)
        {
            if (Current.CategoryPalette.Count == 0)
            {
                return GetColor("accent");
            }
            string hex = Current.CategoryPalette[Math.Abs(index) % Current.CategoryPalette.Count];
            return TryParseColor(hex, out Color color) ? color : GetColor("accent");
        }

        /// <summary>
        /// 递归应用当前主题到控件树(幂等,可重复调用)
        /// </summary>
        /// <param name="root">根控件(通常为 Form)</param>
        public static void Apply(Control root)
        {
            if (root == null)
            {
                return;
            }

            Color parentBack = root is Form || root.Parent == null
                ? GetColor("windowBack")
                : root.Parent.BackColor;
            root.SuspendLayout();
            try
            {
                ApplyCore(root, parentBack);
                ChromePainter.RefreshTree(root);
            }
            finally
            {
                root.ResumeLayout(true);
            }
        }

        /// <summary>
        /// 单个控件应用配色(记录父容器底色用于 Label 透明)
        /// </summary>
        private static void ApplyCore(Control control, Color parentBack)
        {
            Color text = GetColor("text");
            Color panelBack = GetColor("panelBack");
            Color panelAltBack = GetColor("panelAltBack");
            string? tag = control.Tag as string;

            if (control is Form form)
            {
                form.BackColor = GetColor("windowBack");
                form.ForeColor = text;
                ApplyTitleBar(form);
            }
            else if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = GetColor("inputBack");
                textBox.ForeColor = text;
            }
            else if (control is RichTextBox)
            {
                control.BackColor = GetColor("editorBack");
                control.ForeColor = GetColor("editorText");
            }
            else if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = GetColor("border");
                button.Cursor = Cursors.Hand;
                if (tag == "theme:accent")
                {
                    button.BackColor = GetColor("accent");
                    button.ForeColor = GetColor("accentText");
                }
                else if (tag == "theme:danger")
                {
                    button.BackColor = GetColor("danger");
                    button.ForeColor = Color.White;
                }
                else
                {
                    button.BackColor = panelBack;
                    button.ForeColor = text;
                }
            }
            else if (control is ListView || control is ListBox || control is TreeView)
            {
                control.BackColor = panelAltBack;
                control.ForeColor = text;
            }
            else if (control is Label || control is LinkLabel)
            {
                control.BackColor = ChromePainter.HostsMetal(control.Parent) ? Color.Transparent : parentBack;
                control.ForeColor = tag == "theme:muted" ? GetColor("textSecondary") : (tag == "theme:link" ? GetColor("link") : text);
            }
            else if (control is PictureBox picture && tag == "theme:emblem")
            {
                picture.SizeMode = PictureBoxSizeMode.Zoom;
                picture.BackColor = parentBack;
                picture.Image = ChromeAssets.Emblem;
            }
            else if (control is PictureBox || control is ProgressBar)
            {
                // 图片与进度条保持控件默认渲染
            }
            else
            {
                control.BackColor = parentBack;
                control.ForeColor = text;
            }

            foreach (Control child in control.Controls)
            {
                Color childParentBack = control is Form ? GetColor("windowBack") : control.BackColor;
                ApplyCore(child, childParentBack);
            }
        }

        /// <summary>
        /// 深色标题栏(DWM 属性 20,失败回退 19,再失败静默保持系统标题栏)
        /// </summary>
        private static void ApplyTitleBar(Form form)
        {
            if (!Current.DarkTitleBar)
            {
                return;
            }

            try
            {
                int dark = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));
                }
            }
            catch
            {
                // 旧系统无 DWM 支持,忽略
            }
        }

        /// <summary>
        /// 从流加载/覆盖主题(按 id 去重覆盖)
        /// </summary>
        private static void LoadThemeFromStream(Stream stream, string sourceName)
        {
            try
            {
                Theme? theme = JsonSerializer.Deserialize<Theme>(stream, _jsonOptions);
                if (theme != null && !string.IsNullOrWhiteSpace(theme.Id))
                {
                    _themes[theme.Id] = theme;
                }
            }
            catch (Exception ex)
            {
                // 单个主题损坏不阻塞启动
                System.Diagnostics.Debug.WriteLine("主题加载失败 " + sourceName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 解析 "#RRGGBB"
        /// </summary>
        internal static bool TryParseColor(string? hex, out Color color)
        {
            if (!string.IsNullOrEmpty(hex))
            {
                try
                {
                    color = ColorTranslator.FromHtml(hex);
                    return true;
                }
                catch
                {
                    // 格式非法走默认色
                }
            }
            color = Color.Black;
            return false;
        }

        /// <summary>
        /// 浅色主题默认色(键缺失兜底)
        /// </summary>
        private static Color DefaultColor(string key)
        {
            return key switch
            {
                "windowBack" => Color.White,
                "panelBack" => Color.FromArgb(245, 246, 248),
                "panelAltBack" => Color.White,
                "border" => Color.FromArgb(220, 222, 226),
                "inputBack" => Color.White,
                "inputBorder" => Color.FromArgb(204, 204, 204),
                "text" => Color.FromArgb(32, 34, 38),
                "textSecondary" => Color.FromArgb(128, 132, 138),
                "accent" => Color.FromArgb(0, 122, 204),
                "accentText" => Color.White,
                "selectionBack" => Color.FromArgb(204, 228, 247),
                "pinnedMark" => Color.FromArgb(191, 144, 0),
                "trashTint" => Color.FromArgb(146, 110, 110),
                "warning" => Color.FromArgb(156, 116, 33),
                "danger" => Color.FromArgb(200, 60, 52),
                "link" => Color.FromArgb(0, 102, 170),
                "editorBack" => Color.White,
                "editorText" => Color.FromArgb(32, 34, 38),
                "previewBack" => Color.White,
                _ => Color.Black
            };
        }

        /// <summary>
        /// 设置窗口标题栏深浅(DWM)
        /// </summary>
        /// <returns>调用结果</returns>
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
