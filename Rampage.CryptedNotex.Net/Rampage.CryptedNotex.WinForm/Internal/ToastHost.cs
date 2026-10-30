using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 提示种类
    /// </summary>
    public enum ToastKind
    {
        /// <summary>
        /// 一般说明
        /// </summary>
        Info,

        /// <summary>
        /// 成功
        /// </summary>
        Success,

        /// <summary>
        /// 失败或无法继续
        /// </summary>
        Error
    }

    /// <summary>
    /// 非模态提示条：叠在窗体底部，自动消失，点击可提前关闭。不阻断操作。
    /// </summary>
    internal sealed class ToastHost
    {
        private const int TOAST_WIDTH = 380;
        private const int MAX_TOASTS = 3;

        private readonly Form _form;
        private readonly List<ToastCard> _cards = new List<ToastCard>();

        /// <summary>
        /// 绑定宿主窗体
        /// </summary>
        /// <param name="form">提示所在窗体</param>
        public ToastHost(Form form)
        {
            _form = form;
            _form.Resize += OnFormResize;
            _form.Disposed += OnFormDisposed;
            ThemeManager.CurrentChanged += OnThemeChanged;
        }

        /// <summary>
        /// 主题切换后重刷色条。推迟到下一拍，等窗体自己的 Apply 先走完。
        /// </summary>
        private void OnThemeChanged(Theme theme)
        {
            if (_form.IsDisposed)
            {
                return;
            }

            if (_form.IsHandleCreated)
            {
                try
                {
                    _form.BeginInvoke(new Action(RestyleCards));
                }
                catch (InvalidOperationException)
                {
                    // 句柄正在销毁
                }
                return;
            }

            RestyleCards();
        }

        /// <summary>
        /// 按当前主题重刷已显示的卡片
        /// </summary>
        private void RestyleCards()
        {
            if (_form.IsDisposed)
            {
                return;
            }

            foreach (ToastCard card in _cards)
            {
                if (!card.IsDisposed)
                {
                    card.ApplyColors();
                }
            }
        }

        /// <summary>
        /// 尺寸变化时重新排到底部
        /// </summary>
        private void OnFormResize(object? sender, EventArgs e)
        {
            Relayout();
        }

        /// <summary>
        /// 窗体关闭后解除主题订阅
        /// </summary>
        private void OnFormDisposed(object? sender, EventArgs e)
        {
            ThemeManager.CurrentChanged -= OnThemeChanged;
            _form.Resize -= OnFormResize;
            _form.Disposed -= OnFormDisposed;
        }

        /// <summary>
        /// 距客户区底边的空隙(主窗体让出状态条)
        /// </summary>
        public int BottomInset
        {
            get;
            set;
        } = 18;

        /// <summary>
        /// 显示一条提示。相同文案会刷新停留时间，而不是再叠一条。
        /// </summary>
        /// <param name="message">正文</param>
        /// <param name="kind">种类</param>
        public void ShowToast(string message, ToastKind kind)
        {
            if (_form.IsDisposed)
            {
                return;
            }

            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                if (_cards[i].Matches(message, kind))
                {
                    _cards[i].Restart();
                    Relayout();
                    return;
                }
            }

            ToastCard card = new ToastCard(message, kind);
            card.Dismissed += (sender, e) => Remove(card);
            _form.Controls.Add(card);
            card.BringToFront();
            ThemeManager.Apply(card);
            card.ApplyColors();
            _cards.Add(card);

            while (_cards.Count > MAX_TOASTS)
            {
                _cards[0].Dismiss();
            }
            Relayout();
        }

        /// <summary>
        /// 按当前客户区重新排到底部居中
        /// </summary>
        public void Relayout()
        {
            if (_form.IsDisposed)
            {
                return;
            }

            int y = _form.ClientSize.Height - BottomInset;
            int x = Math.Max(12, (_form.ClientSize.Width - TOAST_WIDTH) / 2);
            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                ToastCard card = _cards[i];
                if (card.IsDisposed)
                {
                    continue;
                }
                y -= card.Height;
                card.SetBounds(x, Math.Max(8, y), TOAST_WIDTH, card.Height);
                card.BringToFront();
                y -= 8;
            }
        }

        /// <summary>
        /// 移除已关闭的卡片
        /// </summary>
        private void Remove(ToastCard card)
        {
            _cards.Remove(card);
            if (!card.IsDisposed)
            {
                _form.Controls.Remove(card);
                card.Dispose();
            }
            Relayout();
        }

        /// <summary>
        /// 单条提示卡片
        /// </summary>
        private sealed class ToastCard : Panel
        {
            private readonly ToastKind _kind;
            private readonly string _message;
            private readonly System.Windows.Forms.Timer _timer;
            private readonly Label _caption;
            private readonly Label _body;

            /// <summary>
            /// 请求宿主移除
            /// </summary>
            public event EventHandler? Dismissed;

            /// <summary>
            /// 构造卡片并开始倒计时
            /// </summary>
            public ToastCard(string message, ToastKind kind)
            {
                _kind = kind;
                _message = message;
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                Font = new Font("Microsoft YaHei UI", 9F);
                Padding = new Padding(16, 8, 12, 8);
                Tag = "theme:panelAlt";

                _caption = new Label
                {
                    Text = Caption(kind),
                    Dock = DockStyle.Top,
                    Height = 18,
                    Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                _body = new Label
                {
                    Text = message,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = false
                };
                Controls.Add(_body);
                Controls.Add(_caption);

                int textWidth = TOAST_WIDTH - 36;
                Size measured = TextRenderer.MeasureText(message, Font, new Size(textWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                Height = Math.Max(58, measured.Height + 36);

                Click += (sender, e) => Dismiss();
                _caption.Click += (sender, e) => Dismiss();
                _body.Click += (sender, e) => Dismiss();

                _timer = new System.Windows.Forms.Timer
                {
                    Interval = kind == ToastKind.Error ? 5600 : kind == ToastKind.Success ? 2600 : 3400
                };
                _timer.Tick += (sender, e) => Dismiss();
                _timer.Start();
            }

            /// <summary>
            /// 是否为同一条提示
            /// </summary>
            public bool Matches(string message, ToastKind kind)
            {
                return _kind == kind && _message == message;
            }

            /// <summary>
            /// 重新计算停留时间
            /// </summary>
            public void Restart()
            {
                _timer.Stop();
                _timer.Start();
                BringToFront();
            }

            /// <summary>
            /// 关闭
            /// </summary>
            public void Dismiss()
            {
                if (_timer.Enabled)
                {
                    _timer.Stop();
                }
                Dismissed?.Invoke(this, EventArgs.Empty);
            }

            /// <summary>
            /// 释放计时器
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _timer.Dispose();
                }
                base.Dispose(disposing);
            }

            /// <summary>
            /// 主题应用之后补上色条颜色。面板底色交给 theme:panelAlt。
            /// </summary>
            public void ApplyColors()
            {
                Color back = ThemeManager.GetColor("panelAltBack");
                BackColor = back;
                _caption.BackColor = back;
                _body.BackColor = back;
                _caption.ForeColor = StripeColor(_kind);
                _body.ForeColor = ThemeManager.GetColor("text");
                Invalidate();
            }

            /// <summary>
            /// 左侧色条 + 细边框
            /// </summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                Color back = ThemeManager.GetColor("panelAltBack");
                Color border = ThemeManager.GetColor("border");
                Color stripe = StripeColor(_kind);
                using (SolidBrush backBrush = new SolidBrush(back))
                using (SolidBrush stripeBrush = new SolidBrush(stripe))
                using (Pen pen = new Pen(border))
                {
                    e.Graphics.FillRectangle(backBrush, ClientRectangle);
                    e.Graphics.FillRectangle(stripeBrush, 0, 0, 4, Height);
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            /// <summary>
            /// 种类标题
            /// </summary>
            private static string Caption(ToastKind kind)
            {
                return kind switch
                {
                    ToastKind.Success => "完成",
                    ToastKind.Error => "失败",
                    _ => "提示"
                };
            }

            /// <summary>
            /// 色条颜色。成功色不在主题表里，用固定绿，深浅底上都看得清。
            /// </summary>
            private static Color StripeColor(ToastKind kind)
            {
                return kind switch
                {
                    ToastKind.Success => Color.FromArgb(46, 160, 90),
                    ToastKind.Error => ThemeManager.GetColor("danger"),
                    _ => ThemeManager.GetColor("accent")
                };
            }
        }
    }
}
