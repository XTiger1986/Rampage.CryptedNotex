using Rampage.CryptedNotex.WinForm.Internal;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Forms
{
    /// <summary>
    /// 附件图片预览窗体：内存解密后的图片(不落临时文件),等比缩放适应窗口
    /// </summary>
    public class ImagePreviewForm : NotexFormBase
    {
        private readonly PictureBox _pictureBox;
        private Image? _image;

        /// <summary>
        /// 构造图片预览窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="fileName">图片名(标题)</param>
        /// <param name="plainBytes">解密后的图片字节</param>
        public ImagePreviewForm(IRampageApplication app, string fileName, byte[] plainBytes) : base(app)
        {
            Text = fileName;
            ClientSize = new Size(920, 680);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(480, 360);

            Panel captionBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(16, 8, 16, 8)
            };
            Label caption = new Label
            {
                Text = fileName + "    ·    仅在内存中预览，不写临时文件",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Tag = "theme:muted"
            };
            captionBar.Controls.Add(caption);
            Controls.Add(captionBar);

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Tag = "theme:editor"
            };
            Controls.Add(_pictureBox);
            captionBar.BringToFront();

            try
            {
                // 流的生命周期须与 Image 一致,这里持有 MemoryStream 防止 GC
                MemoryStream stream = new MemoryStream(plainBytes);
                _image = Image.FromStream(stream);
                _pictureBox.Image = _image;
            }
            catch (ArgumentException)
            {
                ShowToast("无法识别的图片格式", ToastKind.Error);
            }
        }

        /// <summary>
        /// 释放图片
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
