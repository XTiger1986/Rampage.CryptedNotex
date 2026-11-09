using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Controls
{
    /// <summary>
    /// 附件面板：笔记详情下方的附件条(名称/大小/删除)，支持添加/预览/导出(动作交宿主执行)
    /// </summary>
    public class AttachmentPanel : UserControl
    {
        /// <summary>
        /// 单附件大小警告阈值(字节)
        /// </summary>
        public const long SIZE_WARNING_BYTES = 50L * 1024 * 1024;

        private readonly FlowLayoutPanel _flowPanel;
        private readonly Button _addButton;
        private bool _readOnly;

        /// <summary>
        /// 请求宿主添加附件(打开文件选择框)
        /// </summary>
        public event EventHandler? AddAttachmentRequested;

        /// <summary>
        /// 请求宿主打开附件(图片预览/其他导出)
        /// </summary>
        public event EventHandler<NoteAttachment>? OpenAttachmentRequested;

        /// <summary>
        /// 请求宿主删除附件
        /// </summary>
        public event EventHandler<NoteAttachment>? RemoveAttachmentRequested;

        /// <summary>
        /// 构造附件面板
        /// </summary>
        public AttachmentPanel()
        {
            Height = 52;
            Dock = DockStyle.Bottom;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoScroll = true,
                WrapContents = false
            };

            _addButton = new Button
            {
                Text = "添加附件",
                AutoSize = true,
                Margin = new Padding(2, 4, 4, 4)
            };
            _addButton.Click += (sender, e) => AddAttachmentRequested?.Invoke(this, EventArgs.Empty);

            layout.Controls.Add(_flowPanel, 0, 0);
            layout.Controls.Add(_addButton, 1, 0);
            Controls.Add(layout);
        }

        /// <summary>
        /// 只读查看时仍可打开附件，不能添加或删除
        /// </summary>
        /// <param name="readOnly">true 为只读</param>
        public void SetReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            _addButton.Visible = !readOnly;
        }

        /// <summary>
        /// 刷新附件列表(不含密文)
        /// </summary>
        /// <param name="attachments">附件列表</param>
        public void LoadAttachments(List<NoteAttachment> attachments)
        {
            _flowPanel.SuspendLayout();
            _flowPanel.Controls.Clear();
            foreach (NoteAttachment attachment in attachments)
            {
                _flowPanel.Controls.Add(BuildChip(attachment));
            }
            _flowPanel.ResumeLayout();
            if (IsHandleCreated)
            {
                ThemeManager.Apply(this);
            }
        }

        /// <summary>
        /// 构造单个附件 chip
        /// </summary>
        private Control BuildChip(NoteAttachment attachment)
        {
            Panel chip = new Panel
            {
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 6),
                Padding = new Padding(10, 4, 6, 4),
                MinimumSize = new Size(0, 30),
                Cursor = Cursors.Hand,
                Tag = attachment
            };
            bool hovered = false;
            chip.MouseEnter += (sender, e) => SetChipHover(chip, ref hovered, true);
            chip.MouseLeave += (sender, e) => SetChipHover(chip, ref hovered, false);
            chip.Paint += (sender, e) =>
            {
                using (Pen pen = new Pen(hovered ? ThemeManager.GetColor("accent") : ThemeManager.GetColor("border"), hovered ? 1.5F : 1F))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, chip.Width - 1, chip.Height - 1);
                }
            };

            string sizeText = FormatSize(attachment.FileSize);
            Label nameLabel = new Label
            {
                Text = attachment.FileName + " (" + sizeText + ")",
                AutoSize = true,
                Location = new Point(8, 6)
            };
            // 子控件上移动会触发父 Panel 的 MouseLeave,这里一并订阅保持悬停态
            nameLabel.MouseEnter += (sender, e) => SetChipHover(chip, ref hovered, true);
            nameLabel.Click += (sender, e) => OpenAttachmentRequested?.Invoke(this, attachment);
            chip.Controls.Add(nameLabel);

            Button removeButton = new Button
            {
                Text = "×",
                Size = new Size(20, 20),
                Location = new Point(nameLabel.Right + 4, 4),
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            removeButton.MouseEnter += (sender, e) => SetChipHover(chip, ref hovered, true);
            removeButton.Click += (sender, e) => RemoveAttachmentRequested?.Invoke(this, attachment);
            removeButton.Visible = !_readOnly;
            chip.Controls.Add(removeButton);
            chip.Width = (_readOnly ? nameLabel.Right : removeButton.Right) + 8;
            return chip;
        }

        /// <summary>
        /// 切换 chip 悬停态并重绘
        /// </summary>
        private static void SetChipHover(Control chip, ref bool hovered, bool value)
        {
            if (hovered == value)
            {
                return;
            }
            hovered = value;
            chip.Invalidate();
        }

        /// <summary>
        /// 字节数人性化显示
        /// </summary>
        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return (bytes / 1024.0 / 1024.0).ToString("0.#") + " MB";
            }
            if (bytes >= 1024)
            {
                return (bytes / 1024.0).ToString("0.#") + " KB";
            }
            return bytes + " B";
        }
    }
}
