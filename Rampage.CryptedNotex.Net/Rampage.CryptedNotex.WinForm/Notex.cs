using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.WinForm.Controls;
using Rampage.CryptedNotex.WinForm.Forms;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        private AttachmentPanel _attachmentPanel = null!;
        private Button _trashButton = null!;
        private Label _userLabel = null!;
        private NoteEditorBase? _activeEditor;

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
        /// 构造主窗体
        /// </summary>
        /// <param name="app">应用上下文</param>
        /// <param name="session">加密会话</param>
        public Notex(IRampageApplication app, CryptoSession session) : base(app)
        {
            _session = session;
            Text = "加密笔记本 - " + (session.User?.DisplayName ?? string.Empty);
            ClientSize = new Size(1180, 700);
            MinimumSize = new Size(940, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUi();
            BuildMenus();
        }

        /// <summary>
        /// 加载：读偏好、装分类、恢复上次笔记、启动自动锁定
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _userSetting = GetService<IUserSettingService>().GetUserSetting(_session.User!.Id);
            ApplyEditorFonts();
            Internal.Themes.ThemeManager.CurrentChanged += OnThemeChanged;

            _autoLock = new AutoLockManager();
            _autoLock.UpdateSettings(_userSetting.AutoLockMinutes);
            _autoLock.AutoLockRequested += OnAutoLockRequested;

            LoadCategories();
            TryRestoreLastNote();
        }

        /// <summary>
        /// 关闭前：冲写未保存内容、解除监听
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            FlushEditorPendingSave();
            _autoLock?.Dispose();
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
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #region 界面构建

        /// <summary>
        /// 构建三栏界面
        /// </summary>
        private void BuildUi()
        {
            SplitContainer outer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 220,
                Panel1MinSize = 180
            };

            BuildSidebar(outer.Panel1);
            BuildContentArea(outer.Panel2);
            Controls.Add(outer);
        }

        /// <summary>
        /// 左侧分类侧栏
        /// </summary>
        private void BuildSidebar(Panel panel)
        {
            FlowLayoutPanel topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(6, 6, 6, 4)
            };
            Button newCategoryButton = new Button
            {
                Text = "＋ 新建分类",
                AutoSize = true
            };
            newCategoryButton.Click += (sender, e) => CreateCategory();
            topPanel.Controls.Add(newCategoryButton);
            panel.Controls.Add(topPanel);

            _trashButton = new Button
            {
                Text = "🗑 回收站",
                AutoSize = true
            };
            _trashButton.Click += (sender, e) => ToggleTrashView();
            topPanel.Controls.Add(_trashButton);

            _categoryList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                ShowGroups = false,
                HeaderStyle = ColumnHeaderStyle.None,
                MultiSelect = false
            };
            _categoryList.Columns.Add("分类", 190);
            _categoryList.SelectedIndexChanged += OnCategorySelected;
            _categoryList.MouseUp += CategoryListMouseUp;
            panel.Controls.Add(_categoryList);

            TableLayoutPanel bottomPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(6)
            };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            Button settingsButton = new Button
            {
                Text = "⚙ 设置",
                Dock = DockStyle.Fill
            };
            settingsButton.Click += (sender, e) => OpenSettings();
            bottomPanel.Controls.Add(settingsButton, 0, 0);

            Button aboutButton = new Button
            {
                Text = "ℹ 关于",
                Dock = DockStyle.Fill
            };
            aboutButton.Click += (sender, e) =>
            {
                using (AboutForm form = new AboutForm(App))
                {
                    form.ShowDialog(this);
                }
            };
            bottomPanel.Controls.Add(aboutButton, 1, 0);

            _userLabel = new Label
            {
                Text = "👤 " + (_session.User?.DisplayName ?? string.Empty),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "theme:muted"
            };
            bottomPanel.Controls.Add(_userLabel, 0, 1);

            Button logoutButton = new Button
            {
                Text = "登出",
                Dock = DockStyle.Fill
            };
            logoutButton.Click += (sender, e) =>
            {
                FlushEditorPendingSave();
                ExitReason = MainExitReason.Logout;
                Close();
            };
            bottomPanel.Controls.Add(logoutButton, 1, 1);
            panel.Controls.Add(bottomPanel);
        }

        /// <summary>
        /// 右侧内容区(笔记列表 + 编辑区)
        /// </summary>
        private void BuildContentArea(Panel panel)
        {
            SplitContainer inner = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 350,
                Panel1MinSize = 260
            };

            BuildNoteListColumn(inner.Panel1);
            BuildEditorColumn(inner.Panel2);
            panel.Controls.Add(inner);
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
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                Margin = new Padding(6, 6, 6, 2),
                PlaceholderText = "🔍 按标题搜索 (Ctrl+F)"
            };
            _searchBox.TextChanged += (sender, e) =>
            {
                _searchTimer ??= new Timer
                {
                    Interval = 300
                };
                _searchTimer.Stop();
                _searchTimer.Tick -= SearchTimerTick;
                _searchTimer.Tick += SearchTimerTick;
                _searchTimer.Start();
            };
            layout.Controls.Add(_searchBox, 0, 0);

            _normalToolbar = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(6, 2, 6, 2)
            };
            BuildNormalToolbar(_normalToolbar);
            layout.Controls.Add(_normalToolbar, 0, 1);

            _trashToolbar = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(6, 2, 6, 2),
                Visible = false
            };
            BuildTrashToolbar(_trashToolbar);
            layout.Controls.Add(_trashToolbar, 0, 1);

            _noteList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                Margin = new Padding(6, 2, 6, 6)
            };
            _noteList.Columns.Add("标题", 200);
            _noteList.Columns.Add("修改时间", 118);
            _noteList.Columns.Add("字数", 58);
            _noteList.SelectedIndexChanged += OnNoteSelected;
            _noteList.MouseUp += NoteListMouseUp;
            layout.Controls.Add(_noteList, 0, 2);
            panel.Controls.Add(layout);
        }

        /// <summary>
        /// 普通视图工具条(新建下拉/置顶/删除)
        /// </summary>
        private void BuildNormalToolbar(Panel panel)
        {
            Button newNoteButton = new Button
            {
                Text = "＋ 新建笔记 ▾",
                Location = new Point(0, 2),
                AutoSize = true
            };
            newNoteButton.Click += (sender, e) =>
            {
                Point location = newNoteButton.PointToScreen(new Point(0, newNoteButton.Height));
                _newNoteMenu.Show(location);
            };
            panel.Controls.Add(newNoteButton);

            Button pinButton = new Button
            {
                Text = "📌 置顶",
                Location = new Point(120, 2),
                AutoSize = true
            };
            pinButton.Click += (sender, e) => TogglePinned();
            panel.Controls.Add(pinButton);

            Button trashButton = new Button
            {
                Text = "🗑 删除",
                Location = new Point(200, 2),
                AutoSize = true
            };
            trashButton.Click += (sender, e) => TrashSelectedNote();
            panel.Controls.Add(trashButton);
        }

        /// <summary>
        /// 回收站视图工具条(恢复/彻底删除/清空)
        /// </summary>
        private void BuildTrashToolbar(Panel panel)
        {
            Button restoreButton = new Button
            {
                Text = "↩ 恢复",
                Location = new Point(0, 2),
                AutoSize = true
            };
            restoreButton.Click += (sender, e) => RestoreSelectedNote();
            panel.Controls.Add(restoreButton);

            Button purgeButton = new Button
            {
                Text = "✖ 彻底删除",
                Location = new Point(90, 2),
                AutoSize = true,
                Tag = "theme:danger"
            };
            purgeButton.Click += (sender, e) => PurgeSelectedNote();
            panel.Controls.Add(purgeButton);

            Button emptyButton = new Button
            {
                Text = "清空回收站",
                Location = new Point(210, 2),
                AutoSize = true
            };
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
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(6, 6, 6, 0)
            };

            _titleBox = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Microsoft YaHei UI", 12F),
                BorderStyle = BorderStyle.FixedSingle,
                Height = 32,
                PlaceholderText = "无标题(标题为明文,敏感信息请写正文)"
            };
            _titleBox.TextChanged += (sender, e) => _activeEditor?.NotifyTitleChanged();
            headerPanel.Controls.Add(_titleBox);

            FlowLayoutPanel metaRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(0, 4, 0, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _typeLabel = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                Padding = new Padding(0, 4, 12, 0),
                Tag = "theme:link"
            };
            metaRow.Controls.Add(_typeLabel);

            _metaLabel = new Label
            {
                Text = "选择或新建一条笔记",
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 0),
                Tag = "theme:muted"
            };
            metaRow.Controls.Add(_metaLabel);
            headerPanel.Controls.Add(metaRow);
            layout.Controls.Add(headerPanel, 0, 0);

            _editorHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 4, 6, 2),
                Padding = new Padding(1)
            };

            _editorMessage = new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = "theme:muted",
                Visible = false
            };
            _editorHost.Controls.Add(_editorMessage);
            layout.Controls.Add(_editorHost, 0, 1);

            _attachmentPanel = new AttachmentPanel
            {
                Margin = new Padding(6, 0, 6, 6)
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
            _noteMenu.Items.Add("📌 置顶/取消置顶", null, (sender, e) => TogglePinned());
            _noteMenu.Items.Add("🗑 移入回收站", null, (sender, e) => TrashSelectedNote());

            _trashNoteMenu = new ContextMenuStrip();
            _trashNoteMenu.Items.Add("↩ 恢复", null, (sender, e) => RestoreSelectedNote());
            _trashNoteMenu.Items.Add("✖ 彻底删除", null, (sender, e) => PurgeSelectedNote());

            _newNoteMenu = new ContextMenuStrip();
            _newNoteMenu.Items.Add("📝 纯文本笔记", null, (sender, e) => CreateNote(ContentType.PlainText));
            _newNoteMenu.Items.Add("🎨 富文本笔记(RTF)", null, (sender, e) => CreateNote(ContentType.RichText));
            _newNoteMenu.Items.Add("📄 Markdown 笔记", null, (sender, e) => CreateNote(ContentType.Markdown));
        }

        #endregion

        #region 分类

        /// <summary>
        /// 加载分类列表并选第一个
        /// </summary>
        private void LoadCategories()
        {
            _categories = GetService<ICategoryService>().GetCategoryList(_session.User!.Id);
            _categoryList.BeginUpdate();
            _categoryList.Items.Clear();
            foreach (Category category in _categories)
            {
                ListViewItem item = new ListViewItem(CategoryDisplayText(category))
                {
                    Tag = category,
                    ForeColor = Internal.Themes.ThemeManager.GetCategoryColor(_categoryList.Items.Count)
                };
                _categoryList.Items.Add(item);
            }
            _categoryList.EndUpdate();
            if (_categoryList.Items.Count > 0 && _categoryList.SelectedIndices.Count == 0)
            {
                _categoryList.SelectedIndices.Add(0);
            }
        }

        /// <summary>
        /// 分类显示文本(独立密码分类带锁标记)
        /// </summary>
        private static string CategoryDisplayText(Category category)
        {
            string suffix = category.EncryptMode == (int)CategoryEncryptMode.IndependentPassword ? "  🔒" : string.Empty;
            string prefix = category.IsDefault == 1 ? "★ " : "● ";
            return prefix + category.Name + suffix;
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
        private void OnCategorySelected(object? sender, EventArgs e)
        {
            Category? category = SelectedCategory();
            if (category == null || ReferenceEquals(category, _currentCategory))
            {
                return;
            }

            FlushEditorPendingSave();
            _trashView = false;
            SyncViewMode();
            _currentCategory = category;
            ReloadNotes();
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
                    MessageBox.Show(result.Message ?? "创建失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                LoadCategories();
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
                    MessageBox.Show(result.Message ?? "重命名失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                LoadCategories();
            }
        }

        /// <summary>
        /// 删除分类(其下笔记移入默认分类)
        /// </summary>
        private void DeleteCategory()
        {
            Category? category = SelectedCategory();
            if (category == null)
            {
                return;
            }
            if (category.IsDefault == 1)
            {
                MessageBox.Show("默认分类不可删除", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            R result = GetService<ICategoryService>().RemoveCategory(_session.User!.Id, category.Id, true);
            if (!result.IsSuccess)
            {
                MessageBox.Show(result.Message ?? "删除失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _currentCategory = null;
            LoadCategories();
            ReloadNotes();
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
                    MessageBox.Show("独立密码至少 6 位", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (password != confirm)
                {
                    MessageBox.Show("两次输入不一致", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        MessageBox.Show(result.Message ?? "设置失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (result.Data is byte[] categoryKey)
                    {
                        _session.MarkCategoryUnlocked(category.Id, categoryKey);
                    }
                    LoadCategories();
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
                    MessageBox.Show("两次输入的新密码不一致", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        MessageBox.Show(result.Message ?? "重置失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (result.Data is byte[] categoryKey)
                    {
                        _session.ReplaceCategoryKey(category.Id, categoryKey);
                    }
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
                        MessageBox.Show(result.Message ?? "切换失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    LoadCategories();
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
        private void ReloadNotes()
        {
            int userId = _session.User!.Id;
            if (_trashView)
            {
                _notes = GetService<INoteService>().GetTrashList(userId);
            }
            else if (_searchKeyword.Length > 0)
            {
                _notes = GetService<INoteService>().SearchByTitleList(userId, _searchKeyword);
            }
            else if (_currentCategory != null)
            {
                _notes = GetService<INoteService>().GetNoteList(userId, _currentCategory.Id);
            }
            else
            {
                _notes = new List<Note>();
            }

            _noteList.BeginUpdate();
            _noteList.Items.Clear();
            foreach (Note note in _notes)
            {
                _noteList.Items.Add(BuildNoteItem(note));
            }
            _noteList.EndUpdate();
        }

        /// <summary>
        /// 构造笔记列表项
        /// </summary>
        private static ListViewItem BuildNoteItem(Note note)
        {
            ListViewItem item = new ListViewItem((note.IsPinned == 1 ? "📌 " : string.Empty) + (note.Title.Length == 0 ? "无标题" : note.Title))
            {
                Tag = note
            };
            DateTime time = (note.UpdateTime ?? note.CreateTime).ToLocalTime();
            item.SubItems.Add(time.ToString("MM-dd HH:mm"));
            item.SubItems.Add(note.WordCount.ToString());
            if (note.Status == (int)NoteStatus.Trashed)
            {
                item.ForeColor = Internal.Themes.ThemeManager.GetColor("trashTint");
            }
            return item;
        }

        /// <summary>
        /// 搜索防抖到点
        /// </summary>
        private void SearchTimerTick(object? sender, EventArgs e)
        {
            _searchTimer?.Stop();
            _searchKeyword = _searchBox.Text.Trim();
            if (_trashView && _searchKeyword.Length > 0)
            {
                // 回收站不支持搜索,回到普通搜索视图
                _trashView = false;
                SyncViewMode();
            }
            ReloadNotes();
        }

        /// <summary>
        /// 切换回收站视图
        /// </summary>
        private void ToggleTrashView()
        {
            FlushEditorPendingSave();
            _trashView = !_trashView;
            if (_trashView)
            {
                _searchKeyword = string.Empty;
                _searchBox.Text = string.Empty;
            }
            SyncViewMode();
            ReloadNotes();
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
        private void OnNoteSelected(object? sender, EventArgs e)
        {
            Note? note = SelectedNote();
            if (note == null || ReferenceEquals(note, _currentNote))
            {
                return;
            }
            OpenNote(note);
        }

        /// <summary>
        /// 打开笔记(解锁/解密/装载)
        /// </summary>
        private void OpenNote(Note note)
        {
            FlushEditorPendingSave();
            Category? category = FindCategory(note.CategoryId);
            if (category == null)
            {
                ShowEditorMessage("笔记所属分类不存在");
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

            R<NoteDetailDto> detail = GetService<INoteService>().GetNoteDetail(note.Id, contentKey);
            if (!detail.IsSuccess || detail.Data == null)
            {
                _currentNote = null;
                ShowEditorMessage(detail.Message ?? "读取失败");
                return;
            }

            _currentNote = note;
            HideEditorMessage();
            _titleBox.Text = detail.Data.Title;
            _typeLabel.Text = TypeName(detail.Data.ContentType);
            _metaLabel.Text = "修改 " + (detail.Data.UpdateTime ?? detail.Data.CreateTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                + " · " + detail.Data.WordCount + " 字";
            NoteEditorBase editor = ActivateEditor(detail.Data.ContentType);
            editor.LoadNote(detail.Data);
            LoadAttachments(note.Id);
            SaveLastNoteId(note.Id);
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
                MessageBox.Show("独立密码错误", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        private void CreateNote(ContentType contentType)
        {
            if (_currentCategory == null)
            {
                MessageBox.Show("请先选择分类", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            _searchBox.Text = string.Empty;

            Note note = new Note
            {
                UserId = _session.User!.Id,
                CategoryId = category.Id,
                Title = string.Empty,
                ContentType = (int)contentType
            };
            R result = GetService<INoteService>().SaveNote(note, string.Empty, _session.GetContentKey(category));
            if (!result.IsSuccess)
            {
                MessageBox.Show(result.Message ?? "创建失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ReloadNotes();
            foreach (ListViewItem item in _noteList.Items)
            {
                if (((Note)item.Tag!).Id == note.Id)
                {
                    item.Selected = true;
                    break;
                }
            }
            _titleBox.Focus();
        }

        /// <summary>
        /// 置顶/取消置顶
        /// </summary>
        private void TogglePinned()
        {
            Note? note = SelectedNote() ?? _currentNote;
            if (note == null)
            {
                return;
            }
            GetService<INoteService>().UpdatePinned(note.Id, note.IsPinned != 1);
            ReloadNotes();
            foreach (ListViewItem item in _noteList.Items)
            {
                if (((Note)item.Tag!).Id == note.Id)
                {
                    item.Selected = true;
                    break;
                }
            }
        }

        /// <summary>
        /// 移入回收站
        /// </summary>
        private void TrashSelectedNote()
        {
            Note? note = SelectedNote();
            if (note == null)
            {
                return;
            }

            FlushEditorPendingSave();
            GetService<INoteService>().MoveToTrashList(new List<int> { note.Id }, _session.User!.Id);
            if (ReferenceEquals(note, _currentNote))
            {
                ClearEditor();
            }
            ReloadNotes();
        }

        /// <summary>
        /// 从回收站恢复
        /// </summary>
        private void RestoreSelectedNote()
        {
            Note? note = SelectedNote();
            if (note == null)
            {
                return;
            }

            FlushEditorPendingSave();
            GetService<INoteService>().RestoreList(new List<int> { note.Id }, _session.User!.Id);
            if (ReferenceEquals(note, _currentNote))
            {
                ClearEditor();
            }
            ReloadNotes();
        }

        /// <summary>
        /// 彻底删除(附件级联)
        /// </summary>
        private void PurgeSelectedNote()
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
            GetService<INoteService>().PurgeList(new List<int> { note.Id }, _session.User!.Id);
            if (ReferenceEquals(note, _currentNote))
            {
                ClearEditor();
            }
            ReloadNotes();
        }

        /// <summary>
        /// 清空回收站
        /// </summary>
        private void EmptyTrash()
        {
            DialogResult choice = MessageBox.Show("清空回收站？其中全部笔记及附件将被彻底删除,不可恢复。",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes)
            {
                return;
            }

            FlushEditorPendingSave();
            GetService<INoteService>().EmptyTrash(_session.User!.Id);
            ClearEditor();
            ReloadNotes();
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
        /// 登录后恢复上次打开的笔记
        /// </summary>
        private void TryRestoreLastNote()
        {
            if (_userSetting.LastNoteId <= 0)
            {
                return;
            }
            Note? note = GetService<INoteService>().GetNoteById(_userSetting.LastNoteId);
            if (note == null || note.Status != (int)NoteStatus.Normal || note.UserId != _session.User!.Id)
            {
                return;
            }

            int index = _categories.FindIndex(m => m.Id == note.CategoryId);
            if (index >= 0)
            {
                _categoryList.SelectedIndices.Add(index);
            }
            foreach (ListViewItem item in _noteList.Items)
            {
                if (((Note)item.Tag!).Id == note.Id)
                {
                    item.Selected = true;
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
        }

        /// <summary>
        /// 保存当前笔记(标题+正文加密写库)
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
            string content = _activeEditor.CollectContent();
            R result = GetService<INoteService>().SaveNote(note, content, contentKey);
            if (!result.IsSuccess)
            {
                MessageBox.Show(result.Message ?? "保存失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _activeEditor.MarkSaved();
            _metaLabel.Text = "修改 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · " + _activeEditor.GetWordCount() + " 字";
            RefreshNoteItem(note);
        }

        /// <summary>
        /// 刷新列表中的笔记行
        /// </summary>
        private void RefreshNoteItem(Note note)
        {
            foreach (ListViewItem item in _noteList.Items)
            {
                if (((Note)item.Tag!).Id == note.Id)
                {
                    int index = item.Index;
                    bool selected = item.Selected;
                    _noteList.Items.RemoveAt(index);
                    ListViewItem rebuilt = BuildNoteItem(note);
                    _noteList.Items.Insert(Math.Min(index, _noteList.Items.Count), rebuilt);
                    rebuilt.Selected = selected;
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
            _titleBox.Text = string.Empty;
            _metaLabel.Text = "选择或新建一条笔记";
            _typeLabel.Text = string.Empty;
            foreach (KeyValuePair<ContentType, NoteEditorBase> pair in _editors)
            {
                pair.Value.Visible = false;
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
        private void AddAttachment()
        {
            if (_currentNote == null)
            {
                MessageBox.Show("请先打开一条笔记", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show("分类已锁定,无法添加附件", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                foreach (string file in dialog.FileNames)
                {
                    try
                    {
                        byte[] plainBytes = File.ReadAllBytes(file);
                        if (plainBytes.Length > AttachmentPanel.SIZE_WARNING_BYTES
                            && MessageBox.Show("文件「" + Path.GetFileName(file) + "」超过 50MB,仍要加密保存吗？",
                                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        {
                            continue;
                        }
                        R result = GetService<INoteAttachmentService>().AddAttachment(
                            _session.User!.Id, _currentNote.Id, Path.GetFileName(file),
                            GuessMimeType(file), plainBytes, contentKey);
                        if (!result.IsSuccess)
                        {
                            MessageBox.Show(result.Message ?? "附件保存失败", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("读取文件失败：" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                LoadAttachments(_currentNote.Id);
            }
        }

        /// <summary>
        /// 打开附件：图片内存预览,其他类型导出
        /// </summary>
        private void OpenAttachment(NoteAttachment attachment)
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
                MessageBox.Show("分类已锁定", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[]? plainBytes = GetService<INoteAttachmentService>().DecryptAttachment(attachment.Id, contentKey);
            if (plainBytes == null)
            {
                MessageBox.Show("附件解密失败(数据损坏或密钥不符)", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllBytes(dialog.FileName, plainBytes);
                        MessageBox.Show("已导出：" + dialog.FileName, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("导出失败：" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        /// <summary>
        /// 删除附件
        /// </summary>
        private void RemoveAttachment(NoteAttachment attachment)
        {
            GetService<INoteAttachmentService>().RemoveAttachment(attachment.Id);
            if (_currentNote != null)
            {
                LoadAttachments(_currentNote.Id);
            }
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
        }

        /// <summary>
        /// 主题切换：重刷自身与编辑器
        /// </summary>
        private void OnThemeChanged(Internal.Themes.Theme theme)
        {
            if (IsHandleCreated)
            {
                Internal.Themes.ThemeManager.Apply(this);
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
    }
}
