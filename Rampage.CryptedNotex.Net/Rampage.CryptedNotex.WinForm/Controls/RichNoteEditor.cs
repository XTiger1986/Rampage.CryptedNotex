using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.Model.Dto;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Controls
{
    /// <summary>
    /// 富文本编辑器：RichTextBox(RTF 格式)+迷你格式工具条(加粗/斜体/下划线/删除线/字号/前景色/项目符号)
    /// </summary>
    public class RichNoteEditor : NoteEditorBase
    {
        private readonly RichTextBox _richTextBox;
        private readonly FlowLayoutPanel _toolbar;

        /// <summary>
        /// 构造富文本编辑器
        /// </summary>
        public RichNoteEditor()
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(2)
            };
            _toolbar.Controls.Add(BuildToolButton("B", FontStyle.Bold));
            _toolbar.Controls.Add(BuildToolButton("I", FontStyle.Italic));
            _toolbar.Controls.Add(BuildToolButton("U", FontStyle.Underline));
            _toolbar.Controls.Add(BuildToolButton("S", FontStyle.Strikeout));
            _toolbar.Controls.Add(BuildSeparator());
            _toolbar.Controls.Add(BuildBulletButton());
            _toolbar.Controls.Add(BuildColorButton());
            _toolbar.Controls.Add(BuildSeparator());
            _toolbar.Controls.Add(BuildFontSizeSelector());

            _richTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 11F),
                AcceptsTab = true
            };
            _richTextBox.TextChanged += (sender, e) =>
            {
                if (!IsReadOnly)
                {
                    MarkDirty();
                }
            };
            _richTextBox.KeyDown += EditorKeyDown;

            layout.Controls.Add(_toolbar, 0, 0);
            layout.Controls.Add(_richTextBox, 0, 1);
            Controls.Add(layout);
        }

        /// <inheritdoc />
        public override void LoadNote(NoteDetailDto dto)
        {
            Current = dto;
            try
            {
                _richTextBox.Rtf = dto.PlainContent;
            }
            catch (ArgumentException)
            {
                // 历史/异常数据不是合法 RTF 时退回纯文本展示
                _richTextBox.Text = dto.PlainContent;
            }
            IsDirty = false;
        }

        /// <inheritdoc />
        public override void SetReadOnly(bool readOnly)
        {
            base.SetReadOnly(readOnly);
            _richTextBox.ReadOnly = readOnly;
            _toolbar.Visible = !readOnly;
        }

        /// <inheritdoc />
        public override string CollectContent()
        {
            // RTF 类型以 RTF 文档为存储格式
            return _richTextBox.Rtf ?? string.Empty;
        }

        /// <inheritdoc />
        public override int GetWordCount()
        {
            // 字数按纯文本口径统计而非 RTF 源码
            return WordCountTool.CountNonWhitespace(_richTextBox.Text);
        }

        /// <summary>
        /// Ctrl+S 立即保存
        /// </summary>
        private void EditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true;
                RequestSaveNow();
            }
        }

        /// <summary>
        /// 构造字体样式切换按钮
        /// </summary>
        private Button BuildToolButton(string text, FontStyle style)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(30, 26),
                Font = new Font("Microsoft YaHei UI", 9F, style)
            };
            button.Click += (sender, e) => ToggleStyle(style);
            return button;
        }

        /// <summary>
        /// 切换选中文本的字体样式
        /// </summary>
        private void ToggleStyle(FontStyle style)
        {
            if (_richTextBox.SelectionFont == null)
            {
                return;
            }

            FontStyle current = _richTextBox.SelectionFont.Style;
            FontStyle target = (current & style) == style ? current & ~style : current | style;
            _richTextBox.SelectionFont = new Font(_richTextBox.SelectionFont, target);
            MarkDirty();
        }

        /// <summary>
        /// 项目符号开关
        /// </summary>
        private Button BuildBulletButton()
        {
            Button button = new Button
            {
                Text = "• 列表",
                Size = new Size(52, 26),
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            button.Click += (sender, e) =>
            {
                _richTextBox.SelectionBullet = !_richTextBox.SelectionBullet;
                MarkDirty();
            };
            return button;
        }

        /// <summary>
        /// 前景色选择
        /// </summary>
        private Button BuildColorButton()
        {
            Button button = new Button
            {
                Text = "A 颜色",
                Size = new Size(60, 26),
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            button.Click += (sender, e) =>
            {
                using (ColorDialog dialog = new ColorDialog())
                {
                    if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        _richTextBox.SelectionColor = dialog.Color;
                        MarkDirty();
                    }
                }
            };
            return button;
        }

        /// <summary>
        /// 字号选择
        /// </summary>
        private ComboBox BuildFontSizeSelector()
        {
            ComboBox comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 52,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            foreach (float size in new float[] { 9F, 10F, 11F, 12F, 14F, 16F, 18F, 22F, 28F })
            {
                comboBox.Items.Add(size);
            }
            comboBox.SelectedItem = 11F;
            comboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (_richTextBox.SelectionFont != null && comboBox.SelectedItem is float size)
                {
                    _richTextBox.SelectionFont = new Font(_richTextBox.SelectionFont.FontFamily, size, _richTextBox.SelectionFont.Style);
                    MarkDirty();
                }
            };
            return comboBox;
        }

        /// <summary>
        /// 工具条分隔符
        /// </summary>
        private static Control BuildSeparator()
        {
            return new Label
            {
                Text = "|",
                AutoSize = true,
                Padding = new Padding(4, 4, 4, 0),
                Tag = "theme:muted"
            };
        }
    }
}
