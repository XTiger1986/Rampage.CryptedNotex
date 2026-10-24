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
    }
}
