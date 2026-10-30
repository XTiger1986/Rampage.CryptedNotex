using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 顶栏忙碌条：不定进度动画 + 说明文字。隐藏时不占布局。
    /// </summary>
    internal sealed class BusyBanner : Panel
    {
        private readonly Label _label;
        private readonly ProgressBar _bar;

        /// <summary>
        /// 构造忙碌条
        /// </summary>
        public BusyBanner()
        {
            Height = 42;
            Visible = false;
            Padding = new Padding(16, 6, 16, 6);
            DoubleBuffered = true;

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _bar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 24,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 14, 8)
            };
            _label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0, 4, 0, 0),
                Text = "正在处理…"
            };
            grid.Controls.Add(_bar, 0, 0);
            grid.Controls.Add(_label, 1, 0);
            Controls.Add(grid);
        }

        /// <summary>
        /// 显示并更新说明
        /// </summary>
        /// <param name="message">忙碌说明</param>
        public void ShowBusy(string message)
        {
            _label.Text = message;
            Visible = true;
            BringToFront();
        }

        /// <summary>
        /// 收起忙碌条
        /// </summary>
        public void HideBusy()
        {
            Visible = false;
        }

        /// <summary>
        /// 底边用强调色标出，和正文区分开
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(ThemeManager.GetColor("accent"), 2F))
            {
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }
    }
}
