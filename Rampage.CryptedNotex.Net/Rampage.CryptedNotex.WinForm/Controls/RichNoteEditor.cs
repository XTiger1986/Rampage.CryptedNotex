using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Controls
{
    /// <summary>
    /// 富文本编辑器：RichTextBox(RTF 格式)+主题化格式工具条(加粗/斜体/下划线/删除线/项目符号/文字颜色/字号)
    /// </summary>
    /// <remarks>
    /// 工具条按钮 Tag=theme:tool(悬停/按下反馈,无边框),条带 Tag=theme:toolbar,分组竖线 Tag=theme:rule;
    /// 正文面 Tag=theme:editor(编辑器底/文本随主题)。加载与主题切换时做对比度自适应:
    /// 全文单色且与底色明暗相同时(如浅色主题下写的旧笔记拿到深色主题),整体重着色为当前主题文本色。
    /// </remarks>
    public class RichNoteEditor : NoteEditorBase
    {
        private readonly Panel _toolbar;
        private readonly RichTextBox _richTextBox;
        private readonly Button _boldButton;
        private readonly Button _italicButton;
        private readonly Button _underlineButton;
        private readonly Button _strikeButton;
        private readonly Button _bulletButton;
        private readonly Button _colorButton;
        private readonly ComboBox _fontSizeBox;
        private readonly ToolTip _toolTips;
        private bool _updatingToolbar;

        /// <summary>
        /// 构造富文本编辑器
        /// </summary>
        public RichNoteEditor()
        {
            _toolTips = new ToolTip();

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _toolbar = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 42,
                Tag = "theme:toolbar",
                Margin = new Padding(0)
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(8, 7, 8, 6)
            };
            _boldButton = BuildStyleButton("B", FontStyle.Bold, "加粗 (Ctrl+B)");
            _italicButton = BuildStyleButton("I", FontStyle.Italic, "斜体 (Ctrl+I)");
            _underlineButton = BuildStyleButton("U", FontStyle.Underline, "下划线 (Ctrl+U)");
            _strikeButton = BuildStyleButton("S", FontStyle.Strikeout, "删除线");
            _bulletButton = BuildButton("≡", "项目符号", new Font("Microsoft YaHei UI", 11F));
            _colorButton = BuildButton("A", "文字颜色", new Font("Microsoft YaHei UI", 10.5F, FontStyle.Underline));
            _fontSizeBox = BuildFontSizeSelector();
            flow.Controls.Add(_boldButton);
            flow.Controls.Add(_italicButton);
            flow.Controls.Add(_underlineButton);
            flow.Controls.Add(_strikeButton);
            flow.Controls.Add(BuildRule());
            flow.Controls.Add(_bulletButton);
            flow.Controls.Add(BuildRule());
            flow.Controls.Add(_colorButton);
            flow.Controls.Add(BuildRule());
            flow.Controls.Add(WrapVerticallyCentered(_fontSizeBox));

            // 底部分隔线后加(Fill 先、边缘后,停靠次序才正确)
            _toolbar.Controls.Add(flow);
            _toolbar.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                Tag = "theme:rule",
                Margin = new Padding(0)
            });

            Panel surface = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 10, 14, 12),
                Tag = "theme:editor",
                Margin = new Padding(0)
            };
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
            _richTextBox.SelectionChanged += (sender, e) => UpdateToolbarState();

            // 按钮事件在 _richTextBox 就绪后挂接(项目符号/文字颜色)
            _bulletButton.Click += (sender, e) =>
            {
                if (IsReadOnly)
                {
                    return;
                }
                _richTextBox.SelectionBullet = !_richTextBox.SelectionBullet;
                MarkDirty();
                UpdateToolbarState();
            };
            _colorButton.Click += (sender, e) =>
            {
                if (IsReadOnly)
                {
                    return;
                }
                using (ColorDialog dialog = new ColorDialog())
                {
                    if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        _richTextBox.SelectionColor = dialog.Color;
                        MarkDirty();
                        UpdateToolbarState();
                    }
                }
            };
            surface.Controls.Add(_richTextBox);

            layout.Controls.Add(_toolbar, 0, 0);
            layout.Controls.Add(surface, 0, 1);
            Controls.Add(layout);

            ThemeManager.CurrentChanged += OnThemeChanged;
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
            NormalizeContentColor();
            UpdateToolbarState();
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
        /// 释放主题订阅与提示组件
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.CurrentChanged -= OnThemeChanged;
                _toolTips.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Ctrl+S 保存；Ctrl+B/I/U 快捷设置样式
        /// </summary>
        private void EditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true;
                RequestSaveNow();
            }
            else if (e.Control && e.KeyCode == Keys.B)
            {
                e.SuppressKeyPress = true;
                ToggleStyle(FontStyle.Bold);
            }
            else if (e.Control && e.KeyCode == Keys.I)
            {
                e.SuppressKeyPress = true;
                ToggleStyle(FontStyle.Italic);
            }
            else if (e.Control && e.KeyCode == Keys.U)
            {
                e.SuppressKeyPress = true;
                ToggleStyle(FontStyle.Underline);
            }
        }

        /// <summary>
        /// 构造字体样式切换按钮(字面即样式：B 粗体/I 斜体/U 下划线/S 删除线)
        /// </summary>
        private Button BuildStyleButton(string text, FontStyle style, string tip)
        {
            Button button = BuildButton(text, tip, new Font("Microsoft YaHei UI", 10F, style));
            button.Click += (sender, e) => ToggleStyle(style);
            return button;
        }

        /// <summary>
        /// 构造工具条按钮(主题化 Tag,悬停/按下反馈由 ThemeManager 注入)
        /// </summary>
        private Button BuildButton(string text, string tip, Font font)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(32, 28),
                Font = font,
                Tag = "theme:tool",
                Margin = new Padding(1, 0, 1, 0),
                TabStop = false
            };
            _toolTips.SetToolTip(button, tip);
            return button;
        }

        /// <summary>
        /// 工具条分组竖线(1 像素,主题边框色)
        /// </summary>
        private static Control BuildRule()
        {
            return new Panel
            {
                Size = new Size(1, 18),
                Margin = new Padding(7, 5, 7, 5),
                Tag = "theme:rule",
                TabStop = false
            };
        }

        /// <summary>
        /// 让字号下拉在流式布局中垂直居中(按钮高 28,下拉高约 25)
        /// </summary>
        private static Control WrapVerticallyCentered(Control inner)
        {
            Panel wrapper = new Panel
            {
                Size = new Size(56, 28),
                Padding = new Padding(0, 3, 0, 0),
                Margin = new Padding(1, 0, 1, 0)
            };
            inner.Dock = DockStyle.Fill;
            wrapper.Controls.Add(inner);
            return wrapper;
        }

        /// <summary>
        /// 切换选中文本的字体样式
        /// </summary>
        private void ToggleStyle(FontStyle style)
        {
            if (IsReadOnly || _richTextBox.SelectionFont == null)
            {
                return;
            }

            FontStyle current = _richTextBox.SelectionFont.Style;
            FontStyle target = (current & style) == style ? current & ~style : current | style;
            _richTextBox.SelectionFont = new Font(_richTextBox.SelectionFont, target);
            MarkDirty();
            UpdateToolbarState();
        }

        /// <summary>
        /// 字号下拉(选择即应用到选区)
        /// </summary>
        private ComboBox BuildFontSizeSelector()
        {
            ComboBox comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 54,
                Font = new Font("Microsoft YaHei UI", 9F),
                Tag = "theme:tool",
                FlatStyle = FlatStyle.Flat
            };
            foreach (float size in new float[] { 9F, 10F, 11F, 12F, 14F, 16F, 18F, 22F, 28F })
            {
                comboBox.Items.Add(size);
            }
            comboBox.SelectedIndex = 2;
            comboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (_updatingToolbar || IsReadOnly)
                {
                    return;
                }
                if (_richTextBox.SelectionFont != null && comboBox.SelectedItem is float size)
                {
                    _richTextBox.SelectionFont = new Font(_richTextBox.SelectionFont.FontFamily, size, _richTextBox.SelectionFont.Style);
                    MarkDirty();
                }
            };
            _toolTips.SetToolTip(comboBox, "字号");
            return comboBox;
        }

        /// <summary>
        /// 按当前选区刷新工具条状态：B/I/U/S/项目符号的按下态、颜色指示、字号同步
        /// </summary>
        private void UpdateToolbarState()
        {
            if (_updatingToolbar)
            {
                return;
            }

            _updatingToolbar = true;
            try
            {
                Font? font = _richTextBox.SelectionFont;
                SetToggle(_boldButton, font != null && font.Bold);
                SetToggle(_italicButton, font != null && font.Italic);
                SetToggle(_underlineButton, font != null && font.Underline);
                SetToggle(_strikeButton, font != null && font.Strikeout);
                SetToggle(_bulletButton, _richTextBox.SelectionBullet);

                // 颜色指示："A" 显示当前选区颜色；混色/无色/与底色难分辨时退回主题文本色
                Color toolbarBack = ThemeManager.GetColor("panelBack");
                Color fallback = ThemeManager.GetColor("text");
                Color shown = fallback;
                Color selection = _richTextBox.SelectionColor;
                if (selection != Color.Empty && !selection.IsSystemColor)
                {
                    shown = Math.Abs(selection.GetBrightness() - toolbarBack.GetBrightness()) < 0.25f ? fallback : selection;
                }
                _colorButton.ForeColor = shown;

                if (font != null)
                {
                    float current = font.Size;
                    float nearest = 11F;
                    float delta = float.MaxValue;
                    foreach (object? item in _fontSizeBox.Items)
                    {
                        if (item is float size && Math.Abs(size - current) < delta)
                        {
                            delta = Math.Abs(size - current);
                            nearest = size;
                        }
                    }
                    _fontSizeBox.SelectedItem = nearest;
                }
            }
            finally
            {
                _updatingToolbar = false;
            }
        }

        /// <summary>
        /// 设置工具条按钮的"按下"视觉(选中底色)或常态(透明融于工具条)
        /// </summary>
        private static void SetToggle(Button button, bool active)
        {
            button.BackColor = active ? ThemeManager.GetColor("selectionBack") : ThemeManager.GetColor("panelBack");
            button.ForeColor = ThemeManager.GetColor("text");
        }

        /// <summary>
        /// 正文颜色随主题：空文档把插入色设为主题文本色；全文单色且与底色明暗相同时整体重着色
        /// (旧笔记在不同明暗主题间迁移时的可读性保护)
        /// </summary>
        private void NormalizeContentColor()
        {
            Color back = ThemeManager.GetColor("editorBack");
            Color fore = ThemeManager.GetColor("editorText");
            _richTextBox.BackColor = back;
            _richTextBox.ForeColor = fore;

            if (_richTextBox.TextLength == 0)
            {
                _richTextBox.SelectionStart = 0;
                _richTextBox.SelectionLength = 0;
                _richTextBox.SelectionColor = fore;
                return;
            }

            int selectionStart = _richTextBox.SelectionStart;
            int selectionLength = _richTextBox.SelectionLength;
            _richTextBox.SelectAll();
            Color uniform = _richTextBox.SelectionColor;
            if (uniform != Color.Empty && !uniform.IsSystemColor && uniform.ToArgb() != fore.ToArgb())
            {
                bool textDark = uniform.GetBrightness() < 0.5f;
                bool backDark = back.GetBrightness() < 0.5f;
                if (textDark == backDark)
                {
                    // 全文单色且在当前底色上不可读：重着色为主题文本色
                    _richTextBox.SelectionColor = fore;
                }
            }
            _richTextBox.Select(selectionStart, selectionLength);
        }

        /// <summary>
        /// 主题切换：延后到整棵控件树重刷之后再校正正文颜色与工具条状态
        /// </summary>
        private void OnThemeChanged(Theme theme)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    NormalizeContentColor();
                    UpdateToolbarState();
                }));
            }
            else
            {
                NormalizeContentColor();
                UpdateToolbarState();
            }
        }
    }
}
