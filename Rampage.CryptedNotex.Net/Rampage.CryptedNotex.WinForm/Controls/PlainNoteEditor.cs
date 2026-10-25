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
                BorderStyle = BorderStyle.None
            };
            _textBox.TextChanged += (sender, e) =>
            {
                if (!IsReadOnly)
                {
                    MarkDirty();
                }
            };
            _textBox.KeyDown += EditorKeyDown;
            Controls.Add(_textBox);
        }

        /// <inheritdoc />
        public override void LoadNote(NoteDetailDto dto)
        {
            Current = dto;
            _textBox.Text = dto.PlainContent;
            _textBox.SelectionStart = _textBox.Text.Length;
            IsDirty = false;
        }

        /// <inheritdoc />
        public override string CollectContent()
        {
            return _textBox.Text;
        }

        /// <inheritdoc />
        public override int GetWordCount()
        {
            return WordCountTool.CountNonWhitespace(_textBox.Text);
        }

        /// <inheritdoc />
        public override void SetEditorFont(string family, double size)
        {
            _textBox.Font = CreateSafeEditorFont(family, size);
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
