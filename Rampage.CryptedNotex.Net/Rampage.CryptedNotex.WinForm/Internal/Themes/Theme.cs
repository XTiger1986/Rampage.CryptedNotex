using System.Collections.Generic;

namespace Rampage.CryptedNotex.WinForm.Internal.Themes
{
    /// <summary>
    /// 主题字体定义
    /// </summary>
    public sealed class ThemeFont
    {
        /// <summary>
        /// 字体族
        /// </summary>
        public string Family { get; set; } = "Microsoft YaHei UI";

        /// <summary>
        /// 字号(磅)
        /// </summary>
        public double Size { get; set; } = 9.0;
    }

    /// <summary>
    /// 界面主题(JSON 文件模型；颜色键约定见 ThemeManager.GetColor 备注)
    /// </summary>
    public sealed class Theme
    {
        /// <summary>
        /// 主题 id(唯一；内嵌主题可被 exe 旁 themes\同名 id 的自定义 JSON 覆盖)
        /// </summary>
        public string Id { get; set; } = "light";

        /// <summary>
        /// 显示名
        /// </summary>
        public string Name { get; set; } = "浅色";

        /// <summary>
        /// 一句身份描述(主题画廊展示;旧 JSON 可缺省)
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 是否启用深色标题栏(DwmSetWindowAttribute,Win11/较新 Win10 生效)
        /// </summary>
        public bool DarkTitleBar { get; set; }

        /// <summary>
        /// 颜色表(hex 字符串,键→"#RRGGBB")
        /// </summary>
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 字体表(ui=界面字体;listTitle=列表标题字体)
        /// </summary>
        public Dictionary<string, ThemeFont>? Fonts { get; set; }

        /// <summary>
        /// 分类色板(侧栏色点/列表标记,按分类序号取模)
        /// </summary>
        public List<string> CategoryPalette { get; set; } = new List<string>();

        /// <summary>
        /// 金属质感与壁纸。缺省或 metal=none 时保持扁平上色。
        /// </summary>
        public ThemeChrome? Chrome { get; set; }

        /// <summary>
        /// 可选视觉身份。旧版自定义主题可省略，自动采用相应 id 的图形和默认按钮形状。
        /// </summary>
        public ThemeAppearance? Appearance { get; set; }
    }

    /// <summary>
    /// 仅用于绘制的主题属性，不参与用户数据存储。
    /// </summary>
    public sealed class ThemeAppearance
    {
        public string Motif { get; set; } = string.Empty;

        public string StyleName { get; set; } = string.Empty;

        /// <summary>
        /// rounded / square / cut。
        /// </summary>
        public string ButtonShape { get; set; } = "rounded";
    }

    /// <summary>
    /// 主题的金属边框与壁纸(可选；旧主题 JSON 不含此段)
    /// </summary>
    public sealed class ThemeChrome
    {
        /// <summary>
        /// 金属：none 扁平 / silver 拉丝银 / black 拉丝黑
        /// </summary>
        public string Metal { get; set; } = "none";

        /// <summary>
        /// 壁纸 id(invader / alienmorph / darkstar / alx / xenomorph)；空则无壁纸
        /// </summary>
        public string Wallpaper { get; set; } = string.Empty;

        /// <summary>
        /// 内发光色(#RRGGBB)；空则用 accent
        /// </summary>
        public string Glow { get; set; } = string.Empty;
    }
}
