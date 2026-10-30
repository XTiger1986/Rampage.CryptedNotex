using Rampage.CryptedNotex.WinForm.Internal.Themes;
using Rampage.Model;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 模态进度执行器：显示进度窗，后台执行带 IProgress 的长任务(如重加密)，期间阻断界面其他操作
    /// </summary>
    internal static class ProgressRunner
    {
        /// <summary>
        /// 模态执行长任务并返回结果
        /// </summary>
        /// <param name="owner">宿主窗体</param>
        /// <param name="title">进度窗标题</param>
        /// <param name="work">后台任务(在 UI 线程外的线程执行,入参为进度上报)</param>
        /// <returns>任务结果；后台抛异常时返回异常消息的失败 R</returns>
        public static R Run(Form owner, string title, Func<IProgress<int>, R> work)
        {
            ProgressDialog dialog = new ProgressDialog(title);
            dialog.Icon = owner.Icon;
            R result = R.Failure("操作未完成");
            dialog.Shown += async (sender, e) =>
            {
                IProgress<int> progress = new Progress<int>(value => dialog.Report(value));
                try
                {
                    result = await UiWork.RunAsync(() => work(progress));
                }
                catch (Exception ex)
                {
                    result = R.Failure(ex.Message);
                }

                if (!dialog.IsDisposed)
                {
                    dialog.BeginInvoke(new Action(dialog.Complete));
                }
            };
            ThemeManager.Apply(dialog);
            try
            {
                dialog.ShowDialog(owner);
            }
            finally
            {
                // Close 会在模态循环里重入到这里，等宿主消息循环下一拍再释放
                if (owner.IsHandleCreated && !owner.IsDisposed)
                {
                    ProgressDialog pending = dialog;
                    owner.BeginInvoke(new Action(() =>
                    {
                        if (!pending.IsDisposed)
                        {
                            pending.Dispose();
                        }
                    }));
                }
                else if (!dialog.IsDisposed)
                {
                    dialog.Dispose();
                }
            }
            return result;
        }

        /// <summary>
        /// 不可中途关掉的进度窗，避免重加密做到一半被用户关掉
        /// </summary>
        private sealed class ProgressDialog : Form
        {
            private readonly ProgressBar _bar;
            private readonly Label _percent;
            private bool _completed;

            /// <summary>
            /// 构造进度窗
            /// </summary>
            /// <param name="title">标题</param>
            public ProgressDialog(string title)
            {
                Text = title;
                Font = new Font("Microsoft YaHei UI", 9.5F);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                ControlBox = false;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(440, 156);

                Label heading = new Label
                {
                    Text = "正在处理，请稍候",
                    Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(24, 20)
                };
                Controls.Add(heading);

                Label hint = new Label
                {
                    Text = "完成前不能关闭此窗口，以免加密做到一半。",
                    Tag = "theme:muted",
                    AutoSize = true,
                    Location = new Point(24, 48)
                };
                Controls.Add(hint);

                _bar = new ProgressBar
                {
                    Location = new Point(24, 84),
                    Size = new Size(392, 16),
                    Minimum = 0,
                    Maximum = 100,
                    Style = ProgressBarStyle.Continuous
                };
                Controls.Add(_bar);

                _percent = new Label
                {
                    Text = "0%",
                    Tag = "theme:muted",
                    AutoSize = true,
                    Location = new Point(24, 112)
                };
                Controls.Add(_percent);
            }

            /// <summary>
            /// 更新百分比
            /// </summary>
            /// <param name="value">0–100</param>
            public void Report(int value)
            {
                int percent = Math.Min(Math.Max(value, 0), 100);
                _bar.Value = percent;
                _percent.Text = percent + "%";
            }

            /// <summary>
            /// 任务结束，允许关闭
            /// </summary>
            public void Complete()
            {
                _completed = true;
                DialogResult = DialogResult.OK;
                Close();
            }

            /// <summary>
            /// 任务未完成时拒绝关闭
            /// </summary>
            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (!_completed)
                {
                    e.Cancel = true;
                }
                base.OnFormClosing(e);
            }
        }
    }
}
