using Rampage.CryptedNotex.WinForm.Internal;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 分类新建/重命名窗体
    /// </summary>
    public class CategoryEditForm : NotexFormBase
    {
        private readonly TextBox _nameBox;

        /// <summary>
        /// 确认的分类名
        /// </summary>
        public string CategoryName
        {
            get;
            private set;
        } = string.Empty;

        /// <summary>
        /// 构造分类编辑窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="title">窗体标题</param>
        /// <param name="initialName">初始名称(重命名时传入)</param>
        public CategoryEditForm(IRampageApplication app, string title, string initialName) : base(app)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 240);
            DialogBody body = new DialogBody();
            Controls.Add(body);
            body.AddHeading(title);
            _nameBox = new TextBox { Text = initialName, Font = new Font("Microsoft YaHei UI", 10.5F) };
            _nameBox.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Confirm();
                }
            };
            body.AddField("分类名", _nameBox);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            Button okButton = UiFactory.Button("保存分类", "theme:accent");
            okButton.Click += (sender, e) => Confirm();
            Button cancelButton = UiFactory.Button("取消");
            cancelButton.Click += (sender, e) => { DialogResult = DialogResult.Cancel; Close(); };
            actions.Controls.Add(okButton);
            actions.Controls.Add(cancelButton);
            body.Add(actions);
            AcceptButton = okButton;
            CancelButton = cancelButton;
            Shown += (sender, e) => { body.FitForm(this); _nameBox.Focus(); _nameBox.SelectAll(); };
        }

        /// <summary>
        /// 打开时全选名称
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _nameBox.Focus();
            _nameBox.SelectAll();
        }

        /// <summary>
        /// 确定
        /// </summary>
        private void Confirm()
        {
            string name = _nameBox.Text.Trim();
            if (name.Length == 0)
            {
                ShowToast("分类名不能为空", ToastKind.Error);
                _nameBox.Focus();
                return;
            }
            CategoryName = name;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
