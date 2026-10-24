using Rampage.CryptedNotex.Model.Dto;
using System;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Rampage.CryptedNotex.WinForm.Controls
{
    /// <summary>
    /// 笔记编辑器基类：当前笔记/脏标记/防抖自动保存/Ctrl+S 立即保存
    /// </summary>
    /// <remarks>
    /// 保存动作由宿主(主窗体)执行：本控件只通过 SaveRequested 请求保存并交出内容(CollectContent)。
    /// 切换笔记/关窗/锁定前宿主必须调用 FlushPendingSave。
    /// </remarks>
    public abstract class NoteEditorBase : UserControl
    {
        /// <summary>
        /// 自动保存防抖间隔(毫秒)
        /// </summary>
        protected const int AUTOSAVE_DELAY_MS = 2000;

        private Timer? _autosaveTimer;

        /// <summary>
        /// 当前编辑的笔记详情
        /// </summary>
        public NoteDetailDto? Current
        {
            get;
            protected set;
        }

        /// <summary>
        /// 是否有未保存修改
        /// </summary>
        public bool IsDirty
        {
            get;
            protected set;
        }

        /// <summary>
        /// 请求宿主立即保存(自动保存到点或 Ctrl+S)
        /// </summary>
        public event EventHandler? SaveRequested;

        /// <summary>
        /// 内容变化(宿主可更新字数等状态)
        /// </summary>
        public event EventHandler? ContentChanged;

        /// <summary>
        /// 加载笔记内容
        /// </summary>
        /// <param name="dto">笔记详情(含解密后明文)</param>
        public abstract void LoadNote(NoteDetailDto dto);

        /// <summary>
        /// 收集当前正文明文(富文本为 RTF 文档)
        /// </summary>
        /// <returns>正文明文</returns>
        public abstract string CollectContent();

        /// <summary>
        /// 当前字数(非空白字符数)
        /// </summary>
        /// <returns>字数</returns>
        public abstract int GetWordCount();

        /// <summary>
        /// 设置编辑器字体(用户偏好)
        /// </summary>
        /// <param name="family">字体族</param>
        /// <param name="size">字号</param>
        public virtual void SetEditorFont(string family, double size)
        {
        }

        /// <summary>
        /// 宿主通知标题已修改(标题由主窗体持有,同样计入待保存)
        /// </summary>
        public void NotifyTitleChanged()
        {
            if (Current != null)
            {
                MarkDirty();
            }
        }

        /// <summary>
        /// 按用户偏好安全构造编辑器字体：钳制字号到 [6,72] 并对非法/未安装字体族回退 Consolas
        /// (防御历史脏数据,如 editor_font_size=0 会使 Font 构造直接抛异常)
        /// </summary>
        /// <param name="family">字体族(可空/未安装时回退)</param>
        /// <param name="size">字号(超范围时回退 12)</param>
        /// <returns>可安全使用的字体</returns>
        protected static Font CreateSafeEditorFont(string? family, double size)
        {
            float safeSize = size < 6 || size > 72 ? 12F : (float)size;
            string safeFamily = string.IsNullOrWhiteSpace(family) ? "Consolas" : family;
            try
            {
                return new Font(safeFamily, safeSize);
            }
            catch (ArgumentException)
            {
                // 字体族未安装等情形
                return new Font("Consolas", safeSize);
            }
        }

        /// <summary>
        /// 标记内容已变化：置脏并重启防抖计时
        /// </summary>
        protected void MarkDirty()
        {
            IsDirty = true;
            _autosaveTimer ??= CreateTimer();
            _autosaveTimer.Stop();
            _autosaveTimer.Start();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 宿主保存成功后清除脏标记
        /// </summary>
        public void MarkSaved()
        {
            IsDirty = false;
            _autosaveTimer?.Stop();
        }

        /// <summary>
        /// 有待保存内容时立即请求宿主保存(切换笔记/关窗/锁定前由宿主调用)
        /// </summary>
        public void FlushPendingSave()
        {
            if (!IsDirty)
            {
                return;
            }

            _autosaveTimer?.Stop();
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 立即请求保存(Ctrl+S)
        /// </summary>
        protected void RequestSaveNow()
        {
            _autosaveTimer?.Stop();
            if (IsDirty)
            {
                SaveRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 创建防抖计时器
        /// </summary>
        private Timer CreateTimer()
        {
            Timer timer = new Timer
            {
                Interval = AUTOSAVE_DELAY_MS
            };
            timer.Tick += (sender, e) =>
            {
                timer.Stop();
                if (IsDirty)
                {
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            return timer;
        }

        /// <summary>
        /// 释放计时器
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _autosaveTimer != null)
            {
                _autosaveTimer.Dispose();
                _autosaveTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}
