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
    /// 正文面 Tag=theme:document，保留原始 RTF 的文字颜色，按文档文字选择稳定的明暗纸张。
    /// 主题仅影响工具条和周边界面；未编辑正文时原样交还加载的字符串，避免查看、改标题或换主题改写历史文档。
    /// </remarks>
    public class RichNoteEditor : NoteEditorBase
    {
        private readonly Panel _toolbar;
        private readonly RichTextBox _richTextBox;
        private readonly Panel _documentSurface;
        private readonly Button _boldButton;
        private readonly Button _italicButton;
        private readonly Button _underlineButton;
        private readonly Button _strikeButton;
        private readonly Button _bulletButton;
        private readonly Button _colorButton;
        private readonly ComboBox _fontSizeBox;
        private readonly ToolTip _toolTips;
        private bool _updatingToolbar;
        private bool _loadingDocument;
        private bool _documentChanged;
        private string _loadedContent = string.Empty;

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
                Height = 46,
                Tag = "theme:toolbar",
                Margin = new Padding(0)
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(12, 9, 12, 8)
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

            _documentSurface = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 24),
                BackColor = Color.White,
                Tag = "theme:document",
                Margin = new Padding(0)
            };
            _richTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 11F),
                AcceptsTab = true,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(32, 34, 38),
                Tag = "theme:document"
            };
            _richTextBox.TextChanged += (sender, e) =>
            {
                if (!_loadingDocument && !IsReadOnly)
                {
                    MarkDocumentDirty();
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
                MarkDocumentDirty();
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
                        MarkDocumentDirty();
                        UpdateToolbarState();
                    }
                }
            };
            _documentSurface.Controls.Add(_richTextBox);

            layout.Controls.Add(_toolbar, 0, 0);
            layout.Controls.Add(_documentSurface, 0, 1);
            Controls.Add(layout);

            ThemeManager.CurrentChanged += OnThemeChanged;
        }

        /// <inheritdoc />
        public override void LoadNote(NoteDetailDto dto)
        {
            Current = dto;
            _loadedContent = dto.PlainContent;
            _loadingDocument = true;
            try
            {
                try
                {
                    _richTextBox.Rtf = dto.PlainContent;
                }
                catch (ArgumentException)
                {
                    // 历史/异常数据不是合法 RTF 时退回纯文本展示；未编辑时仍保留原始内容。
                    _richTextBox.Text = dto.PlainContent;
                }
                ApplyDocumentCanvas();
            }
            finally
            {
                _loadingDocument = false;
            }
            _documentChanged = false;
            UpdateToolbarState();
            MarkSaved();
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
            // RichEdit 会规范化 RTF；仅在用户实际修改正文/格式后接受该序列化结果。
            return _documentChanged ? _richTextBox.Rtf ?? string.Empty : _loadedContent;
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
            MarkDocumentDirty();
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
                    MarkDocumentDirty();
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
            if (_updatingToolbar || _loadingDocument)
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
            ChromePainter.SetButtonActive(button, active);
            button.ForeColor = ThemeManager.GetColor("text");
        }

        /// <summary>
        /// 用户修改正文或格式后，才允许重新序列化 RTF 并启动自动保存。
        /// </summary>
        private void MarkDocumentDirty()
        {
            _documentChanged = true;
            MarkDirty();
        }

        /// <summary>
        /// 按文档现有文字选择纸张明暗，兼容历史深色主题写入的浅色 RTF。
        /// 仅改变控件背景，绝不改动选中文字的颜色、字体、正文或持久化格式。
        /// </summary>
        private void ApplyDocumentCanvas()
        {
            int start = _richTextBox.SelectionStart;
            int length = _richTextBox.SelectionLength;
            double lightInk = 0;
            int samples = Math.Min(_richTextBox.TextLength, 128);
            for (int sample = 0; sample < samples; sample++)
            {
                int position = (int)((long)sample * _richTextBox.TextLength / samples);
                _richTextBox.Select(position, 1);
                Color color = _richTextBox.SelectionColor;
                lightInk += color.GetBrightness();
            }
            Color paper = samples > 0 && lightInk / samples > 0.55
                ? Color.FromArgb(30, 34, 41)
                : Color.White;
            _documentSurface.BackColor = paper;
            _richTextBox.BackColor = paper;
            _richTextBox.Select(start, length);
        }

        /// <summary>
        /// 主题只刷新工具条状态，绝不设置正文前景色或选区格式。
        /// </summary>
        private void OnThemeChanged(Theme theme)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed)
                    {
                        UpdateToolbarState();
                    }
                }));
            }
            else
            {
                UpdateToolbarState();
            }
        }
    }
}
