using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 主界面按钮的统一尺寸与内边距
    /// </summary>
    internal static class UiFactory
    {
        /// <summary>
        /// 工具条/侧栏按钮：最小高度 32，随文字变宽
        /// </summary>
        /// <param name="text">按钮文字</param>
        /// <param name="themeTag">theme:accent / theme:danger，普通按钮留空</param>
        /// <returns>按钮</returns>
        public static Button Button(string text, string? themeTag = null)
        {
            Button button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36),
                Margin = new Padding(4, 4, 4, 4),
                Padding = new Padding(10, 4, 10, 4),
                UseVisualStyleBackColor = false
            };
            if (!string.IsNullOrEmpty(themeTag))
            {
                button.Tag = themeTag;
            }
            return button;
        }

        /// <summary>
        /// 打开控件双缓冲(受保护属性)
        /// </summary>
        /// <param name="control">目标控件</param>
        public static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                control,
                new object[] { true });
        }
    }
}
