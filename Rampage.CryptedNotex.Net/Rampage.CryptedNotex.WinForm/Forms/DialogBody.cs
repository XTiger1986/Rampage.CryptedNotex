using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 简短对话框的自动流式布局。使用控件的实测高度，避免高 DPI 标签压住输入框。
    /// </summary>
    internal sealed class DialogBody : TableLayoutPanel
    {
        public DialogBody()
        {
            Dock = DockStyle.Top;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ColumnCount = 1;
            Padding = new Padding(28);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        }

        public void Add(Control control)
        {
            int row = RowCount++;
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(control, 0, row);
        }

        public void AddHeading(string title)
        {
            Add(new Label { Text = title, AutoSize = true, Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 18) });
        }

        public void AddHelp(string text)
        {
            Add(new Label { Text = text, Tag = "theme:muted", AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 18) });
        }

        public void AddField(string label, Control control)
        {
            Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 0, 0, 18);
            control.AccessibleName = label;
            Add(control);
        }

        public void FitForm(Form form)
        {
            Rectangle work = Screen.FromControl(form).WorkingArea;
            form.ClientSize = new Size(form.ClientSize.Width, Math.Min(PreferredSize.Height, work.Height - (int)(72 * form.DeviceDpi / 96F)));
            form.AutoScroll = true;
        }
    }
}
