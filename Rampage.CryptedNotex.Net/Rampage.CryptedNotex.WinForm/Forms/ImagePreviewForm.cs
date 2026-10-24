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
            Text = "图片预览 - " + fileName;
            ClientSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterParent;

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(60, 60, 60),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Controls.Add(_pictureBox);

            try
            {
                // 流的生命周期须与 Image 一致,这里持有 MemoryStream 防止 GC
                MemoryStream stream = new MemoryStream(plainBytes);
                _image = Image.FromStream(stream);
                _pictureBox.Image = _image;
            }
            catch (ArgumentException)
            {
                MessageBox.Show("无法识别的图片格式", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
