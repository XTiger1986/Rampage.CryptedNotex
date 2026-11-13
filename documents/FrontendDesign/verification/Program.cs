using Microsoft.Extensions.DependencyInjection;
using Rampage;
using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.WinForm;
using Rampage.CryptedNotex.WinForm.Forms;
using Rampage.CryptedNotex.WinForm.Internal;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using Rampage.DependencyInjection;
using System.Drawing.Imaging;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{
    private static readonly List<string> Results = new List<string>();

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(args.Contains("--dpi96") ? HighDpiMode.DpiUnaware : HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Contains("--regression"))
        {
            return RichTextRegression.Run();
        }
        // The preview owns its output directory and never opens an installed application's database.
        if (!AppContext.BaseDirectory.Contains(Path.Combine("verification", "bin"), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Run the preview from its verification/bin build output.");
        }
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        string runDirectory = Path.Combine(AppContext.BaseDirectory, "runs", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(runDirectory);
        Directory.CreateDirectory("config");
        File.WriteAllText(Path.Combine("config", "app.json"), JsonSerializer.Serialize(new
        {
            DatabaseFactory = new
            {
                DataAccessTypeName = "Rampage.Dapper.DapperDataAccessor, Rampage.Dapper",
                Clients = new[] { new { Name = "CryptedNotex", Type = "Sqlite", ConnectionString = "Data Source=" + Path.Combine(runDirectory, "preview.db") } }
            }
        }));
        try
        {
            NotexApplication app = RampageBooter.Run<NotexApplication>(Array.Empty<string>());
            using IDisposable scope = RampageScope.UseGlobal();
            ThemeManager.Initialize();
            ThemeManager.ApplyTheme("xenomorph");
            IUserService users = RampageScope.CurrentProvider!.GetRequiredService<IUserService>();
            INoteService notes = RampageScope.CurrentProvider!.GetRequiredService<INoteService>();
            ICategoryService categories = RampageScope.CurrentProvider!.GetRequiredService<ICategoryService>();
            IUserSettingService settings = RampageScope.CurrentProvider!.GetRequiredService<IUserSettingService>();
            var created = users.CreateUser("preview", "设计预览", "preview-only-123", out byte[]? key);
            if (!created.IsSuccess || created.Data == null || key == null)
            {
                throw new InvalidOperationException(created.Message);
            }
            CryptoSession session = new CryptoSession();
            session.Init(created.Data, key);
            foreach (string name in new[] { "灵感手记", "工作档案", "阅读摘录" })
            {
                categories.SaveCategory(new Category { UserId = created.Data.Id, Name = name });
            }
            int categoryId = categories.GetCategoryList(created.Data.Id)[0].Id;
            string body = "为想法，留一处安静的空间。\r\n\r\n这里是你的本地加密档案。记录日常、整理灵感，或收好只属于自己的文字。\r\n\r\n01  从一条笔记开始\r\n\r\n用分类组织内容，使用标题快速检索。正文与附件加密保存在本机。\r\n\r\n02  选择你的记录方式\r\n\r\n纯文本适合专注书写；富文本保留排版；Markdown 让结构更清晰。\r\n\r\n标题用于检索，会以明文保存。敏感信息请写在正文中。";
            Note first = new Note { UserId = created.Data.Id, CategoryId = categoryId, Title = "留住每一个值得记录的想法", ContentType = 0, IsPinned = 1 };
            notes.SaveNote(first, body, key);
            foreach (var sample in new[] { ("本周的工作与计划", 0), ("阅读清单与随手摘录", 2), ("一封写给未来的信", 1), ("窗边，和一个新的开始", 0) })
            {
                string text = sample.Item2 == 1 ? "{\\rtf1\\ansi A letter to the future.}" : sample.Item2 == 2 ? "# 阅读清单\n\n让阅读成为思考的开始。\n\n## 本月计划\n\n- [ ] 一本关于设计的书\n- [ ] 一本关于生活的书\n\n> 记录遇见的句子，也记录自己的思考。\n\n**留一点时间，慢慢读。**" : body;
                notes.SaveNote(new Note { UserId = created.Data.Id, CategoryId = categoryId, Title = sample.Item1, ContentType = sample.Item2 }, text, key);
            }
            UserSetting preference = settings.GetUserSetting(created.Data.Id);
            preference.LastNoteId = first.Id;
            preference.ThemeId = "xenomorph";
            preference.AutoLockMinutes = 0;
            preference.MinimizeToTray = 0;
            settings.SaveUserSetting(preference);
            using Notex main = new Notex(app, session);
            main.Shown += async (sender, e) =>
            {
                try
                {
                    await Task.Delay(150);
                    float scale = main.DeviceDpi / 96F;
                    main.ClientSize = new Size((int)(1280 * scale), (int)(780 * scale));
                    typeof(Notex).GetMethod("ApplySplitDistances", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, null);
                    await Task.Delay(650);
                    byte[] originalCipher = notes.GetNoteById(first.Id)!.Content!.ToArray();
                    foreach (string field in new[] { "_editToggleButton", "_titleBox", "_metaLabel", "_searchBox" })
                    {
                        Control control = (Control)typeof(Notex).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
                        Results.Add(field + ": " + control.Bounds + " parent: " + control.Parent!.ClientSize + " visible: " + control.Visible);
                        Check(control.Visible && control.Parent.ClientRectangle.Contains(control.Bounds), field + " fits its parent");
                        Check(main.RectangleToScreen(main.ClientRectangle).Contains(control.RectangleToScreen(control.ClientRectangle)), field + " fits the window");
                    }
                    foreach (Theme theme in ThemeManager.AvailableThemes)
                    {
                        ThemeManager.ApplyTheme(theme.Id);
                        await Task.Delay(60);
                        Capture(main, Path.Combine(runDirectory, "workspace-" + theme.Id + ".png"));
                    }
                    Check(originalCipher.SequenceEqual(notes.GetNoteById(first.Id)!.Content!), "Theme browsing preserves stored note ciphertext");
                    ThemeManager.ApplyTheme("xenomorph");
                    ListView noteList = (ListView)typeof(Notex).GetField("_noteList", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
                    foreach (int contentType in new[] { 1, 2 })
                    {
                        foreach (ListViewItem item in noteList.Items)
                        {
                            item.Selected = item.Tag is Note note && note.ContentType == contentType;
                        }
                        await Task.Delay(450);
                        typeof(Notex).GetMethod("EnterEdit", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, null);
                        await Task.Delay(150);
                        Capture(main, Path.Combine(runDirectory, contentType == 1 ? "editor-rich.png" : "editor-markdown.png"));
                    }
                    using (SettingsForm form = new SettingsForm(app, session, preference))
                    {
                        form.Show(main);
                        await Task.Delay(150);
                        Capture(form, Path.Combine(runDirectory, "settings.png"));
                        form.ClientSize = new Size((int)(820 * scale), (int)(540 * scale));
                        await Task.Delay(60);
                        Capture(form, Path.Combine(runDirectory, "settings-compact.png"));
                        form.Close();
                    }
                    using (LoginForm form = new LoginForm(app, new CryptoSession()))
                    {
                        form.Show(main);
                        await Task.Delay(100);
                        Capture(form, Path.Combine(runDirectory, "login.png"));
                        form.ClientSize = new Size((int)(560 * scale), (int)(480 * scale));
                        await Task.Delay(60);
                        Capture(form, Path.Combine(runDirectory, "login-compact.png"));
                        form.Close();
                    }
                    using (CreateUserForm form = new CreateUserForm(app, new CryptoSession()))
                    {
                        form.Show(main);
                        await Task.Delay(100);
                        Capture(form, Path.Combine(runDirectory, "create-account.png"));
                        form.Close();
                    }
                    File.WriteAllLines(Path.Combine(runDirectory, "report.txt"), Results);
                    Console.WriteLine(runDirectory);
                    if (args.Contains("--capture"))
                    {
                        main.Close();
                    }
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(runDirectory, "error.txt"), error.ToString());
                    main.Close();
                }
            };
            Application.Run(main);
            session.Lock();
            return File.Exists(Path.Combine(runDirectory, "error.txt")) ? 1 : 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(runDirectory, "error.txt"), error.ToString());
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Check(bool condition, string name)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + name);
        if (!condition)
        {
            throw new InvalidOperationException(name);
        }
    }

    private static void Capture(Form form, string path)
    {
        form.PerformLayout();
        form.Refresh();
        using Bitmap bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, ImageFormat.Png);
        Results.Add("CAPTURE " + Path.GetFileName(path) + " @ " + form.DeviceDpi + "dpi");
    }
}
