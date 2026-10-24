using Rampage.Model;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 模态进度执行器：显示进度窗,后台执行带 IProgress 的长任务(如重加密),期间阻断界面其他操作
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
            Form dialog = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedSingle,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(360, 110),
                ShowInTaskbar = false
            };
            Label statusLabel = new Label
            {
                Text = "正在处理…",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };
            ProgressBar progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100
            };
            dialog.Controls.Add(progressBar);
            dialog.Controls.Add(statusLabel);
            Themes.ThemeManager.Apply(dialog);

            IProgress<int> progress = new Progress<int>(value => progressBar.Value = Math.Min(Math.Max(value, 0), 100));
            R result = R.Failure();
            Task task = Task.Run(() =>
            {
                result = work(progress);
            });

            // 模态等待任务完成(消息循环保持进度刷新)
            while (!task.IsCompleted)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(30);
            }
            dialog.Dispose();
            return result;
        }
    }
}
