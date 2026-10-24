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
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(340, 128);

            Label nameLabel = new Label
            {
                Text = "分类名",
                AutoSize = true,
                Location = new Point(14, 14)
            };
            Controls.Add(nameLabel);

            _nameBox = new TextBox
            {
                Text = initialName,
                Location = new Point(14, 36),
                Size = new Size(312, 26),
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            _nameBox.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Confirm();
                }
            };
            Controls.Add(_nameBox);

            Button okButton = new Button
            {
                Text = "确定",
                Tag = "theme:accent",
                Location = new Point(14, 76),
                Size = new Size(148, 32)
            };
            okButton.Click += (sender, e) => Confirm();
            Controls.Add(okButton);

            Button cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(178, 76),
                Size = new Size(148, 32)
            };
            cancelButton.Click += (sender, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);
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
                MessageBox.Show("分类名不能为空", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CategoryName = name;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
