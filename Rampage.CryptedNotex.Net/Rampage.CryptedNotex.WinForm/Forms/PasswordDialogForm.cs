using Rampage.CryptedNotex.WinForm.Internal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 通用密码输入对话框：按字段标签列表动态生成 1~3 个密码框(解锁/设置/重置独立密码等场景共用)
    /// </summary>
    public class PasswordDialogForm : NotexFormBase
    {
        private readonly List<TextBox> _boxes = new List<TextBox>();

        /// <summary>
        /// 各字段输入值(OK 关闭后有效,与构造传入标签一一对应)
        /// </summary>
        public string[] Values
        {
            get;
            private set;
        } = Array.Empty<string>();

        /// <summary>
        /// 构造密码对话框
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="title">窗体标题</param>
        /// <param name="message">说明文字(可空)</param>
        /// <param name="fieldLabels">密码框标签(1~3 个)</param>
        public PasswordDialogForm(IRampageApplication app, string title, string message, params string[] fieldLabels) : base(app)
        {
            if (fieldLabels == null || fieldLabels.Length == 0 || fieldLabels.Length > 3)
            {
                throw new ArgumentException("密码字段须为 1~3 个", nameof(fieldLabels));
            }

            Values = new string[fieldLabels.Length];
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            int y = 16;
            if (!string.IsNullOrEmpty(message))
            {
                Label messageLabel = new Label
                {
                    Text = message,
                    Tag = "theme:muted",
                    AutoSize = true,
                    MaximumSize = new Size(340, 0),
                    Location = new Point(16, y)
                };
                Controls.Add(messageLabel);
                y = messageLabel.Bottom + 10;
            }

            foreach (string label in fieldLabels)
            {
                Label fieldLabel = new Label
                {
                    Text = label,
                    AutoSize = true,
                    Location = new Point(16, y)
                };
                Controls.Add(fieldLabel);
                y += 20;

                TextBox box = new TextBox
                {
                    Location = new Point(16, y),
                    Size = new Size(330, 26),
                    Font = new Font("Microsoft YaHei UI", 10F),
                    UseSystemPasswordChar = true
                };
                int index = _boxes.Count;
                box.KeyDown += (sender, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        if (index < _boxes.Count - 1)
                        {
                            _boxes[index + 1].Focus();
                        }
                        else
                        {
                            Confirm();
                        }
                    }
                };
                _boxes.Add(box);
                Controls.Add(box);
                y += 34;
            }

            Button okButton = new Button
            {
                Text = "确定",
                Tag = "theme:accent",
                Location = new Point(16, y + 4),
                Size = new Size(158, 32)
            };
            okButton.Click += (sender, e) => Confirm();
            Controls.Add(okButton);

            Button cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(188, y + 4),
                Size = new Size(158, 32)
            };
            cancelButton.Click += (sender, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);

            ClientSize = new Size(370, y + 48);
        }

        /// <summary>
        /// 打开时聚焦第一个输入框
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _boxes[0].Focus();
        }

        /// <summary>
        /// 确定：收集输入并关闭
        /// </summary>
        private void Confirm()
        {
            for (int i = 0; i < _boxes.Count; i++)
            {
                Values[i] = _boxes[i].Text;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
