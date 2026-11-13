using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.WinForm.Controls;
using Rampage.CryptedNotex.WinForm.Forms;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using Rampage.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Rampage.CryptedNotex.WinForm
{
    /// <summary>
    /// 主窗体：三栏布局(分类侧栏｜笔记列表｜编辑区+附件)；退出原因决定回登录界面还是退出程序
    /// </summary>
    public class Notex : NotexFormBase
    {
        private readonly CryptoSession _session;
        private readonly Dictionary<ContentType, NoteEditorBase> _editors = new Dictionary<ContentType, NoteEditorBase>();

        private UserSetting _userSetting = new UserSetting();
        private AutoLockManager? _autoLock;
        private List<Category> _categories = new List<Category>();
        private List<Note> _notes = new List<Note>();
        private Category? _currentCategory;
        private Note? _currentNote;
        private bool _trashView;
        private string _searchKeyword = string.Empty;
        private Timer? _searchTimer;

        private ListView _categoryList = null!;
        private ListView _noteList = null!;
        private TextBox _searchBox = null!;
        private Panel _normalToolbar = null!;
        private Panel _trashToolbar = null!;
        private Panel _editorHost = null!;
        private TextBox _titleBox = null!;
        private Label _typeLabel = null!;
        private Label _metaLabel = null!;
        private Label _editorMessage = null!;
        private Label _emptyLabel = null!;
        private AttachmentPanel _attachmentPanel = null!;
        private Button _trashButton = null!;
        private Label _userLabel = null!;
        private Label _collectionLabel = null!;
        private Label _collectionSummary = null!;
        private Button _pinButton = null!;
        private Button _deleteButton = null!;
        private NoteEditorBase? _activeEditor;
        private Button _editToggleButton = null!;
        private bool _editing;
        private bool _openForEdit;
        private Panel _frame = null!;
        private Panel _statusBar = null!;
        private Label _statusLabel = null!;
        private Panel _statusProgressHost = null!;
        private SplitContainer _outerSplit = null!;
        private SplitContainer _innerSplit = null!;
        private ImageList? _categoryRowImages;
        private ImageList? _noteRowImages;
        private int _categoryHoverIndex = -1;
        private int _noteHoverIndex = -1;
        private int _statusBusy;
        private int _listRequestId;
        private int _noteRequestId;
        private bool _suppressCategoryEvent;
        private bool _closing;
        private bool _announceNextSave;
        private bool _suppressSearch;
        private Size _clientSize;
        private Task _saveQueue = Task.CompletedTask;
        private NotifyIcon? _trayIcon;
        private FormWindowState _stateBeforeTray = FormWindowState.Normal;
        private bool _trayTipShown;

        private ContextMenuStrip _categoryMenu = null!;
        private ContextMenuStrip _noteMenu = null!;
        private ContextMenuStrip _trashNoteMenu = null!;
        private ContextMenuStrip _newNoteMenu = null!;

        /// <summary>
        /// 退出原因(关闭后由 NotexGuiContext 判断去向)
        /// </summary>
        public MainExitReason ExitReason
        {
            get;
            private set;
        }

        /// <summary>
        /// 主窗体用底部状态条表示忙碌
        /// </summary>
        protected override bool UsesBusyBanner
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// 提示条让出状态条
        /// </summary>
        protected override int ToastBottomMargin
        {
            get
            {
                return 46;
            }
        }

        /// <summary>
        /// 构造主窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        public Notex(IRampageApplication app, CryptoSession session) : base(app)
        {
            _session = session;
            Text = NotexBrand.DisplayName + " - " + (session.User?.DisplayName ?? string.Empty);
            Tag = "theme:wallpaper";
            // 默认尺寸随主屏工作区走(9/10,1180x700 为下限):高分屏上编辑器列不再被写死的尺寸挤小
            Rectangle workArea = Screen.PrimaryScreen!.WorkingArea;
            _clientSize = new Size(
                Math.Min(ScalePixel(1440), workArea.Width * 9 / 10),
                Math.Min(ScalePixel(900), workArea.Height * 9 / 10));
            ClientSize = _clientSize;
            MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUi();
            BuildMenus();
            ClearEditor();
            _editorMessage.Text = "让想法，从这里开始。\n\n选择一条笔记，或按 Ctrl+N 开始记录。";
            _editorMessage.Visible = true;
        }

        /// <summary>
        /// 句柄创建后的缩放可能改掉构造时的客户区，这里再钉一次
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(ScalePixel(1000), workArea.Width), Math.Min(ScalePixel(640), workArea.Height));
            ClientSize = _clientSize;
            ApplySplitDistances();
            ApplyListMetrics();
            FitNoteColumns();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyListMetrics();
            FitNoteColumns();
        }

        /// <summary>
        /// 笔记列表列宽跟客户区走，避免底部出现横向滚动条
        /// </summary>
        private void FitNoteColumns()
        {
            if (_noteList == null || _noteList.Columns.Count < 3 || _noteList.ClientSize.Width < 120)
            {
                return;
            }

            _noteList.Columns[1].Width = 0;
            _noteList.Columns[2].Width = 0;
            _noteList.Columns[0].Width = Math.Max(80, _noteList.ClientSize.Width - 2);
            if (_categoryList.Columns.Count > 0)
            {
                _categoryList.Columns[0].Width = Math.Max(80, _categoryList.ClientSize.Width - 2);
            }
        }

        private int ScalePixel(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96.0);
        }

        private void ApplyListMetrics()
        {
            if (_categoryRowImages != null)
            {
                _categoryRowImages.ImageSize = new Size(1, Math.Min(256, ScalePixel(46)));
            }
            if (_noteRowImages != null)
            {
                _noteRowImages.ImageSize = new Size(1, Math.Min(256, ScalePixel(88)));
            }
        }

        /// <summary>
        /// 窗体有了真实宽度后再摆分割条，避免构造时被夹成一条缝
        /// </summary>
        private void ApplySplitDistances()
        {
            try
            {
                int outer = Math.Min(ScalePixel(224), Math.Max(_outerSplit.Panel1MinSize, _outerSplit.Width - _outerSplit.Panel2MinSize - _outerSplit.SplitterWidth));
                if (outer > _outerSplit.Panel1MinSize)
                {
                    _outerSplit.SplitterDistance = outer;
                }
                int inner = Math.Min(ScalePixel(350), Math.Max(_innerSplit.Panel1MinSize, _innerSplit.Width - ScalePixel(400) - _innerSplit.SplitterWidth));
                if (_innerSplit.Width > _innerSplit.Panel1MinSize + ScalePixel(400) + _innerSplit.SplitterWidth)
                {
                    _innerSplit.Panel2MinSize = ScalePixel(400);
                }
                if (inner > _innerSplit.Panel1MinSize)
                {
                    _innerSplit.SplitterDistance = inner;
                }
            }
            catch (InvalidOperationException)
            {
                // 宽度还不够放下两侧最小尺寸时保持现状
            }
        }

        /// <summary>
        /// 加载：读偏好、装分类、恢复上次笔记、启动自动锁定
        /// </summary>
        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                _userSetting = GetService<IUserSettingService>().GetUserSetting(_session.User!.Id);
                ApplyFramePadding();
                ApplyEditorFonts();
                Internal.Themes.ThemeManager.CurrentChanged += OnThemeChanged;

                _autoLock = new AutoLockManager();
                _autoLock.UpdateSettings(_userSetting.AutoLockMinutes);
                _autoLock.AutoLockRequested += OnAutoLockRequested;

                LoadCategories();
                if (!await TryRestoreLastNoteAsync())
                {
                    await ReloadNotesAsync();
                }
            }
            catch (Exception ex)
            {
                ShowToast("加载失败：" + ex.Message, ToastKind.Error);
            }
        }

        /// <summary>
        /// 关闭前：冲写未保存内容、收掉托盘图标、解除监听
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_closing)
            {
                base.OnFormClosing(e);
                return;
            }

            _closing = true;
            _searchTimer?.Stop();
            _searchTimer?.Dispose();
            _searchTimer = null;
            FlushEditorPendingSave();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _autoLock?.Dispose();
            _categoryRowImages?.Dispose();
            _noteRowImages?.Dispose();
            Internal.Themes.ThemeManager.CurrentChanged -= OnThemeChanged;
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Ctrl+F 聚焦搜索
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                return true;
            }
            if (keyData == Keys.Escape && _searchBox.Focused && _searchBox.TextLength > 0)
            {
                _searchBox.Clear();
                return true;
            }
            if (keyData == (Keys.Control | Keys.N) && !_trashView)
            {
                CreateNote(ContentType.PlainText);
                return true;
            }
            if (keyData == (Keys.Control | Keys.D1) || keyData == (Keys.Control | Keys.NumPad1))
            {
                ActivateDefaultCategory();
                return true;
            }
            if (keyData == (Keys.Control | Keys.D2) || keyData == (Keys.Control | Keys.NumPad2))
            {
                if (!_trashView)
                {
                    ToggleTrashView();
                }
                return true;
            }
            if (keyData == (Keys.Control | Keys.D3) || keyData == (Keys.Control | Keys.NumPad3))
            {
                OpenSettings();
                return true;
            }
            if (keyData == Keys.Delete && _noteList.ContainsFocus && !_trashView)
            {
                TrashSelectedNote();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #region 界面构建

        /// <summary>
        /// 构建三栏界面
        /// </summary>
        private void BuildUi()
        {
            _frame = new Panel
            {
                Dock = DockStyle.Fill,
                Tag = "theme:inset"
            };

            _outerSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                Panel1MinSize = 190,
                SplitterWidth = 1
            };
            _outerSplit.Panel1.Tag = "theme:sidebar";

            BuildSidebar(_outerSplit.Panel1);
            BuildContentArea(_outerSplit.Panel2);
            _frame.Controls.Add(_outerSplit);

            _statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                Padding = new Padding(12, 0, 12, 0),
                Tag = "theme:chrome"
            };
            _statusProgressHost = new Panel
            {
                Dock = DockStyle.Left,
                Width = 128,
                Visible = false,
                Padding = new Padding(0, 11, 12, 11)
            };
            ProgressBar statusProgress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 24
            };
            _statusProgressHost.Controls.Add(statusProgress);
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "就绪",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Tag = "theme:muted"
            };
            Label shortcutHint = new Label
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                Text = "Ctrl+N 新建   ·   Ctrl+F 搜索   ·   Ctrl+S 保存",
                TextAlign = ContentAlignment.MiddleRight,
                Tag = "theme:muted",
                Padding = new Padding(12, 8, 0, 0)
            };
            _statusBar.Controls.Add(_statusLabel);
            _statusBar.Controls.Add(_statusProgressHost);
            _statusBar.Controls.Add(shortcutHint);
            _frame.Controls.Add(_statusBar);
            Controls.Add(_frame);
        }

        /// <summary>
        /// 有壁纸时留出一圈桌面边，扁平主题则贴边
        /// </summary>
        private void ApplyFramePadding()
        {
            _frame.Padding = Padding.Empty;
        }

        /// <summary>
        /// 左侧分类侧栏
        /// </summary>
        private void BuildSidebar(Panel panel)
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Tag = "theme:sidebar"
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 206F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 172F));
            panel.Controls.Add(grid);

            WorkspaceIdentity brand = new WorkspaceIdentity
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Tag = "theme:sidebar"
            };
            grid.Controls.Add(brand, 0, 0);

            TableLayoutPanel categoryHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(16, 4, 12, 4),
                Margin = Padding.Empty,
                Tag = "theme:sidebar"
            };
            categoryHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            categoryHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
            Label heading = new Label
            {
                Text = "我的分类",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "theme:muted"
            };
            categoryHeader.Controls.Add(heading, 0, 0);
            Button newCategoryButton = UiFactory.Button("＋ 分类");
            newCategoryButton.Dock = DockStyle.Fill;
            newCategoryButton.AutoSize = false;
            newCategoryButton.Margin = Padding.Empty;
            newCategoryButton.AccessibleName = "新建分类";
            newCategoryButton.Click += (sender, e) => CreateCategory();
            categoryHeader.Controls.Add(newCategoryButton, 1, 0);
            grid.Controls.Add(categoryHeader, 0, 1);

            _categoryRowImages = new ImageList { ImageSize = new Size(1, 46) };
            _categoryList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                ShowGroups = false,
                HeaderStyle = ColumnHeaderStyle.None,
                MultiSelect = false,
                OwnerDraw = true,
                BorderStyle = BorderStyle.None,
                SmallImageList = _categoryRowImages,
                Margin = new Padding(10, 0, 10, 8),
                AccessibleName = "笔记分类"
            };
            _categoryList.Columns.Add("分类", 200);
            UiFactory.EnableDoubleBuffer(_categoryList);
            _categoryList.Resize += (sender, e) => FitNoteColumns();
            _categoryList.DrawItem += SuppressListItemDraw;
            _categoryList.DrawSubItem += CategoryDrawSubItem;
            _categoryList.DrawColumnHeader += (sender, e) => e.DrawDefault = true;
            _categoryList.SelectedIndexChanged += OnCategorySelected;
            _categoryList.MouseUp += CategoryListMouseUp;
            _categoryList.MouseMove += (sender, e) => UpdateListHover(_categoryList, ref _categoryHoverIndex, e.Location);
            _categoryList.MouseLeave += (sender, e) => UpdateListHover(_categoryList, ref _categoryHoverIndex, new Point(-1, -1));
            grid.Controls.Add(_categoryList, 0, 2);

            TableLayoutPanel bottomPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(12, 8, 12, 12),
                Margin = Padding.Empty,
                Tag = "theme:sidebar"
            };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _trashButton = UiFactory.Button("回收站    Ctrl+2");
            _trashButton.Dock = DockStyle.Fill;
            _trashButton.AutoSize = false;
            _trashButton.Click += (sender, e) => ToggleTrashView();
            bottomPanel.Controls.Add(_trashButton, 0, 0);
            bottomPanel.SetColumnSpan(_trashButton, 2);

            Button settingsButton = UiFactory.Button("设置");
            settingsButton.AutoSize = false;
            settingsButton.Dock = DockStyle.Fill;
            settingsButton.Click += (sender, e) => OpenSettings();
            bottomPanel.Controls.Add(settingsButton, 0, 1);
            Button aboutButton = UiFactory.Button("关于");
            aboutButton.AutoSize = false;
            aboutButton.Dock = DockStyle.Fill;
            aboutButton.Click += (sender, e) =>
            {
                using (AboutForm form = new AboutForm(App))
                {
                    form.ShowDialog(this);
                }
            };
            bottomPanel.Controls.Add(aboutButton, 1, 1);
            _userLabel = new Label
            {
                Text = _session.User?.DisplayName ?? string.Empty,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                AutoEllipsis = true,
                Tag = "theme:muted"
            };
            bottomPanel.Controls.Add(_userLabel, 0, 2);
            Button logoutButton = UiFactory.Button("登出");
            logoutButton.AutoSize = false;
            logoutButton.Dock = DockStyle.Fill;
            logoutButton.Click += (sender, e) =>
            {
                FlushEditorPendingSave();
                ExitReason = MainExitReason.Logout;
                Close();
            };
            bottomPanel.Controls.Add(logoutButton, 1, 2);
            grid.Controls.Add(bottomPanel, 0, 3);
        }

        /// <summary>
        /// 右侧内容区(笔记列表 + 编辑区)
        /// </summary>
        private void BuildContentArea(Panel panel)
        {
            _innerSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                Panel1MinSize = 320,
                SplitterWidth = 1
            };

            BuildNoteListColumn(_innerSplit.Panel1);
            _innerSplit.Panel2.Tag = "theme:editor";
            BuildEditorColumn(_innerSplit.Panel2);
            panel.Controls.Add(_innerSplit);
        }

        /// <summary>
        /// 中间笔记列表列
        /// </summary>
        private void BuildNoteListColumn(Panel panel)
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Tag = "theme:card"
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel collectionHeader = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(20, 16, 20, 8),
                Tag = "theme:card"
            };
            _collectionSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Text = "选择分类，开始记录",
                Tag = "theme:muted"
            };
            _collectionLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "我的笔记",
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                AutoEllipsis = true
            };
            collectionHeader.Controls.Add(_collectionLabel);
            collectionHeader.Controls.Add(_collectionSummary);
            layout.Controls.Add(collectionHeader, 0, 0);

            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10.5F),
                Margin = new Padding(20, 8, 20, 8),
                PlaceholderText = "搜索所有标题    Ctrl+F",
                AccessibleName = "搜索笔记标题"
            };
            _searchBox.TextChanged += (sender, e) =>
            {
                if (_suppressSearch)
                {
                    return;
                }
                _searchTimer ??= new Timer
                {
                    Interval = 300
                };
                _searchTimer.Stop();
                _searchTimer.Tick -= SearchTimerTick;
                _searchTimer.Tick += SearchTimerTick;
                _searchTimer.Start();
            };
            layout.Controls.Add(_searchBox, 0, 1);

            _normalToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(14, 0, 14, 4),
                Padding = new Padding(2, 2, 2, 0),
                Tag = "theme:chrome"
            };
            BuildNormalToolbar(_normalToolbar);
            layout.Controls.Add(_normalToolbar, 0, 2);

            _trashToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(14, 0, 14, 4),
                Padding = new Padding(2, 2, 2, 0),
                Visible = false,
                Tag = "theme:chrome"
            };
            BuildTrashToolbar(_trashToolbar);
            layout.Controls.Add(_trashToolbar, 0, 2);

            _noteRowImages = new ImageList { ImageSize = new Size(1, 88) };
            _noteList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                OwnerDraw = true,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.None,
                SmallImageList = _noteRowImages,
                Margin = Padding.Empty,
                AccessibleName = "笔记列表"
            };
            _noteList.Columns.Add("标题", 160);
            _noteList.Columns.Add("修改时间", 108);
            _noteList.Columns.Add("字数", 56);
            _noteList.Font = new Font("Microsoft YaHei UI", 9.5F);
            UiFactory.EnableDoubleBuffer(_noteList);
            _noteList.Resize += (sender, e) => FitNoteColumns();
            _noteList.DrawItem += SuppressListItemDraw;
            _noteList.DrawSubItem += NoteListDrawSubItem;
            _noteList.DrawColumnHeader += NoteListDrawHeader;
            _noteList.SelectedIndexChanged += OnNoteSelected;
            _noteList.MouseUp += NoteListMouseUp;
            _noteList.MouseMove += (sender, e) => UpdateListHover(_noteList, ref _noteHoverIndex, e.Location);
            _noteList.MouseLeave += (sender, e) => UpdateListHover(_noteList, ref _noteHoverIndex, new Point(-1, -1));
            _noteList.DoubleClick += (sender, e) => EnterEdit();

            Panel listHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 8, 8),
                Tag = "theme:panelAlt"
            };
            _emptyLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "这个分类还没有笔记",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 11F),
                Tag = "theme:muted",
                Visible = false
            };
            listHost.Controls.Add(_noteList);
            listHost.Controls.Add(_emptyLabel);
            layout.Controls.Add(listHost, 0, 3);
            panel.Controls.Add(layout);
        }

        /// <summary>
        /// 普通视图工具条(新建下拉/置顶/删除)
        /// </summary>
        private void BuildNormalToolbar(Panel panel)
        {
            Button newNoteButton = UiFactory.Button("＋ 新建笔记 ▾", "theme:accent");
            newNoteButton.Click += (sender, e) =>
            {
                Point location = newNoteButton.PointToScreen(new Point(0, newNoteButton.Height));
                _newNoteMenu.Show(location);
            };
            panel.Controls.Add(newNoteButton);

            _pinButton = UiFactory.Button("置顶");
            _pinButton.Enabled = false;
            _pinButton.Click += (sender, e) => TogglePinned();
            panel.Controls.Add(_pinButton);

            _deleteButton = UiFactory.Button("删除");
            _deleteButton.Enabled = false;
            _deleteButton.Click += (sender, e) => TrashSelectedNote();
            panel.Controls.Add(_deleteButton);
        }

        /// <summary>
        /// 回收站视图工具条(恢复/彻底删除/清空)
        /// </summary>
        private void BuildTrashToolbar(Panel panel)
        {
            Button restoreButton = UiFactory.Button("恢复");
            restoreButton.Click += (sender, e) => RestoreSelectedNote();
            panel.Controls.Add(restoreButton);

            Button purgeButton = UiFactory.Button("彻底删除", "theme:danger");
            purgeButton.Click += (sender, e) => PurgeSelectedNote();
            panel.Controls.Add(purgeButton);

            Button emptyButton = UiFactory.Button("清空");
            emptyButton.Click += (sender, e) => EmptyTrash();
            panel.Controls.Add(emptyButton);
        }

        /// <summary>
        /// 右侧编辑区列
        /// </summary>
        private void BuildEditorColumn(Panel panel)
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 142F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

            TableLayoutPanel headerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Tag = "theme:editor",
                Margin = new Padding(28, 22, 28, 0)
            };
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            headerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _titleBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                BorderStyle = BorderStyle.None,
                Tag = "theme:editorTitle",
                AccessibleName = "笔记标题（明文）",
                PlaceholderText = "笔记标题（明文）"
            };
            _titleBox.TextChanged += (sender, e) => _activeEditor?.NotifyTitleChanged();
            headerPanel.Controls.Add(_titleBox, 0, 0);

            Panel metaRow = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 8),
                Tag = "theme:editor",
                Margin = Padding.Empty
            };
            _typeLabel = new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Left,
                Width = 84,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                Tag = "theme:link"
            };
            metaRow.Controls.Add(_typeLabel);

            _metaLabel = new Label
            {
                Text = "选择或新建一条笔记",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "theme:muted"
            };

            _editToggleButton = new Button
            {
                Text = "编辑",
                AutoSize = true,
                Enabled = false,
                MinimumSize = new Size(72, 30),
                Padding = new Padding(12, 2, 12, 2),
                Margin = Padding.Empty,
                Dock = DockStyle.Right,
                Width = 80,
                Tag = "theme:accent"
            };
            _editToggleButton.Click += (sender, e) => ToggleEditMode();
            metaRow.Controls.Clear();
            metaRow.Controls.Add(_metaLabel);
            metaRow.Controls.Add(_typeLabel);
            metaRow.Controls.Add(_editToggleButton);
            headerPanel.Controls.Add(metaRow, 0, 1);
            layout.Controls.Add(headerPanel, 0, 0);

            _editorHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(28, 6, 28, 12),
                Padding = new Padding(0),
                Tag = "theme:editor"
            };

            _editorMessage = new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 12F),
                Tag = "theme:muted",
                Visible = false
            };
            _editorHost.Controls.Add(_editorMessage);
            layout.Controls.Add(_editorHost, 0, 1);

            _attachmentPanel = new AttachmentPanel
            {
                Margin = new Padding(28, 0, 28, 12)
            };
            _attachmentPanel.AddAttachmentRequested += (sender, e) => AddAttachment();
            _attachmentPanel.OpenAttachmentRequested += (sender, attachment) => OpenAttachment(attachment);
            _attachmentPanel.RemoveAttachmentRequested += (sender, attachment) => RemoveAttachment(attachment);
            layout.Controls.Add(_attachmentPanel, 0, 2);
            panel.Controls.Add(layout);
        }

        /// <summary>
        /// 构建右键菜单与新建菜单
        /// </summary>
        private void BuildMenus()
        {
            _categoryMenu = new ContextMenuStrip();
            _categoryMenu.Items.Add("重命名", null, (sender, e) => RenameCategory());
            _categoryMenu.Items.Add("设置独立密码…", null, (sender, e) => SetIndependentPassword());
            _categoryMenu.Items.Add("重置独立密码…", null, (sender, e) => ResetIndependentPassword());
            _categoryMenu.Items.Add("切回跟随全局密码…", null, (sender, e) => ChangeToFollowUser());
            _categoryMenu.Items.Add(new ToolStripSeparator());
            _categoryMenu.Items.Add("上移", null, (sender, e) => MoveCategory(-1));
            _categoryMenu.Items.Add("下移", null, (sender, e) => MoveCategory(1));
            _categoryMenu.Items.Add(new ToolStripSeparator());
            _categoryMenu.Items.Add("删除", null, (sender, e) => DeleteCategory());

            _noteMenu = new ContextMenuStrip();
            _noteMenu.Items.Add("编辑", null, (sender, e) => EnterEdit());
            _noteMenu.Items.Add("置顶或取消置顶", null, (sender, e) => TogglePinned());
            _noteMenu.Items.Add("移入回收站", null, (sender, e) => TrashSelectedNote());

            _trashNoteMenu = new ContextMenuStrip();
            _trashNoteMenu.Items.Add("恢复", null, (sender, e) => RestoreSelectedNote());
            _trashNoteMenu.Items.Add("彻底删除", null, (sender, e) => PurgeSelectedNote());

            _newNoteMenu = new ContextMenuStrip();
            _newNoteMenu.Items.Add("纯文本笔记", null, (sender, e) => CreateNote(ContentType.PlainText));
            _newNoteMenu.Items.Add("富文本笔记", null, (sender, e) => CreateNote(ContentType.RichText));
            _newNoteMenu.Items.Add("Markdown 笔记", null, (sender, e) => CreateNote(ContentType.Markdown));
            StyleMenus();
        }

        #endregion

        #region 分类

        /// <summary>
        /// 加载分类列表并选第一个
        /// </summary>
        private void LoadCategories()
        {
            int selectedId = _currentCategory?.Id ?? 0;
            _categories = GetService<ICategoryService>().GetCategoryList(_session.User!.Id);
            _suppressCategoryEvent = true;
            try
            {
                _categoryList.BeginUpdate();
                _categoryList.Items.Clear();
                int selectIndex = 0;
                for (int i = 0; i < _categories.Count; i++)
                {
                    Category category = _categories[i];
                    ListViewItem item = new ListViewItem(category.Name)
                    {
                        Tag = category
                    };
                    _categoryList.Items.Add(item);
                    if (selectedId > 0 && category.Id == selectedId)
                    {
                        selectIndex = i;
                    }
                }
                _categoryList.EndUpdate();
                if (_categoryList.Items.Count > 0)
                {
                    _categoryList.SelectedIndices.Add(selectIndex);
                    _currentCategory = (Category)_categoryList.Items[selectIndex].Tag!;
                }
                else
                {
                    _currentCategory = null;
                }
            }
            finally
            {
                _suppressCategoryEvent = false;
            }
        }

        /// <summary>
        /// 当前选中分类
        /// </summary>
        private Category? SelectedCategory()
        {
            return _categoryList.SelectedItems.Count > 0 ? _categoryList.SelectedItems[0].Tag as Category : null;
        }

        /// <summary>
        /// 切换分类
        /// </summary>
        private async void OnCategorySelected(object? sender, EventArgs e)
        {
            if (_suppressCategoryEvent || _closing)
            {
                return;
            }

            Category? category = SelectedCategory();
            if (category == null || ReferenceEquals(category, _currentCategory))
            {
                return;
            }

            FlushEditorPendingSave();
            _trashView = false;
            SyncViewMode();
            _currentCategory = category;
            await ReloadNotesAsync();
        }

        /// <summary>
        /// 分类右键菜单
        /// </summary>
        private void CategoryListMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }
            ListViewHitTestInfo hit = _categoryList.HitTest(e.Location);
            if (hit.Item == null)
            {
                return;
            }

            foreach (ListViewItem item in _categoryList.Items)
            {
                item.Selected = ReferenceEquals(item, hit.Item);
            }

            Category category = (Category)hit.Item.Tag!;
            bool independent = category.EncryptMode == (int)CategoryEncryptMode.IndependentPassword;
            _categoryMenu.Items[1].Enabled = !independent;
            _categoryMenu.Items[2].Enabled = independent;
            _categoryMenu.Items[3].Enabled = independent;
            _categoryMenu.Items[6].Enabled = hit.Item.Index > 0;
            _categoryMenu.Items[7].Enabled = hit.Item.Index < _categoryList.Items.Count - 1;
            _categoryMenu.Show(Cursor.Position);
        }

        /// <summary>
        /// 新建分类
        /// </summary>
        private void CreateCategory()
        {
            using (CategoryEditForm form = new CategoryEditForm(App, "新建分类", string.Empty))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                Category category = new Category
                {
                    UserId = _session.User!.Id,
                    Name = form.CategoryName
                };
                R result = GetService<ICategoryService>().SaveCategory(category);
                if (!result.IsSuccess)
                {
                    ShowToast(result.Message ?? "创建失败", ToastKind.Error);
                    return;
                }
                LoadCategories();
                ShowToast("已创建分类「" + form.CategoryName + "」", ToastKind.Success);
            }
        }

        /// <summary>
        /// 重命名分类
        /// </summary>
        private void RenameCategory()
        {
            Category? category = SelectedCategory();
            if (category == null)
            {
                return;
            }

            using (CategoryEditForm form = new CategoryEditForm(App, "重命名分类", category.Name))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                category.Name = form.CategoryName;
                R result = GetService<ICategoryService>().SaveCategory(category);
                if (!result.IsSuccess)
                {
                    ShowToast(result.Message ?? "重命名失败", ToastKind.Error);
                    return;
                }
                LoadCategories();
                ShowToast("已重命名分类", ToastKind.Success);
            }
        }

        /// <summary>
        /// 删除分类(其下笔记移入默认分类)
        /// </summary>
        private async void DeleteCategory()
        {
            Category? category = SelectedCategory();
            if (category == null)
            {
                return;
            }
            if (category.IsDefault == 1)
            {
                ShowToast("默认分类不可删除", ToastKind.Info);
                return;
            }

            DialogResult choice = MessageBox.Show(
                "删除分类「" + category.Name + "」？\n该分类下的笔记(含回收站)将移入默认分类。",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (choice != DialogResult.Yes)
            {
                return;
            }

            FlushEditorPendingSave();
            string name = category.Name;
            R result = GetService<ICategoryService>().RemoveCategory(_session.User!.Id, category.Id, true);
            if (!result.IsSuccess)
            {
                ShowToast(result.Message ?? "删除失败", ToastKind.Error);
                return;
            }
            _currentCategory = null;
            LoadCategories();
            ClearEditor();
            await ReloadNotesAsync();
            ShowToast("已删除分类「" + name + "」", ToastKind.Success);
        }

        /// <summary>
        /// 上移/下移分类
        /// </summary>
        private void MoveCategory(int direction)
        {
            Category? category = SelectedCategory();
            if (category == null)
            {
                return;
            }

            int index = _categories.FindIndex(m => m.Id == category.Id);
            int target = index + direction;
            if (index < 0 || target < 0 || target >= _categories.Count)
            {
                return;
            }

            (_categories[index], _categories[target]) = (_categories[target], _categories[index]);
            GetService<ICategoryService>().UpdateCategorySort(_session.User!.Id, _categories.Select(m => m.Id));
            LoadCategories();
            _categoryList.SelectedIndices.Add(target);
        }

        /// <summary>
        /// 设置独立密码(0→1,全量重加密)
        /// </summary>
        private void SetIndependentPassword()
        {
            Category? category = SelectedCategory();
            if (category == null)
            {
                return;
            }

            using (PasswordDialogForm form = new PasswordDialogForm(App, "设置独立密码",
                "为分类「" + category.Name + "」设置独立密码后：\n· 其笔记改由该密码加密(与登录密码无关)\n· 现有笔记将立即重新加密",
                "独立密码(至少6位)", "确认独立密码"))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string password = form.Values[0];
                string confirm = form.Values[1];
                if (password.Length < 6)
                {
                    ShowToast("独立密码至少 6 位", ToastKind.Error);
                    return;
                }
                if (password != confirm)
                {
                    ShowToast("两次输入不一致", ToastKind.Error);
                    return;
                }

                FlushEditorPendingSave();
                _autoLock?.Suppress(true);
                try
                {
                    R result = ProgressRunner.Run(this, "设置独立密码(重新加密)",
                        progress => GetService<ICategoryService>().SetCategoryIndependentPassword(
                            _session.User!.Id, category.Id, password, _session.GetUserContentKey(), progress));
                    if (!result.IsSuccess)
                    {
                        ShowToast(result.Message ?? "设置失败", ToastKind.Error);
                        return;
                    }
                    if (result.Data is byte[] categoryKey)
                    {
                        _session.MarkCategoryUnlocked(category.Id, categoryKey);
                    }
                    LoadCategories();
                    ShowToast("已设置独立密码，该分类笔记已重新加密。", ToastKind.Success);
                }
                finally
                {
                    _autoLock?.Suppress(false);
                }
            }
        }

        /// <summary>
        /// 重置独立密码(1 内换密,全量重加密)
        /// </summary>
        private void ResetIndependentPassword()
        {
            Category? category = SelectedCategory();
            if (category == null)
            {
                return;
            }

            using (PasswordDialogForm form = new PasswordDialogForm(App, "重置独立密码",
                "重置分类「" + category.Name + "」的独立密码,其全部笔记将用新密码重新加密。",
                "旧独立密码", "新独立密码(至少6位)", "确认新独立密码"))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                if (form.Values[1] != form.Values[2])
                {
                    ShowToast("两次输入的新密码不一致", ToastKind.Error);
                    return;
                }

                FlushEditorPendingSave();
                _autoLock?.Suppress(true);
                try
                {
                    R result = ProgressRunner.Run(this, "重置独立密码(重新加密)",
                        progress => GetService<ICategoryService>().ResetCategoryPassword(
                            _session.User!.Id, category.Id, form.Values[0], form.Values[1], progress));
                    if (!result.IsSuccess)
                    {
                        ShowToast(result.Message ?? "重置失败", ToastKind.Error);
                        return;
                    }
                    if (result.Data is byte[] categoryKey)
                    {
                        _session.ReplaceCategoryKey(category.Id, categoryKey);
                    }
                    ShowToast("独立密码已重置，笔记已重新加密。", ToastKind.Success);
                }
                finally
                {
                    _autoLock?.Suppress(false);
                }
            }
        }

        /// <summary>
        /// 切回跟随全局(1→0,全量重加密)
        /// </summary>
        private void ChangeToFollowUser()
        {
            Category? category = SelectedCategory();
            if (category == null)
            {
                return;
            }

            using (PasswordDialogForm form = new PasswordDialogForm(App, "切回跟随全局密码",
                "分类「" + category.Name + "」将改用你的登录密码加密,全部笔记立即重新加密。",
                "当前独立密码"))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                FlushEditorPendingSave();
                _autoLock?.Suppress(true);
                try
                {
                    R result = ProgressRunner.Run(this, "切换加密模式(重新加密)",
                        progress => GetService<ICategoryService>().ChangeCategoryToFollowUser(
                            _session.User!.Id, category.Id, form.Values[0], _session.GetUserContentKey(), progress));
                    if (!result.IsSuccess)
                    {
                        ShowToast(result.Message ?? "切换失败", ToastKind.Error);
                        return;
                    }
                    LoadCategories();
                    ShowToast("已改回跟随登录密码，笔记已重新加密。", ToastKind.Success);
                }
                finally
                {
                    _autoLock?.Suppress(false);
                }
            }
        }

        #endregion

        #region 笔记列表

        /// <summary>
        /// 重载当前视图的笔记列表(分类视图/搜索视图/回收站视图)
        /// </summary>
        private async Task ReloadNotesAsync()
        {
            if (_closing || IsDisposed || _session.User == null)
            {
                return;
            }
            int requestId = ++_listRequestId;
            int userId = _session.User!.Id;
            bool trash = _trashView;
            string keyword = _searchKeyword;
            int? categoryId = _currentCategory?.Id;
            string busy = keyword.Length > 0 ? "正在搜索…" : trash ? "正在加载回收站…" : "正在加载笔记…";
            PushStatus(busy);
            try
            {
                List<Note> notes = await UiWork.RunAsync(() => QueryNotes(userId, trash, keyword, categoryId));
                if (IsDisposed || requestId != _listRequestId)
                {
                    return;
                }
                _notes = notes;
                FillNoteList(notes);
            }
            catch (Exception ex)
            {
                if (!IsDisposed && requestId == _listRequestId)
                {
                    ShowToast("加载笔记失败：" + ex.Message, ToastKind.Error);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    PopStatus();
                }
            }
        }

        /// <summary>
        /// 按当前视图取笔记列表(可在后台线程调用)
        /// </summary>
        private List<Note> QueryNotes(int userId, bool trash, string keyword, int? categoryId)
        {
            if (trash)
            {
                return GetService<INoteService>().GetTrashList(userId);
            }
            if (keyword.Length > 0)
            {
                return GetService<INoteService>().SearchByTitleList(userId, keyword);
            }
            if (categoryId.HasValue)
            {
                return GetService<INoteService>().GetNoteList(userId, categoryId.Value);
            }
            return new List<Note>();
        }

        /// <summary>
        /// 把笔记填进列表，空列表换成说明
        /// </summary>
        private void FillNoteList(List<Note> notes)
        {
            _noteList.BeginUpdate();
            _noteList.Items.Clear();
            foreach (Note note in notes)
            {
                _noteList.Items.Add(BuildNoteItem(note));
            }
            _noteList.EndUpdate();
            bool empty = notes.Count == 0;
            _noteList.Visible = !empty;
            _emptyLabel.Visible = empty;
            _emptyLabel.Text = EmptyNoteText();
            _collectionLabel.Text = _searchKeyword.Length > 0 ? "搜索结果" : _trashView ? "回收站" : _currentCategory?.Name ?? "我的笔记";
            _collectionSummary.Text = notes.Count + " 条笔记  ·  " + (_searchKeyword.Length > 0 ? "标题匹配" : _trashView ? "可恢复已删除的笔记" : "最近修改优先 · 置顶优先");
            _pinButton.Enabled = false;
            _deleteButton.Enabled = false;
            if (!empty)
            {
                FitNoteColumns();
            }
        }

        /// <summary>
        /// 空列表文案
        /// </summary>
        private string EmptyNoteText()
        {
            if (_searchKeyword.Length > 0)
            {
                return "没有标题包含「" + _searchKeyword + "」的笔记\n换个关键词，或清空搜索框";
            }
            if (_trashView)
            {
                return "回收站是空的\n删除的笔记会先到这里，可恢复或彻底删除";
            }
            return "这个分类还没有笔记\n点上方「新建笔记」开始记录";
        }

        /// <summary>
        /// 构造笔记列表项
        /// </summary>
        private static ListViewItem BuildNoteItem(Note note)
        {
            ListViewItem item = new ListViewItem(note.Title.Length == 0 ? "无标题" : note.Title)
            {
                Tag = note
            };
            DateTime time = (note.UpdateTime ?? note.CreateTime).ToLocalTime();
            item.SubItems.Add(time.ToString("MM-dd HH:mm"));
            item.SubItems.Add(note.WordCount.ToString());
            return item;
        }

        /// <summary>
        /// 进入默认分类(Ctrl+1)。已选中时仍退出回收站并刷新。
        /// </summary>
        private async void ActivateDefaultCategory()
        {
            FlushEditorPendingSave();
            bool wasTrash = _trashView;
            _trashView = false;
            SyncViewMode();
            ListViewItem? target = null;
            foreach (ListViewItem item in _categoryList.Items)
            {
                if (item.Tag is Category category && category.IsDefault == 1)
                {
                    target = item;
                    break;
                }
            }
            if (target == null)
            {
                return;
            }

            if (target.Selected)
            {
                if (wasTrash)
                {
                    _currentCategory = (Category)target.Tag!;
                    await ReloadNotesAsync();
                }
                return;
            }

            _currentCategory = null;
            target.Selected = true;
        }

        /// <summary>
        /// Details 视图的整行在 DrawSubItem 里画，这里不走系统默认
        /// </summary>
        private static void SuppressListItemDraw(object? sender, DrawListViewItemEventArgs e)
        {
        }

        /// <summary>
        /// owner-draw 列表悬停行追踪：命中行变化时只重绘新旧两行
        /// </summary>
        private static void UpdateListHover(ListView list, ref int hovered, Point location)
        {
            ListViewItem? hit = location.X >= 0 ? list.GetItemAt(location.X, location.Y) : null;
            int index = hit?.Index ?? -1;
            if (index == hovered)
            {
                return;
            }
            int previous = hovered;
            hovered = index;
            if (previous >= 0 && previous < list.Items.Count)
            {
                list.Invalidate(list.GetItemRect(previous));
            }
            if (index >= 0 && index < list.Items.Count)
            {
                list.Invalidate(list.GetItemRect(index));
            }
        }

        /// <summary>
        /// 分类行：色点、默认分类环、独立密码锁
        /// </summary>
        private void CategoryDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.ColumnIndex != 0 || e.Item == null)
            {
                return;
            }

            Category? category = e.Item.Tag as Category;
            bool selected = e.Item.Selected;
            bool hovered = !selected && e.Item.Index == _categoryHoverIndex;
            Color back = selected
                ? Internal.Themes.ThemeManager.GetColor("selectionBack")
                : Internal.Themes.ThemeManager.GetColor("panelAltBack");
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            if (hovered)
            {
                using (SolidBrush hoverBrush = new SolidBrush(Internal.Themes.ChromePainter.RowHoverColor()))
                {
                    e.Graphics.FillRectangle(hoverBrush, e.Bounds);
                }
            }

            Color dot = Internal.Themes.ThemeManager.GetCategoryColor(e.Item.Index);
            int midY = e.Bounds.Y + e.Bounds.Height / 2;
            SmoothingMode old = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush dotBrush = new SolidBrush(dot))
            {
                e.Graphics.FillEllipse(dotBrush, e.Bounds.X + 10, midY - 4, 8, 8);
            }
            if (category != null && category.IsDefault == 1)
            {
                using (Pen ring = new Pen(dot, 1.2F))
                {
                    e.Graphics.DrawEllipse(ring, e.Bounds.X + 7, midY - 7, 14, 14);
                }
            }
            if (category != null && category.EncryptMode == (int)CategoryEncryptMode.IndependentPassword)
            {
                DrawLock(e.Graphics, new Rectangle(e.Bounds.Right - 22, midY - 7, 12, 14), Internal.Themes.ThemeManager.GetColor("textSecondary"));
            }
            e.Graphics.SmoothingMode = old;

            Rectangle textRect = new Rectangle(e.Bounds.X + 28, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 52), e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem?.Text ?? string.Empty,
                _categoryList.Font,
                textRect,
                Internal.Themes.ThemeManager.GetColor("text"),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(ThemeManager.GetColor("accent")))
                {
                    e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + ScalePixel(9), ScalePixel(3), e.Bounds.Height - ScalePixel(18));
                }
                if (_categoryList.Focused)
                {
                    ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -3, -3));
                }
            }
        }

        /// <summary>
        /// 笔记列表表头
        /// </summary>
        private void NoteListDrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            Color back = Internal.Themes.ThemeManager.GetColor("panelBack");
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            Rectangle textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 8), e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                e.Header?.Text ?? string.Empty,
                _noteList.Font,
                textRect,
                Internal.Themes.ThemeManager.GetColor("textSecondary"),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (Pen pen = new Pen(Internal.Themes.ThemeManager.GetColor("border")))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }

        /// <summary>
        /// 笔记行：置顶菱形、回收站色
        /// </summary>
        private void NoteListDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item == null || e.ColumnIndex != 0)
            {
                return;
            }

            Note? note = e.Item.Tag as Note;
            bool selected = e.Item.Selected;
            bool hovered = !selected && e.Item.Index == _noteHoverIndex;
            Color back = ThemeManager.GetColor(selected ? "selectionBack" : "panelAltBack");
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            if (hovered)
            {
                using (SolidBrush brush = new SolidBrush(ChromePainter.RowHoverColor()))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
            }
            int inset = ScalePixel(16);
            int titleY = e.Bounds.Y + ScalePixel(15);
            Color fore = ThemeManager.GetColor(note?.Status == (int)NoteStatus.Trashed ? "trashTint" : "text");
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(ThemeManager.GetColor("accent")))
                {
                    e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + ScalePixel(10), ScalePixel(3), e.Bounds.Height - ScalePixel(20));
                }
            }
            int pinWidth = note?.IsPinned == 1 ? ScalePixel(16) : 0;
            using (Font titleFont = new Font(_noteList.Font, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, e.Item.Text, titleFont,
                    new Rectangle(e.Bounds.X + inset, titleY, Math.Max(0, e.Bounds.Width - inset * 2 - pinWidth), ScalePixel(26)),
                    fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if (pinWidth > 0)
            {
                DrawPin(e.Graphics, new Rectangle(e.Bounds.Right - inset - pinWidth, titleY, pinWidth, ScalePixel(24)), ThemeManager.GetColor("pinnedMark"));
            }
            string type = note == null ? string.Empty : note.ContentType == (int)ContentType.Markdown ? "Markdown" : note.ContentType == (int)ContentType.RichText ? "富文本" : "纯文本";
            string time = note == null ? string.Empty : (note.UpdateTime ?? note.CreateTime).ToLocalTime().ToString("MM-dd HH:mm");
            string detail = type + "  ·  " + time + "  ·  " + (note?.WordCount ?? 0) + " 字";
            using (Font detailFont = new Font(_noteList.Font.FontFamily, 8.5F))
            {
                TextRenderer.DrawText(e.Graphics, detail, detailFont,
                    new Rectangle(e.Bounds.X + inset, e.Bounds.Y + ScalePixel(48), Math.Max(0, e.Bounds.Width - inset * 2), ScalePixel(22)),
                    ThemeManager.GetColor("textSecondary"), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            using (Pen separator = new Pen(ThemeManager.GetColor("border")))
            {
                e.Graphics.DrawLine(separator, e.Bounds.X + inset, e.Bounds.Bottom - 1, e.Bounds.Right - inset, e.Bounds.Bottom - 1);
            }
            if (selected && _noteList.Focused)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -3, -3), fore, back);
            }
        }

        /// <summary>
        /// 置顶标记
        /// </summary>
        private static void DrawPin(Graphics graphics, Rectangle bounds, Color color)
        {
            int cx = bounds.X + 12;
            int cy = bounds.Y + bounds.Height / 2;
            Point[] diamond = new Point[]
            {
                new Point(cx, cy - 5),
                new Point(cx + 4, cy),
                new Point(cx, cy + 5),
                new Point(cx - 4, cy)
            };
            SmoothingMode old = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.FillPolygon(brush, diamond);
            }
            graphics.SmoothingMode = old;
        }

        /// <summary>
        /// 独立密码锁形
        /// </summary>
        private static void DrawLock(Graphics graphics, Rectangle box, Color color)
        {
            using (Pen pen = new Pen(color, 1.4F))
            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.DrawArc(pen, box.X + 2, box.Y, box.Width - 4, box.Width - 4, 200, 140);
                graphics.FillRectangle(brush, box.X, box.Y + 6, box.Width, box.Height - 6);
            }
        }

        /// <summary>
        /// 右键菜单跟当前主题
        /// </summary>
        private void StyleMenus()
        {
            Internal.Themes.ChromePainter.StyleMenu(_categoryMenu);
            Internal.Themes.ChromePainter.StyleMenu(_noteMenu);
            Internal.Themes.ChromePainter.StyleMenu(_trashNoteMenu);
            Internal.Themes.ChromePainter.StyleMenu(_newNoteMenu);
        }

        /// <summary>
        /// 搜索防抖到点
        /// </summary>
        private async void SearchTimerTick(object? sender, EventArgs e)
        {
            if (_closing || IsDisposed)
            {
                return;
            }
            _searchTimer?.Stop();
            _searchKeyword = _searchBox.Text.Trim();
            if (_trashView && _searchKeyword.Length > 0)
            {
                // 回收站不支持搜索,回到普通搜索视图
                _trashView = false;
                SyncViewMode();
            }
            await ReloadNotesAsync();
        }

        /// <summary>
        /// 切换回收站视图
        /// </summary>
        private async void ToggleTrashView()
        {
            FlushEditorPendingSave();
            _trashView = !_trashView;
            if (_trashView)
            {
                _searchKeyword = string.Empty;
                SetSearchText(string.Empty);
            }
            SyncViewMode();
            ClearEditor();
            await ReloadNotesAsync();
        }

        /// <summary>
        /// 同步工具条/侧栏/按钮的视图状态
        /// </summary>
        private void SyncViewMode()
        {
            _normalToolbar.Visible = !_trashView;
            _trashToolbar.Visible = _trashView;
            _trashButton.Tag = _trashView ? "theme:accent" : null;
            Internal.Themes.ThemeManager.Apply(_trashButton.Parent!);
            if (_trashView)
            {
                foreach (ListViewItem item in _categoryList.Items)
                {
                    item.Selected = false;
                }
            }
        }

        /// <summary>
        /// 笔记列表右键菜单
        /// </summary>
        private void NoteListMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }
            ListViewHitTestInfo hit = _noteList.HitTest(e.Location);
            if (hit.Item == null)
            {
                return;
            }
            foreach (ListViewItem item in _noteList.Items)
            {
                item.Selected = ReferenceEquals(item, hit.Item);
            }
            (_trashView ? _trashNoteMenu : _noteMenu).Show(Cursor.Position);
        }

        /// <summary>
        /// 当前选中笔记
        /// </summary>
        private Note? SelectedNote()
        {
            return _noteList.SelectedItems.Count > 0 ? _noteList.SelectedItems[0].Tag as Note : null;
        }

        /// <summary>
        /// 选中笔记：解锁校验→读取解密→装载编辑器
        /// </summary>
        private async void OnNoteSelected(object? sender, EventArgs e)
        {
            _pinButton.Enabled = _noteList.SelectedItems.Count > 0 && !_trashView;
            _deleteButton.Enabled = _noteList.SelectedItems.Count > 0 && !_trashView;

            if (_closing)
            {
                return;
            }

            Note? note = SelectedNote();
            if (note == null || ReferenceEquals(note, _currentNote))
            {
                return;
            }
            await OpenNoteAsync(note);
        }

        /// <summary>
        /// 打开笔记(解锁/解密/装载)。解密在后台进行，过期的打开请求会被丢掉。
        /// </summary>
        private async Task OpenNoteAsync(Note note)
        {
            int requestId = ++_noteRequestId;
            bool edit = _openForEdit && !_trashView;
            _openForEdit = false;
            FlushEditorPendingSave();
            if (IsDisposed || requestId != _noteRequestId)
            {
                return;
            }

            Category? category = FindCategory(note.CategoryId);
            if (category == null)
            {
                ShowEditorMessage("笔记所属分类不存在");
                ShowToast("笔记所属分类不存在", ToastKind.Error);
                return;
            }

            byte[] contentKey;
            try
            {
                contentKey = _session.GetContentKey(category);
            }
            catch (InvalidOperationException)
            {
                if (!UnlockCategory(category))
                {
                    ShowEditorMessage("分类「" + category.Name + "」已锁定,解锁后才能查看");
                    _currentNote = null;
                    return;
                }
                contentKey = _session.GetContentKey(category);
            }

            if (IsDisposed || requestId != _noteRequestId)
            {
                return;
            }

            string title = note.Title.Length == 0 ? "无标题" : note.Title;
            ShowEditorMessage("正在解密「" + title + "」…");
            R<NoteDetailDto> detail;
            try
            {
                detail = await RunMainAsync("正在解密「" + title + "」…", () => GetService<INoteService>().GetNoteDetail(note.Id, contentKey));
            }
            catch (Exception ex)
            {
                if (IsDisposed || requestId != _noteRequestId)
                {
                    return;
                }
                ShowEditorMessage("读取失败");
                ShowToast("读取失败：" + ex.Message, ToastKind.Error);
                return;
            }

            if (IsDisposed || requestId != _noteRequestId)
            {
                return;
            }

            if (!detail.IsSuccess || detail.Data == null)
            {
                _currentNote = null;
                string message = detail.Message ?? "读取失败";
                ShowEditorMessage(message);
                ShowToast(message, ToastKind.Error);
                return;
            }

            _currentNote = note;
            HideEditorMessage();
            NoteEditorBase editor = ActivateEditor(detail.Data.ContentType);
            ApplyReadingMode(edit);
            _titleBox.Text = detail.Data.Title;
            _typeLabel.Text = TypeName(detail.Data.ContentType);
            _metaLabel.Text = "修改 " + (detail.Data.UpdateTime ?? detail.Data.CreateTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                + " · " + detail.Data.WordCount + " 字";
            editor.LoadNote(detail.Data);
            SaveLastNoteId(note.Id);
        }

        /// <summary>
        /// 在只读查看与编辑之间切换。回收站只允许查看。
        /// </summary>
        private void ToggleEditMode()
        {
            if (_editing)
            {
                FinishEdit();
            }
            else
            {
                EnterEdit();
            }
        }

        /// <summary>
        /// 进入编辑。打开已有笔记时默认不调用，需用户明确选择。
        /// </summary>
        private void EnterEdit()
        {
            if (_currentNote == null || _trashView || _editing)
            {
                return;
            }

            ApplyReadingMode(true);
            _titleBox.Focus();
        }

        /// <summary>
        /// 结束编辑：先保存未写入的修改，再回到只读查看
        /// </summary>
        private void FinishEdit()
        {
            if (!_editing)
            {
                return;
            }

            _announceNextSave = true;
            FlushEditorPendingSave();
            _announceNextSave = false;
            ApplyReadingMode(false);
        }

        /// <summary>
        /// 应用查看或编辑：标题、正文、附件增删一起切换
        /// </summary>
        /// <param name="editing">true 为可编辑</param>
        private void ApplyReadingMode(bool editing)
        {
            bool canEdit = _currentNote != null && !_trashView;
            _editing = editing && canEdit;
            _titleBox.ReadOnly = !_editing;
            _editToggleButton.Text = _editing ? "完成" : "编辑";
            _editToggleButton.Enabled = canEdit;
            _activeEditor?.SetReadOnly(!_editing);
            _attachmentPanel.SetReadOnly(!_editing);
            if (_currentNote != null)
            {
                LoadAttachments(_currentNote.Id);
            }
        }

        /// <summary>
        /// 弹解锁框解锁独立密码分类
        /// </summary>
        private bool UnlockCategory(Category category)
        {
            using (PasswordDialogForm form = new PasswordDialogForm(App, "解锁分类",
                "分类「" + category.Name + "」使用独立密码加密。", "分类独立密码"))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }
                if (GetService<ICategoryService>().TryUnlockCategory(category.Id, form.Values[0], out byte[]? categoryKey)
                    && categoryKey != null)
                {
                    _session.MarkCategoryUnlocked(category.Id, categoryKey);
                    return true;
                }
                ShowToast("独立密码错误", ToastKind.Error);
                return false;
            }
        }

        /// <summary>
        /// 按 Id 找分类
        /// </summary>
        private Category? FindCategory(int categoryId)
        {
            return _categories.FirstOrDefault(m => m.Id == categoryId);
        }

        /// <summary>
        /// 新建笔记
        /// </summary>
        private async void CreateNote(ContentType contentType)
        {
            if (_currentCategory == null)
            {
                ShowToast("请先选择分类", ToastKind.Info);
                return;
            }
            Category category = _currentCategory;
            try
            {
                _session.GetContentKey(category);
            }
            catch (InvalidOperationException)
            {
                if (!UnlockCategory(category))
                {
                    return;
                }
            }

            FlushEditorPendingSave();
            if (_trashView)
            {
                _trashView = false;
                SyncViewMode();
            }
            _searchKeyword = string.Empty;
            SetSearchText(string.Empty);

            Note note = new Note
            {
                UserId = _session.User!.Id,
                CategoryId = category.Id,
                Title = string.Empty,
                ContentType = (int)contentType
            };
            byte[] contentKey = _session.GetContentKey(category);
            R result;
            try
            {
                result = await RunMainAsync("正在创建笔记…", () => GetService<INoteService>().SaveNote(note, string.Empty, contentKey));
            }
            catch (Exception ex)
            {
                ShowToast("创建失败：" + ex.Message, ToastKind.Error);
                return;
            }
            if (!result.IsSuccess)
            {
                ShowToast(result.Message ?? "创建失败", ToastKind.Error);
                return;
            }

            await ReloadNotesAsync();
            _openForEdit = true;
            foreach (ListViewItem item in _noteList.Items)
            {
                if (((Note)item.Tag!).Id == note.Id)
                {
                    item.Selected = true;
                    break;
                }
            }
            _titleBox.Focus();
            ShowToast("已创建" + TypeName(contentType) + "笔记", ToastKind.Success);
        }

        /// <summary>
        /// 置顶/取消置顶
        /// </summary>
        private async void TogglePinned()
        {
            Note? note = SelectedNote() ?? _currentNote;
            if (note == null)
            {
                ShowToast("请先选择一条笔记", ToastKind.Info);
                return;
            }
            bool pin = note.IsPinned != 1;
            try
            {
                R result = await RunMainAsync(pin ? "正在置顶…" : "正在取消置顶…", () => GetService<INoteService>().UpdatePinned(note.Id, pin));
                if (!result.IsSuccess)
                {
                    ShowToast(result.Message ?? "更新失败", ToastKind.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowToast("更新失败：" + ex.Message, ToastKind.Error);
                return;
            }
            note.IsPinned = pin ? 1 : 0;
            await ReloadNotesAsync();
            SelectNote(note.Id);
            ShowToast(pin ? "已置顶" : "已取消置顶", ToastKind.Success);
        }

        /// <summary>
        /// 移入回收站
        /// </summary>
        private async void TrashSelectedNote()
        {
            Note? note = SelectedNote();
            if (note == null)
            {
                ShowToast("请先选择一条笔记", ToastKind.Info);
                return;
            }

            FlushEditorPendingSave();
            int userId = _session.User!.Id;
            int noteId = note.Id;
            try
            {
                await RunMainAsync("正在移入回收站…", () =>
                {
                    GetService<INoteService>().MoveToTrashList(new List<int> { noteId }, userId);
                    return true;
                });
            }
            catch (Exception ex)
            {
                ShowToast("移入回收站失败：" + ex.Message, ToastKind.Error);
                return;
            }
            if (ReferenceEquals(note, _currentNote))
            {
                ClearEditor();
            }
            await ReloadNotesAsync();
            ShowToast("已移入回收站", ToastKind.Success);
        }

        /// <summary>
        /// 从回收站恢复
        /// </summary>
        private async void RestoreSelectedNote()
        {
            Note? note = SelectedNote();
            if (note == null)
            {
                return;
            }

            FlushEditorPendingSave();
            int userId = _session.User!.Id;
            int noteId = note.Id;
            try
            {
                await RunMainAsync("正在恢复…", () =>
                {
                    GetService<INoteService>().RestoreList(new List<int> { noteId }, userId);
                    return true;
                });
            }
            catch (Exception ex)
            {
                ShowToast("恢复失败：" + ex.Message, ToastKind.Error);
                return;
            }
            if (ReferenceEquals(note, _currentNote))
            {
                ClearEditor();
            }
            await ReloadNotesAsync();
            ShowToast("已恢复笔记", ToastKind.Success);
        }

        /// <summary>
        /// 彻底删除(附件级联)
        /// </summary>
        private async void PurgeSelectedNote()
        {
            Note? note = SelectedNote();
            if (note == null)
            {
                return;
            }

            DialogResult choice = MessageBox.Show(
                "彻底删除笔记「" + (note.Title.Length == 0 ? "无标题" : note.Title) + "」及其全部附件？此操作不可恢复。",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes)
            {
                return;
            }

            FlushEditorPendingSave();
            int userId = _session.User!.Id;
            int noteId = note.Id;
            try
            {
                await RunMainAsync("正在彻底删除…", () =>
                {
                    GetService<INoteService>().PurgeList(new List<int> { noteId }, userId);
                    return true;
                });
            }
            catch (Exception ex)
            {
                ShowToast("彻底删除失败：" + ex.Message, ToastKind.Error);
                return;
            }
            if (ReferenceEquals(note, _currentNote))
            {
                ClearEditor();
            }
            await ReloadNotesAsync();
            ShowToast("已彻底删除", ToastKind.Success);
        }

        /// <summary>
        /// 清空回收站
        /// </summary>
        private async void EmptyTrash()
        {
            DialogResult choice = MessageBox.Show("清空回收站？其中全部笔记及附件将被彻底删除,不可恢复。",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes)
            {
                return;
            }

            FlushEditorPendingSave();
            int userId = _session.User!.Id;
            try
            {
                await RunMainAsync("正在清空回收站…", () =>
                {
                    GetService<INoteService>().EmptyTrash(userId);
                    return true;
                });
            }
            catch (Exception ex)
            {
                ShowToast("清空失败：" + ex.Message, ToastKind.Error);
                return;
            }
            ClearEditor();
            await ReloadNotesAsync();
            ShowToast("回收站已清空", ToastKind.Success);
        }

        /// <summary>
        /// 记住上次打开的笔记
        /// </summary>
        private void SaveLastNoteId(int noteId)
        {
            if (_userSetting.LastNoteId == noteId)
            {
                return;
            }
            _userSetting.LastNoteId = noteId;
            GetService<IUserSettingService>().SaveUserSetting(_userSetting);
        }

        /// <summary>
        /// 登录后恢复上次打开的笔记。返回 false 表示没有可恢复的笔记。
        /// </summary>
        private async Task<bool> TryRestoreLastNoteAsync()
        {
            if (_userSetting.LastNoteId <= 0)
            {
                return false;
            }
            Note? note = GetService<INoteService>().GetNoteById(_userSetting.LastNoteId);
            if (note == null || note.Status != (int)NoteStatus.Normal || note.UserId != _session.User!.Id)
            {
                return false;
            }

            int index = _categories.FindIndex(m => m.Id == note.CategoryId);
            if (index < 0)
            {
                return false;
            }

            _suppressCategoryEvent = true;
            try
            {
                _categoryList.SelectedIndices.Clear();
                _categoryList.SelectedIndices.Add(index);
                _currentCategory = (Category)_categoryList.Items[index].Tag!;
                _trashView = false;
                SyncViewMode();
            }
            finally
            {
                _suppressCategoryEvent = false;
            }

            await ReloadNotesAsync();
            SelectNote(note.Id);
            return true;
        }

        /// <summary>
        /// 按 Id 选中列表中的笔记
        /// </summary>
        private void SelectNote(int noteId)
        {
            foreach (ListViewItem item in _noteList.Items)
            {
                if (item.Tag is Note listed && listed.Id == noteId)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        #endregion

        #region 编辑器

        /// <summary>
        /// 冲写编辑器未保存内容(切笔记/切视图/关窗/锁定前调用)
        /// </summary>
        private void FlushEditorPendingSave()
        {
            _activeEditor?.FlushPendingSave();
            DrainSaveQueue();
        }

        /// <summary>
        /// 保存当前笔记(标题+正文加密写库)。加密写库在后台排队，界面保持可重绘。
        /// </summary>
        private void SaveCurrentNote()
        {
            if (_currentNote == null || _activeEditor == null)
            {
                return;
            }
            Note note = _currentNote;
            Category? category = FindCategory(note.CategoryId);
            if (category == null)
            {
                return;
            }

            byte[] contentKey;
            try
            {
                contentKey = _session.GetContentKey(category);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            note.Title = _titleBox.Text.Trim();
            SaveSnapshot snapshot = new SaveSnapshot
            {
                Note = note,
                Title = note.Title,
                Content = _activeEditor.CollectContent(),
                Key = contentKey,
                WordCount = _activeEditor.GetWordCount(),
                Announce = _announceNextSave
            };
            _announceNextSave = false;
            if (ReferenceEquals(_currentNote, note))
            {
                _metaLabel.Text = "正在保存…";
            }
            Task previous = _saveQueue;
            _saveQueue = PersistAsync(previous, snapshot);
        }

        /// <summary>
        /// 等到已排队的保存结束。等待期间泵消息，让状态条动画继续走。
        /// </summary>
        private void DrainSaveQueue()
        {
            int spins = 0;
            while (!_saveQueue.IsCompleted && spins < 20000)
            {
                Task observed = _saveQueue;
                while (!observed.IsCompleted && spins < 20000)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(15);
                    spins++;
                }
            }
        }

        /// <summary>
        /// 串行保存一笔快照，避免和列表解密抢数据库
        /// </summary>
        private async Task PersistAsync(Task previous, SaveSnapshot snapshot)
        {
            try
            {
                await previous;
            }
            catch
            {
                // 上一笔的失败已经提示过
            }

            if (IsDisposed)
            {
                return;
            }

            PushStatus("正在保存…");
            try
            {
                R result;
                try
                {
                    result = await UiWork.RunAsync(() => GetService<INoteService>().SaveNote(snapshot.Note, snapshot.Content, snapshot.Key));
                }
                catch (Exception ex)
                {
                    result = R.Failure(ex.Message);
                }

                if (IsDisposed)
                {
                    return;
                }

                if (!result.IsSuccess)
                {
                    ShowToast(result.Message ?? "保存失败", ToastKind.Error);
                    if (ReferenceEquals(_currentNote, snapshot.Note))
                    {
                        _metaLabel.Text = "保存失败";
                    }
                    return;
                }

                bool sameText = ReferenceEquals(_currentNote, snapshot.Note)
                    && _activeEditor != null
                    && _titleBox.Text.Trim() == snapshot.Title
                    && _activeEditor.CollectContent() == snapshot.Content;
                if (sameText)
                {
                    _activeEditor!.MarkSaved();
                    _metaLabel.Text = "已保存 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · " + snapshot.WordCount + " 字";
                }
                else if (ReferenceEquals(_currentNote, snapshot.Note) && _activeEditor != null)
                {
                    _metaLabel.Text = "修改中 · " + _activeEditor.GetWordCount() + " 字";
                }
                RefreshNoteItem(snapshot.Note);
                if (snapshot.Announce)
                {
                    ShowToast("笔记已保存", ToastKind.Success);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    PopStatus();
                }
            }
        }

        /// <summary>
        /// 刷新列表中的笔记行
        /// </summary>
        private void RefreshNoteItem(Note note)
        {
            string title = note.Title.Length == 0 ? "无标题" : note.Title;
            DateTime time = (note.UpdateTime ?? note.CreateTime).ToLocalTime();
            foreach (ListViewItem item in _noteList.Items)
            {
                if (item.Tag is Note listed && listed.Id == note.Id)
                {
                    item.Tag = note;
                    item.Text = title;
                    if (item.SubItems.Count > 1)
                    {
                        item.SubItems[1].Text = time.ToString("MM-dd HH:mm");
                    }
                    if (item.SubItems.Count > 2)
                    {
                        item.SubItems[2].Text = note.WordCount.ToString();
                    }
                    _noteList.Invalidate(item.Bounds);
                    break;
                }
            }
        }

        /// <summary>
        /// 激活(并按需创建)指定类型的编辑器
        /// </summary>
        private NoteEditorBase ActivateEditor(ContentType contentType)
        {
            if (!_editors.TryGetValue(contentType, out NoteEditorBase? editor))
            {
                editor = contentType switch
                {
                    ContentType.RichText => new RichNoteEditor(),
                    ContentType.Markdown => new MarkdownNoteEditor(),
                    _ => new PlainNoteEditor()
                };
                editor.SaveRequested += (sender, e) => SaveCurrentNote();
                editor.ContentChanged += (sender, e) =>
                {
                    if (_activeEditor != null)
                    {
                        _metaLabel.Text = "修改中 · " + _activeEditor.GetWordCount() + " 字";
                    }
                };
                editor.SetEditorFont(_userSetting.EditorFontFamily, _userSetting.EditorFontSize);
                editor.Dock = DockStyle.Fill;
                editor.Visible = false;
                _editorHost.Controls.Add(editor);
                _editors[contentType] = editor;
            }

            foreach (KeyValuePair<ContentType, NoteEditorBase> pair in _editors)
            {
                pair.Value.Visible = ReferenceEquals(pair.Value, editor);
            }
            _activeEditor = editor;
            Internal.Themes.ThemeManager.Apply(editor);
            return editor;
        }

        /// <summary>
        /// 应用编辑器字体偏好
        /// </summary>
        private void ApplyEditorFonts()
        {
            foreach (KeyValuePair<ContentType, NoteEditorBase> pair in _editors)
            {
                pair.Value.SetEditorFont(_userSetting.EditorFontFamily, _userSetting.EditorFontSize);
            }
        }

        /// <summary>
        /// 清空编辑区
        /// </summary>
        private void ClearEditor()
        {
            _currentNote = null;
            _editing = false;
            _activeEditor?.SetReadOnly(true);
            _titleBox.ReadOnly = true;
            _titleBox.Text = string.Empty;
            _activeEditor?.MarkSaved();
            _editToggleButton.Text = "编辑";
            _editToggleButton.Enabled = false;
            _attachmentPanel.SetReadOnly(true);
            _metaLabel.Text = "选择或新建一条笔记";
            _typeLabel.Text = string.Empty;
            foreach (KeyValuePair<ContentType, NoteEditorBase> pair in _editors)
            {
                pair.Value.Visible = false;
                pair.Value.SetReadOnly(true);
            }
            _activeEditor = null;
            _attachmentPanel.LoadAttachments(new List<NoteAttachment>());
        }

        /// <summary>
        /// 在编辑区显示提示信息(如未解锁/解密失败)
        /// </summary>
        private void ShowEditorMessage(string message)
        {
            ClearEditor();
            _editorMessage.Text = message;
            _editorMessage.Visible = true;
        }

        /// <summary>
        /// 隐藏编辑区提示信息
        /// </summary>
        private void HideEditorMessage()
        {
            _editorMessage.Visible = false;
        }

        /// <summary>
        /// 类型显示名
        /// </summary>
        private static string TypeName(ContentType contentType)
        {
            return contentType switch
            {
                ContentType.RichText => "富文本",
                ContentType.Markdown => "Markdown",
                _ => "纯文本"
            };
        }

        #endregion

        #region 附件

        /// <summary>
        /// 加载附件列表
        /// </summary>
        private void LoadAttachments(int noteId)
        {
            _attachmentPanel.LoadAttachments(GetService<INoteAttachmentService>().GetAttachmentList(noteId));
        }

        /// <summary>
        /// 添加附件(文件选择→加密入库)
        /// </summary>
        private async void AddAttachment()
        {
            if (_currentNote == null || !_editing)
            {
                ShowToast("请先进入编辑", ToastKind.Info);
                return;
            }
            Category? category = FindCategory(_currentNote.CategoryId);
            if (category == null)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "选择附件(将加密保存)"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                byte[] contentKey;
                try
                {
                    contentKey = _session.GetContentKey(category);
                }
                catch (InvalidOperationException)
                {
                    ShowToast("分类已锁定,无法添加附件", ToastKind.Error);
                    return;
                }

                int userId = _session.User!.Id;
                int noteId = _currentNote.Id;
                int saved = 0;
                foreach (string file in dialog.FileNames)
                {
                    string fileName = Path.GetFileName(file);
                    byte[] plainBytes;
                    try
                    {
                        plainBytes = await RunMainAsync("正在读取「" + fileName + "」…", () => File.ReadAllBytes(file));
                    }
                    catch (Exception ex)
                    {
                        ShowToast("读取文件失败：" + ex.Message, ToastKind.Error);
                        continue;
                    }

                    if (plainBytes.Length > AttachmentPanel.SIZE_WARNING_BYTES
                        && MessageBox.Show("文件「" + fileName + "」超过 50MB,仍要加密保存吗？",
                            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        continue;
                    }

                    string mimeType = GuessMimeType(file);
                    try
                    {
                        R result = await RunMainAsync("正在加密「" + fileName + "」…", () => GetService<INoteAttachmentService>().AddAttachment(
                            userId, noteId, fileName, mimeType, plainBytes, contentKey));
                        if (!result.IsSuccess)
                        {
                            ShowToast(result.Message ?? "附件保存失败", ToastKind.Error);
                            continue;
                        }
                        saved++;
                    }
                    catch (Exception ex)
                    {
                        ShowToast("附件保存失败：" + ex.Message, ToastKind.Error);
                    }
                }
                if (_currentNote != null && _currentNote.Id == noteId)
                {
                    LoadAttachments(noteId);
                }
                if (saved > 0)
                {
                    ShowToast(saved == 1 ? "已加密保存 1 个附件" : "已加密保存 " + saved + " 个附件", ToastKind.Success);
                }
            }
        }

        /// <summary>
        /// 打开附件：图片内存预览,其他类型导出
        /// </summary>
        private async void OpenAttachment(NoteAttachment attachment)
        {
            if (_currentNote == null)
            {
                return;
            }
            Category? category = FindCategory(_currentNote.CategoryId);
            if (category == null)
            {
                return;
            }

            byte[] contentKey;
            try
            {
                contentKey = _session.GetContentKey(category);
            }
            catch (InvalidOperationException)
            {
                ShowToast("分类已锁定", ToastKind.Error);
                return;
            }

            byte[]? plainBytes;
            try
            {
                plainBytes = await RunMainAsync("正在解密附件…", () => GetService<INoteAttachmentService>().DecryptAttachment(attachment.Id, contentKey));
            }
            catch (Exception ex)
            {
                ShowToast("附件解密失败：" + ex.Message, ToastKind.Error);
                return;
            }
            if (plainBytes == null)
            {
                ShowToast("附件解密失败(数据损坏或密钥不符)", ToastKind.Error);
                return;
            }

            if (attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                using (ImagePreviewForm form = new ImagePreviewForm(App, attachment.FileName, plainBytes))
                {
                    form.ShowDialog(this);
                }
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "导出附件(解密保存)",
                FileName = attachment.FileName
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string path = dialog.FileName;
                try
                {
                    await RunMainAsync("正在导出…", () =>
                    {
                        File.WriteAllBytes(path, plainBytes);
                        return true;
                    });
                    ShowToast("已导出：" + path, ToastKind.Success);
                }
                catch (Exception ex)
                {
                    ShowToast("导出失败：" + ex.Message, ToastKind.Error);
                }
            }
        }

        /// <summary>
        /// 删除附件
        /// </summary>
        private async void RemoveAttachment(NoteAttachment attachment)
        {
            if (!_editing)
            {
                return;
            }

            try
            {
                R result = await RunMainAsync("正在移除附件…", () => GetService<INoteAttachmentService>().RemoveAttachment(attachment.Id));
                if (!result.IsSuccess)
                {
                    ShowToast(result.Message ?? "移除失败", ToastKind.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowToast("移除失败：" + ex.Message, ToastKind.Error);
                return;
            }
            if (_currentNote != null)
            {
                LoadAttachments(_currentNote.Id);
            }
            ShowToast("已移除附件", ToastKind.Success);
        }

        /// <summary>
        /// 按扩展名猜 MIME 类型
        /// </summary>
        private static string GuessMimeType(string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };
        }

        #endregion

        #region 设置/锁定/主题

        /// <summary>
        /// 打开设置窗体并应用变更
        /// </summary>
        private void OpenSettings()
        {
            FlushEditorPendingSave();
            using (SettingsForm form = new SettingsForm(App, _session, _userSetting))
            {
                form.ShowDialog(this);
            }
            _autoLock?.UpdateSettings(_userSetting.AutoLockMinutes);
            ApplyEditorFonts();
            _userLabel.Text = _session.User?.DisplayName ?? string.Empty;
            Text = NotexBrand.DisplayName + " - " + _userLabel.Text;
        }

        /// <summary>
        /// 主题切换：重刷自身与编辑器
        /// </summary>
        private void OnThemeChanged(Internal.Themes.Theme theme)
        {
            if (IsHandleCreated)
            {
                Internal.Themes.ThemeManager.Apply(this);
                ApplyFramePadding();
                StyleMenus();
            }
        }

        /// <summary>
        /// 自动锁定：冲写内容后标记原因并关闭(回登录界面)
        /// </summary>
        private void OnAutoLockRequested()
        {
            BeginInvoke((Action)(() =>
            {
                FlushEditorPendingSave();
                ExitReason = MainExitReason.AutoLocked;
                Close();
            }));
        }

        #endregion

        #region 系统托盘

        /// <summary>
        /// 尺寸变化：进入最小化且偏好开启时隐藏到托盘；否则记录常态窗口状态供恢复使用
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_closing)
            {
                return;
            }

            if (WindowState == FormWindowState.Minimized)
            {
                if (_userSetting.MinimizeToTray == 1)
                {
                    HideToTray();
                }
            }
            else
            {
                _stateBeforeTray = WindowState;
            }
        }

        /// <summary>
        /// 最小化隐藏到系统托盘：图标常驻,首次给出气泡提示
        /// </summary>
        private void HideToTray()
        {
            if (!Visible)
            {
                // 已在托盘(隐藏引发的重复 Resize)
                return;
            }

            FlushEditorPendingSave();
            _trayIcon ??= BuildTrayIcon();
            _trayIcon.Text = NotexBrand.DisplayName + " - " + (_session.User?.DisplayName ?? string.Empty);
            _trayIcon.Visible = true;
            if (!_trayTipShown)
            {
                _trayTipShown = true;
                _trayIcon.ShowBalloonTip(2500, NotexBrand.DisplayName, "已最小化到系统托盘，双击图标恢复窗口。", ToolTipIcon.Info);
            }
            Hide();
        }

        /// <summary>
        /// 从托盘恢复主窗体(还原到最小化前的常态/最大化)
        /// </summary>
        private void RestoreFromTray()
        {
            if (_closing)
            {
                return;
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
            }
            Show();
            WindowState = _stateBeforeTray;
            Activate();
        }

        /// <summary>
        /// 构建托盘图标：双击恢复,右键菜单(显示/立即锁定/退出)
        /// </summary>
        private NotifyIcon BuildTrayIcon()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示" + NotexBrand.DisplayName, null, (sender, e) => RestoreFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("立即锁定", null, (sender, e) => BeginInvoke((Action)(() =>
            {
                FlushEditorPendingSave();
                ExitReason = MainExitReason.AutoLocked;
                Close();
            })));
            menu.Items.Add("退出", null, (sender, e) => BeginInvoke((Action)(() =>
            {
                FlushEditorPendingSave();
                ExitReason = MainExitReason.Close;
                Close();
            })));
            Internal.Themes.ChromePainter.StyleMenu(menu);

            NotifyIcon trayIcon = new NotifyIcon
            {
                Icon = Internal.Themes.ChromeAssets.AppIcon,
                ContextMenuStrip = menu,
                Visible = false
            };
            trayIcon.MouseDoubleClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    RestoreFromTray();
                }
            };
            return trayIcon;
        }

        #endregion

        /// <summary>
        /// 主窗体忙碌：底部状态条上的不定进度，不把三栏顶下去
        /// </summary>
        private async Task<T> RunMainAsync<T>(string message, Func<T> work)
        {
            PushStatus(message);
            try
            {
                return await UiWork.RunAsync(work);
            }
            finally
            {
                if (!IsDisposed)
                {
                    PopStatus();
                }
            }
        }

        /// <summary>
        /// 记下正在进行的操作
        /// </summary>
        private void PushStatus(string message)
        {
            _statusBusy++;
            _statusLabel.Text = message;
            _statusProgressHost.Visible = true;
            UseWaitCursor = true;
        }

        /// <summary>
        /// 结束一层操作。全部结束时回到列表摘要。
        /// </summary>
        private void PopStatus()
        {
            _statusBusy = Math.Max(0, _statusBusy - 1);
            if (_statusBusy > 0)
            {
                return;
            }
            _statusProgressHost.Visible = false;
            UseWaitCursor = false;
            _statusLabel.Text = IdleStatus();
        }

        /// <summary>
        /// 空闲时的状态条文案
        /// </summary>
        private string IdleStatus()
        {
            if (_searchKeyword.Length > 0)
            {
                return "搜索「" + _searchKeyword + "」· " + _notes.Count + " 条";
            }
            if (_trashView)
            {
                return "回收站 · " + _notes.Count + " 条";
            }
            if (_currentCategory != null)
            {
                return _currentCategory.Name + " · " + _notes.Count + " 条";
            }
            return "就绪";
        }

        /// <summary>
        /// 改搜索框但不触发防抖搜索
        /// </summary>
        private void SetSearchText(string text)
        {
            _suppressSearch = true;
            try
            {
                _searchBox.Text = text;
            }
            finally
            {
                _suppressSearch = false;
            }
        }

        /// <summary>
        /// 一次保存的明文快照。后台线程只使用这里的副本，避免保存过程中读到另一条笔记。
        /// </summary>
        private sealed class SaveSnapshot
        {
            public Note Note { get; set; } = null!;

            public string Title { get; set; } = string.Empty;

            public string Content { get; set; } = string.Empty;

            public byte[] Key { get; set; } = Array.Empty<byte>();

            public int WordCount { get; set; }

            public bool Announce { get; set; }
        }
    }
}
