using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.WinForm.Internal;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Controls
{
    /// <summary>
    /// 纯文本编辑器：多行文本框
    /// </summary>
    public class PlainNoteEditor : NoteEditorBase
    {
        private readonly TextBox _textBox;
        private bool _loadingNote;
        private bool _contentChanged;
        private string _loadedContent = string.Empty;

        /// <summary>
        /// 构造纯文本编辑器
        /// </summary>
        public PlainNoteEditor()
        {
            _textBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 12F),
                BorderStyle = BorderStyle.None,
                Tag = "theme:editor"
            };
            _textBox.TextChanged += (sender, e) =>
            {
                if (!_loadingNote && !IsReadOnly)
                {
                    _contentChanged = true;
                    MarkDirty();
                }
            };
            _textBox.KeyDown += EditorKeyDown;
            Panel surface = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 24),
                Tag = "theme:editor"
            };
            surface.Controls.Add(_textBox);
            Controls.Add(surface);
        }

        /// <inheritdoc />
        public override void LoadNote(NoteDetailDto dto)
        {
            Current = dto;
            _loadedContent = dto.PlainContent;
            _loadingNote = true;
            try
            {
                // WinForms 多行 TextBox 需要 CRLF 才能正确显示 LF-only 的历史文本。
                // 原始字符串仍由 _loadedContent 保留，未编辑正文时不会改写换行。
                _textBox.Text = dto.PlainContent.ReplaceLineEndings("\r\n");
                _textBox.SelectionStart = _textBox.Text.Length;
            }
            finally
            {
                _loadingNote = false;
            }
            _contentChanged = false;
            MarkSaved();
        }

        /// <inheritdoc />
        public override string CollectContent()
        {
            return _contentChanged ? _textBox.Text : _loadedContent;
        }

        /// <inheritdoc />
        public override int GetWordCount()
        {
            return WordCountTool.CountNonWhitespace(_textBox.Text);
        }

        /// <inheritdoc />
        public override void SetEditorFont(string family, double size)
        {
            bool loading = _loadingNote;
            _loadingNote = true;
            try
            {
                _textBox.Font = CreateSafeEditorFont(family, size);
            }
            finally
            {
                _loadingNote = loading;
            }
        }

        /// <inheritdoc />
        public override void SetReadOnly(bool readOnly)
        {
            base.SetReadOnly(readOnly);
            _textBox.ReadOnly = readOnly;
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
    }
}
