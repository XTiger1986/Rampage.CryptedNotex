using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Rampage.CryptedNotex.WinForm.Internal.Themes
{
    /// <summary>
    /// 主题身份图形。矢量绘制保持离线、清晰、无额外文件依赖，且不进入编辑器正文。
    /// </summary>
    internal static class ThemeArtwork
    {
        public static string GetStyleName(Theme theme)
        {
            if (!string.IsNullOrWhiteSpace(theme.Appearance?.StyleName))
            {
                return theme.Appearance!.StyleName;
            }

            return theme.Id switch
            {
                "light" => "纸白 · 编辑网格",
                "dark" => "石墨 · 极简光线",
                "contrast" => "黑金 · 清晰边界",
                "forest" => "松林 · 植物剪影",
                "parchment" => "书房 · 档案纸页",
                "violet" => "暮紫 · 柔和圆弧",
                "invader" => "冰青 · 侦察雷达",
                "alienmorph" => "珠紫 · 有机流体",
                "alx" => "蓝橙 · 建筑构成",
                "darkstar" => "酒红 · 星体轨道",
                "xenomorph" => "酸绿 · 生物机械",
                _ => theme.Name
            };
        }

        public static void Draw(Graphics graphics, Rectangle bounds, Theme theme)
        {
            if (bounds.Width < 1 || bounds.Height < 1)
            {
                return;
            }

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SetClip(bounds, CombineMode.Intersect);
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 1000F, bounds.Height / 600F);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color back = Token(theme, "panelBack", Color.White);
                Color accent = Token(theme, "accent", Color.SteelBlue);
                Color secondary = theme.CategoryPalette.Count > 1
                    && ThemeManager.TryParseColor(theme.CategoryPalette[1], out Color palette) ? palette : accent;
                using (SolidBrush brush = new SolidBrush(back))
                {
                    graphics.FillRectangle(brush, 0, 0, 1000, 600);
                }

                string motif = string.IsNullOrWhiteSpace(theme.Appearance?.Motif) ? theme.Id : theme.Appearance!.Motif;
                switch (motif)
                {
                    case "light":
                        DrawEditorial(graphics, accent);
                        break;
                    case "dark":
                        DrawGraphite(graphics, accent);
                        break;
                    case "contrast":
                        DrawContrast(graphics, accent);
                        break;
                    case "forest":
                        DrawLeaves(graphics, accent, secondary);
                        break;
                    case "parchment":
                        DrawPages(graphics, accent);
                        break;
                    case "violet":
                        DrawArches(graphics, accent, secondary);
                        break;
                    case "invader":
                        DrawRadar(graphics, accent);
                        break;
                    case "alienmorph":
                        DrawFluid(graphics, accent, secondary);
                        break;
                    case "alx":
                        DrawArchitecture(graphics, accent, secondary);
                        break;
                    case "darkstar":
                        DrawOrbits(graphics, accent, secondary);
                        break;
                    case "xenomorph":
                        DrawBiomechanical(graphics, accent);
                        break;
                    default:
                        DrawGraphite(graphics, accent);
                        break;
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static Color Token(Theme theme, string key, Color fallback)
        {
            return theme.Colors.TryGetValue(key, out string? hex)
                && ThemeManager.TryParseColor(hex, out Color color) ? color : fallback;
        }

        private static void DrawEditorial(Graphics g, Color accent)
        {
            using (Pen grid = new Pen(Color.FromArgb(22, accent), 1.5F))
            using (Pen line = new Pen(Color.FromArgb(110, accent), 3F))
            using (SolidBrush block = new SolidBrush(Color.FromArgb(24, accent)))
            {
                for (int x = 60; x < 1000; x += 80)
                {
                    g.DrawLine(grid, x, 0, x, 600);
                }
                for (int y = 60; y < 600; y += 80)
                {
                    g.DrawLine(grid, 0, y, 1000, y);
                }
                g.FillRectangle(block, 440, 100, 340, 400);
                g.DrawRectangle(line, 400, 70, 340, 400);
                g.DrawLine(line, 445, 165, 685, 165);
                g.DrawLine(line, 445, 195, 605, 195);
                g.DrawLine(line, 445, 365, 685, 365);
                g.FillRectangle(block, 445, 235, 240, 90);
            }
        }

        private static void DrawGraphite(Graphics g, Color accent)
        {
            using (Pen fine = new Pen(Color.FromArgb(24, accent), 1F))
            using (Pen edge = new Pen(Color.FromArgb(110, accent), 2F))
            using (SolidBrush wash = new SolidBrush(Color.FromArgb(12, accent)))
            {
                g.FillRectangle(wash, 500, 0, 500, 600);
                for (int i = 0; i < 9; i++)
                {
                    g.DrawLine(fine, 160 + i * 75, 520, 460 + i * 75, 80);
                }
                g.DrawLine(edge, 360, 480, 630, 100);
                g.DrawLine(edge, 630, 100, 900, 100);
                g.DrawLine(fine, 60, 520, 940, 520);
                using (SolidBrush marker = new SolidBrush(Color.FromArgb(140, accent)))
                {
                    g.FillRectangle(marker, 620, 90, 20, 20);
                }
            }
        }

        private static void DrawContrast(Graphics g, Color accent)
        {
            using (SolidBrush strong = new SolidBrush(accent))
            using (Pen outline = new Pen(accent, 12F))
            {
                g.DrawRectangle(outline, 440, 110, 340, 360);
                g.FillRectangle(strong, 470, 140, 60, 300);
                g.FillRectangle(strong, 560, 140, 190, 60);
                g.FillRectangle(strong, 560, 230, 130, 40);
                g.FillRectangle(strong, 70, 510, 860, 10);
            }
        }

        private static void DrawLeaves(Graphics g, Color accent, Color secondary)
        {
            using (Pen stem = new Pen(Color.FromArgb(100, accent), 4F))
            using (SolidBrush leaf = new SolidBrush(Color.FromArgb(38, accent)))
            using (SolidBrush other = new SolidBrush(Color.FromArgb(26, secondary)))
            {
                g.DrawBezier(stem, 400, 640, 510, 450, 610, 270, 780, 40);
                for (int i = 0; i < 5; i++)
                {
                    float x = 450 + i * 60;
                    float y = 510 - i * 85;
                    using (GraphicsPath left = new GraphicsPath())
                    using (GraphicsPath right = new GraphicsPath())
                    {
                        left.AddBezier(x, y, x - 180, y - 5, x - 140, y - 170, x - 110, y - 160);
                        left.AddBezier(x - 110, y - 160, x + 10, y - 130, x + 25, y - 60, x, y);
                        right.AddBezier(x + 20, y - 35, x + 50, y - 165, x + 190, y - 140, x + 190, y - 100);
                        right.AddBezier(x + 190, y - 100, x + 155, y - 10, x + 65, y - 5, x + 20, y - 35);
                        g.FillPath(leaf, left);
                        g.FillPath(other, right);
                    }
                }
                g.DrawEllipse(stem, 820, 440, 66, 66);
            }
        }

        private static void DrawPages(Graphics g, Color accent)
        {
            using (SolidBrush paper = new SolidBrush(Color.FromArgb(18, accent)))
            using (Pen rule = new Pen(Color.FromArgb(65, accent), 2F))
            using (Pen ink = new Pen(Color.FromArgb(130, accent), 3F))
            {
                for (int i = 3; i >= 0; i--)
                {
                    Rectangle page = new Rectangle(365 + i * 30, 70 + i * 23, 345, 420);
                    g.FillRectangle(paper, page);
                    g.DrawRectangle(rule, page);
                }
                g.DrawLine(ink, 410, 145, 640, 145);
                for (int y = 195; y < 430; y += 33)
                {
                    g.DrawLine(rule, 410, y, 650 - (y % 3) * 30, y);
                }
                g.DrawRectangle(ink, 70, 450, 110, 65);
                g.DrawLine(rule, 95, 474, 155, 474);
                g.DrawLine(rule, 95, 490, 135, 490);
            }
        }

        private static void DrawArches(Graphics g, Color accent, Color secondary)
        {
            for (int i = 0; i < 7; i++)
            {
                int size = 760 - i * 80;
                using (Pen pen = new Pen(Color.FromArgb(22 + i * 8, i % 2 == 0 ? accent : secondary), 20F))
                {
                    g.DrawArc(pen, 680 - size / 2, 300 - size / 2, size, size, 110, 280);
                }
            }
            using (SolidBrush dot = new SolidBrush(Color.FromArgb(130, secondary)))
            {
                g.FillEllipse(dot, 270, 410, 48, 48);
            }
        }

        private static void DrawRadar(Graphics g, Color accent)
        {
            using (Pen grid = new Pen(Color.FromArgb(36, accent), 1.5F))
            using (Pen ring = new Pen(Color.FromArgb(110, accent), 3F))
            using (SolidBrush sweep = new SolidBrush(Color.FromArgb(25, accent)))
            using (SolidBrush dot = new SolidBrush(accent))
            {
                for (int x = 0; x <= 1000; x += 60)
                {
                    g.DrawLine(grid, x, 0, x, 600);
                }
                for (int y = 0; y <= 600; y += 60)
                {
                    g.DrawLine(grid, 0, y, 1000, y);
                }
                for (int size = 120; size <= 540; size += 120)
                {
                    g.DrawEllipse(ring, 660 - size / 2, 300 - size / 2, size, size);
                }
                g.FillPie(sweep, 390, 30, 540, 540, -70, 60);
                g.DrawLine(ring, 660, 300, 830, 90);
                g.DrawLine(grid, 340, 300, 980, 300);
                g.DrawLine(grid, 660, 0, 660, 600);
                g.FillEllipse(dot, 742, 190, 15, 15);
                g.FillEllipse(dot, 510, 370, 10, 10);
                g.DrawRectangle(ring, 105, 425, 100, 75);
            }
        }

        private static void DrawFluid(Graphics g, Color accent, Color secondary)
        {
            using (SolidBrush wash = new SolidBrush(Color.FromArgb(14, secondary)))
            {
                g.FillEllipse(wash, 210, -80, 800, 800);
            }
            for (int i = 0; i < 10; i++)
            {
                using (Pen pen = new Pen(Color.FromArgb(25 + i * 7, i % 2 == 0 ? accent : secondary), 2.8F))
                using (GraphicsPath path = new GraphicsPath())
                {
                    float inset = i * 19;
                    path.AddBezier(380 + inset, 95 + inset / 2, 680, -90 + inset, 1060 - inset, 175, 830 - inset / 2, 380);
                    path.AddBezier(830 - inset / 2, 380, 580, 600 - inset, 250 + inset, 540 - inset, 380 + inset, 95 + inset / 2);
                    g.DrawPath(pen, path);
                }
            }
        }

        private static void DrawArchitecture(Graphics g, Color accent, Color secondary)
        {
            using (Pen fine = new Pen(Color.FromArgb(50, secondary), 1.5F))
            using (SolidBrush orange = new SolidBrush(Color.FromArgb(160, accent)))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(30, secondary)))
            using (Pen edge = new Pen(Color.FromArgb(130, secondary), 3F))
            {
                g.FillRectangle(blue, 445, 110, 210, 390);
                g.FillRectangle(blue, 655, 210, 180, 290);
                g.DrawRectangle(edge, 445, 110, 210, 390);
                g.DrawRectangle(edge, 655, 210, 180, 290);
                g.FillRectangle(orange, 390, 290, 320, 42);
                for (int x = 465; x < 835; x += 35)
                {
                    g.DrawLine(fine, x, 350, x, 480);
                }
                g.DrawLine(fine, 65, 500, 935, 500);
                g.DrawLine(fine, 65, 510, 935, 510);
                g.FillRectangle(orange, 115, 110, 70, 70);
            }
        }

        private static void DrawOrbits(Graphics g, Color accent, Color secondary)
        {
            using (SolidBrush planet = new SolidBrush(Color.FromArgb(28, accent)))
            using (Pen ring = new Pen(Color.FromArgb(85, accent), 2.5F))
            using (Pen fine = new Pen(Color.FromArgb(45, secondary), 1.5F))
            using (SolidBrush star = new SolidBrush(Color.FromArgb(210, secondary)))
            {
                g.FillEllipse(planet, 470, 110, 350, 350);
                g.DrawEllipse(ring, 470, 110, 350, 350);
                GraphicsState orbit = g.Save();
                g.TranslateTransform(645, 285);
                g.RotateTransform(-25);
                g.DrawEllipse(ring, -325, -115, 650, 230);
                g.DrawEllipse(fine, -390, -160, 780, 320);
                g.FillEllipse(star, 290, -28, 22, 22);
                g.Restore(orbit);
                for (int i = 0; i < 18; i++)
                {
                    int x = 60 + i * 139 % 880;
                    int y = 50 + i * 97 % 480;
                    g.FillEllipse(star, x, y, i % 4 == 0 ? 5 : 2, i % 4 == 0 ? 5 : 2);
                }
            }
        }

        private static void DrawBiomechanical(Graphics g, Color accent)
        {
            using (Pen rib = new Pen(Color.FromArgb(95, accent), 2.5F))
            using (Pen under = new Pen(Color.FromArgb(20, accent), 13F))
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(160, accent)))
            {
                for (int i = 0; i < 9; i++)
                {
                    int x = 310 + i * 52;
                    Point[] points = { new Point(x, 55), new Point(x + 130, 180), new Point(x + 130, 420), new Point(x, 545) };
                    g.DrawLines(under, points);
                    g.DrawLines(rib, points);
                }
                g.DrawLine(rib, 260, 300, 910, 300);
                g.FillRectangle(glow, 580, 288, 70, 24);
                for (int y = 160; y < 450; y += 28)
                {
                    g.FillRectangle(glow, 115, y, 5, 10);
                }
                g.DrawRectangle(rib, 90, 135, 55, 330);
            }
        }
    }
}
