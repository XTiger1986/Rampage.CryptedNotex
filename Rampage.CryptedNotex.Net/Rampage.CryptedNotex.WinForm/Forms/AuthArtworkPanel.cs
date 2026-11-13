using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 认证页面的主题插画区；矢量图案随主题变化，不进入笔记内容区。
    /// </summary>
    internal sealed class AuthArtworkPanel : Panel
    {
        public AuthArtworkPanel()
        {
            Tag = "theme:sidebar";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = DeviceDpi / 96F;
            int inset = (int)(32 * scale);
            Rectangle art = new Rectangle(0, (int)(124 * scale), Width, Math.Max(1, Height - (int)(260 * scale)));
            ThemeArtwork.Draw(e.Graphics, art, ThemeManager.Current);
            using (Font eyebrow = new Font("Consolas", 9F))
            using (Font title = new Font("Microsoft YaHei UI", 25F, FontStyle.Bold))
            using (Font body = new Font("Microsoft YaHei UI", 10F))
            {
                Draw(e.Graphics, "CRYPTED / NOTEX", eyebrow, new Rectangle(inset, inset, Width - 2 * inset, (int)(26 * scale)), ThemeManager.GetColor("accent"));
                Draw(e.Graphics, NotexBrand.DisplayName, title, new Rectangle(inset, inset + (int)(32 * scale), Width - 2 * inset, (int)(60 * scale)), ThemeManager.GetColor("text"));
                int bottom = Height - (int)(118 * scale);
                Draw(e.Graphics, "留给自己的一方空间。", body, new Rectangle(inset, bottom, Width - 2 * inset, (int)(28 * scale)), ThemeManager.GetColor("text"));
                Draw(e.Graphics, "离线存储  /  密码保护  /  专注记录", eyebrow, new Rectangle(inset, bottom + (int)(36 * scale), Width - 2 * inset, (int)(48 * scale)), ThemeManager.GetColor("textSecondary"));
            }
        }

        private static void Draw(Graphics graphics, string text, Font font, Rectangle bounds, Color color)
        {
            TextRenderer.DrawText(graphics, text, font, bounds, color, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }
}
