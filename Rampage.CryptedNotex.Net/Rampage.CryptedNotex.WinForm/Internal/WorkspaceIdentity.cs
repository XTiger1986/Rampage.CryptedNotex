using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// A quiet identity panel: each theme supplies its own artwork, outside the writing surface.
    /// </summary>
    internal sealed class WorkspaceIdentity : Control
    {
        public WorkspaceIdentity()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            AccessibleName = "秘密档案，本地加密笔记";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = DeviceDpi / 96F;
            int inset = (int)(20 * scale);
            int footer = (int)(72 * scale);
            ThemeArtwork.Draw(e.Graphics, new Rectangle(inset, (int)(12 * scale), Math.Max(1, Width - inset * 2), Math.Max(1, Height - footer - (int)(24 * scale))), ThemeManager.Current);
            using (Font title = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold))
            using (Font subtitle = new Font("Microsoft YaHei UI", 8.5F))
            {
                TextRenderer.DrawText(e.Graphics, NotexBrand.DisplayName, title,
                    new Rectangle(inset, Height - footer, Width - inset * 2, (int)(34 * scale)),
                    ThemeManager.GetColor("text"), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, "本地 · 私密 · 自由记录", subtitle,
                    new Rectangle(inset, Height - (int)(36 * scale), Width - inset * 2, (int)(24 * scale)),
                    ThemeManager.GetColor("textSecondary"), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }
}
