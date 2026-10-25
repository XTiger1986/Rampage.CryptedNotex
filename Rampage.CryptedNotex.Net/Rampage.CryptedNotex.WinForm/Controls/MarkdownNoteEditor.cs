using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.Model.Dto;
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Rampage.CryptedNotex.WinForm.Controls
{
    /// <summary>
    /// Markdown 编辑器：等宽编辑 + WebView2 实时预览(本地离线资源)；缺 WebView2 Runtime 时折叠预览仅保留编辑
    /// </summary>
    public class MarkdownNoteEditor : NoteEditorBase
    {
        /// <summary>
        /// 预览资源虚拟域名(映射到 Assets\Markdown 目录)
        /// </summary>
        private const string ASSETS_HOST = "notex-assets";

        private readonly TextBox _textBox;
        private readonly SplitContainer _splitter;
        private readonly Panel _previewFallbackPanel;
        private WebView2? _webView;
        private Timer? _previewTimer;
        private bool _previewReady;
        private bool _webViewAvailable;

        /// <summary>
        /// 构造 Markdown 编辑器
        /// </summary>
        public MarkdownNoteEditor()
        {
            _textBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 12F),
                BorderStyle = BorderStyle.None
            };
            _textBox.TextChanged += (sender, e) =>
            {
                if (!IsReadOnly)
                {
                    MarkDirty();
                }
                RestartPreviewTimer();
            };
            _textBox.KeyDown += EditorKeyDown;

            _previewFallbackPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            Label fallbackLabel = new Label
            {
                Text = "未检测到 WebView2 Runtime，Markdown 仅编辑可用。\n安装后重启程序即可启用预览。",
                Tag = "theme:muted",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _previewFallbackPanel.Controls.Add(fallbackLabel);

            _splitter = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 520
            };
            _splitter.Panel1.Controls.Add(_textBox);
            _splitter.Panel2.Controls.Add(_previewFallbackPanel);
            Controls.Add(_splitter);

            Internal.Themes.ThemeManager.CurrentChanged += OnThemeChanged;
        }

        /// <inheritdoc />
        public override void LoadNote(NoteDetailDto dto)
        {
            Current = dto;
            _textBox.Text = dto.PlainContent;
            _textBox.SelectionStart = _textBox.Text.Length;
            IsDirty = false;
            RenderPreview();
        }

        /// <inheritdoc />
        public override string CollectContent()
        {
            return _textBox.Text;
        }

        /// <inheritdoc />
        public override int GetWordCount()
        {
            return WordCountTool.CountNonWhitespace(_textBox.Text);
        }

        /// <inheritdoc />
        public override void SetEditorFont(string family, double size)
        {
            _textBox.Font = CreateSafeEditorFont(family, size);
        }

        /// <inheritdoc />
        public override void SetReadOnly(bool readOnly)
        {
            base.SetReadOnly(readOnly);
            _textBox.ReadOnly = readOnly;
            ApplyPreviewLayout();
        }

        /// <summary>
        /// 只读且预览可用时只显示渲染结果；编辑或没有预览时显示源码
        /// </summary>
        private void ApplyPreviewLayout()
        {
            bool previewOnly = IsReadOnly && _previewReady;
            try
            {
                _splitter.Panel1Collapsed = previewOnly;
            }
            catch (InvalidOperationException)
            {
                // 分割条尚未完成布局时忽略，预览就绪后会再调一次
            }
        }

        /// <summary>
        /// 控件句柄创建后初始化 WebView2(异步)
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_webViewAvailable && _webView == null)
            {
                InitializePreviewAsync();
            }
        }

        /// <summary>
        /// 初始化 WebView2 预览：探测 Runtime→建环境(映射本地资源)→加载模板页
        /// </summary>
        private async void InitializePreviewAsync()
        {
            try
            {
                string? version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(version))
                {
                    return;
                }
            }
            catch
            {
                // Runtime 缺失：保持降级面板
                return;
            }

            _webViewAvailable = true;
            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            try
            {
                string assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Markdown");
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions
                {
                    AllowSingleSignOnUsingOSPrimaryAccount = false
                });
                await _webView.EnsureCoreWebView2Async(environment);
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(ASSETS_HOST, assetsDirectory, CoreWebView2HostResourceAccessKind.Allow);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                string templatePath = Path.Combine(assetsDirectory, "preview-template.html");
                if (File.Exists(templatePath))
                {
                    string template = await File.ReadAllTextAsync(templatePath);
                    _webView.CoreWebView2.NavigateToString(template);
                    _previewReady = true;
                    // 预览就绪后渲染当前内容并同步主题(模板加载完成有一小段延迟,稍后补渲染)
                    await Task.Delay(200);
                    await RenderPreviewAsync(_textBox.Text);
                    await ApplyPreviewThemeAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WebView2 初始化失败: " + ex.Message);
                _previewReady = false;
                return;
            }

            // 用 WebView 替换降级面板
            _splitter.Panel2.SuspendLayout();
            _splitter.Panel2.Controls.Remove(_previewFallbackPanel);
            _splitter.Panel2.Controls.Add(_webView);
            _splitter.Panel2.ResumeLayout();
            Internal.Themes.ThemeManager.Apply(_splitter.Panel1);
            ApplyPreviewLayout();
        }

        /// <summary>
        /// Ctrl+S 立即保存
        /// </summary>
        private void EditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true;
                RequestSaveNow();
            }
        }

        /// <summary>
        /// 输入停止 500ms 后刷新预览
        /// </summary>
        private void RestartPreviewTimer()
        {
            if (_previewTimer == null)
            {
                _previewTimer = new Timer
                {
                    Interval = 500
                };
                _previewTimer.Tick += (sender, e) =>
                {
                    _previewTimer.Stop();
                    _ = RenderPreviewAsync(_textBox.Text);
                };
            }
            _previewTimer.Stop();
            if (_previewReady)
            {
                _previewTimer.Start();
            }
        }

        /// <summary>
        /// 立即触发一次预览(加载笔记时)
        /// </summary>
        private void RenderPreview()
        {
            if (_previewReady)
            {
                _ = RenderPreviewAsync(_textBox.Text);
            }
        }

        /// <summary>
        /// 调用页面脚本渲染 Markdown
        /// </summary>
        private async Task RenderPreviewAsync(string markdown)
        {
            if (_webView == null || !_previewReady)
            {
                return;
            }

            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync("window.renderMarkdown(" + JsonSerializer.Serialize(markdown) + ")");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("预览渲染失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 主题切换：同步预览页明暗样式
        /// </summary>
        private void OnThemeChanged(Internal.Themes.Theme theme)
        {
            _ = ApplyPreviewThemeAsync();
        }

        /// <summary>
        /// 把当前主题明暗模式注入预览页
        /// </summary>
        private async Task ApplyPreviewThemeAsync()
        {
            if (_webView == null || !_previewReady)
            {
                return;
            }

            string mode = Internal.Themes.ThemeManager.Current.DarkTitleBar ? "dark" : "light";
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync("window.setThemeMode('" + mode + "')");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("预览主题同步失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 释放计时器与 WebView
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Internal.Themes.ThemeManager.CurrentChanged -= OnThemeChanged;
                _previewTimer?.Dispose();
                _previewTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}
