using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace Rampage.CryptedNotex.WinForm.Internal.Themes
{
    /// <summary>
    /// 内嵌金属纹理、壁纸与档案徽记。图片在进程内常驻，不随主题切换释放。
    /// </summary>
    internal static class ChromeAssets
    {
        private const string RESOURCE_PFX = "Rampage.CryptedNotex.WinForm.Assets.Chrome.";

        private static readonly Dictionary<string, Image?> _images = new Dictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);
        private static Icon? _appIcon;
        private static Bitmap? _iconBitmap;

        /// <summary>
        /// 透明档案徽记；资源缺失时为 null
        /// </summary>
        public static Image? Emblem
        {
            get
            {
                return Load("emblem.png");
            }
        }

        /// <summary>
        /// 窗口图标(任务栏与标题栏)
        /// </summary>
        public static Icon AppIcon
        {
            get
            {
                if (_appIcon != null)
                {
                    return _appIcon;
                }

                _iconBitmap = Load("app-icon.png") as Bitmap;
                if (_iconBitmap == null)
                {
                    _appIcon = SystemIcons.Application;
                    return _appIcon;
                }

                _appIcon = Icon.FromHandle(_iconBitmap.GetHicon());
                return _appIcon;
            }
        }

        /// <summary>
        /// 按金属种类取可平铺纹理
        /// </summary>
        /// <param name="metal">silver / black / none</param>
        /// <returns>纹理；none 或缺失时为 null</returns>
        public static Image? Metal(string? metal)
        {
            if (string.Equals(metal, "black", StringComparison.OrdinalIgnoreCase))
            {
                return Load("metal-black.jpg");
            }
            if (string.Equals(metal, "silver", StringComparison.OrdinalIgnoreCase))
            {
                return Load("metal-silver.jpg");
            }
            return null;
        }

        /// <summary>
        /// 按壁纸 id 取壁纸
        /// </summary>
        /// <param name="wallpaperId">主题 chrome.wallpaper</param>
        /// <returns>壁纸；空 id 或缺失时为 null</returns>
        public static Image? GetWallpaper(string? wallpaperId)
        {
            if (string.IsNullOrWhiteSpace(wallpaperId))
            {
                return null;
            }
            return Load("wallpaper-" + wallpaperId + ".jpg");
        }

        /// <summary>
        /// 从内嵌资源复制出独立位图(与资源流脱钩，保留透明通道)
        /// </summary>
        private static Image? Load(string fileName)
        {
            if (_images.TryGetValue(fileName, out Image? cached))
            {
                return cached;
            }

            Image? image = null;
            Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCE_PFX + fileName);
            if (stream != null)
            {
                using (stream)
                using (Image loaded = Image.FromStream(stream))
                {
                    Bitmap copy = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format32bppArgb);
                    using (Graphics graphics = Graphics.FromImage(copy))
                    {
                        graphics.DrawImage(loaded, 0, 0, loaded.Width, loaded.Height);
                    }
                    image = copy;
                }
            }

            _images[fileName] = image;
            return image;
        }
    }
}
