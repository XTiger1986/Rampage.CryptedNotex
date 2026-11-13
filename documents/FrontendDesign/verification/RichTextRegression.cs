using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.WinForm.Controls;
using Rampage.CryptedNotex.WinForm.Internal.Themes;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

public static class RichTextRegression
{
    private static int _checks;

    public static int Run()
    {
        _checks = 0;
        try
        {
            VerifyLegacyTheme();
            VerifyRichDocument("black", @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}{\colortbl;\red0\green0\blue0;}\viewkind4\uc1\pard\cf1\f0\fs24 Existing black document\par Second line\par}");
            VerifyRichDocument("white", @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}{\colortbl;\red255\green255\blue255;}\viewkind4\uc1\pard\cf1\f0\fs24 Existing white document\par}");
            VerifyRichDocument("mixed", @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}{\colortbl;\red0\green0\blue0;\red208\green40\blue40;\red30\green80\blue210;}\pard\cf1 Original \b bold\b0\cf2 red\cf3 blue\cf1\par Unicode \u20013?\u25991?\par}");
            VerifyRichDocument("legacy-plain-fallback", "旧版异常正文 fallback\n保留原始文本");
            using (PlainNoteEditor plain = new PlainNoteEditor())
            {
                VerifyTextDocument(plain, ContentType.PlainText, "历史纯文本\nLF\r\nCRLF\t缩进  \n尾部\n");
            }
            using (MarkdownNoteEditor markdown = new MarkdownNoteEditor())
            {
                VerifyTextDocument(markdown, ContentType.Markdown, "# 历史 Markdown\n\n- **加粗**\n\n```cs\nvar n = 1;\n```\n");
            }
            VerifyAttachmentModes();
            VerifyRichToolbarStates();
            VerifyEditorDpiScaling();
            Console.WriteLine($"PASS: {_checks} editor/theme compatibility checks; no database opened.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void VerifyLegacyTheme()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "themes");
        Directory.CreateDirectory(directory);
        string legacyId = "verification-legacy-" + Guid.NewGuid().ToString("N");
        string path = Path.Combine(directory, legacyId + ".json");
        try
        {
            // 旧 JSON 不带 description/chrome/新增视觉属性；通过真实文件加载路径检验。
            File.WriteAllText(path, "{\"id\":\"" + legacyId + "\",\"name\":\"旧自定义主题\",\"colors\":{\"accent\":\"#126789\"}}", Encoding.UTF8);
            ThemeManager.Initialize();
            string[] known = { "light", "dark", "forest", "parchment", "violet", "contrast", "invader", "alienmorph", "darkstar", "alx", "xenomorph" };
            foreach (string id in known)
            {
                Check(ThemeManager.AvailableThemes.Any(theme => theme.Id == id), "preserved theme id: " + id);
            }
            ThemeManager.ApplyTheme(legacyId);
            Check(ThemeManager.Current.Id == legacyId, "legacy JSON is available");
            Check(ThemeManager.GetColor("accent").ToArgb() == Color.FromArgb(0x12, 0x67, 0x89).ToArgb(), "legacy color is honored");
            ThemeManager.ApplyTheme("unknown-verification-theme");
            Check(ThemeManager.Current.Id == "light", "unknown id falls back to light");
        }
        finally
        {
            File.Delete(path);
            ThemeManager.Initialize();
        }
    }

    private static void VerifyRichDocument(string name, string original)
    {
        using Form host = new Form { Size = new Size(1100, 720) };
        using RichNoteEditor editor = new RichNoteEditor { Dock = DockStyle.Fill };
        host.Controls.Add(editor);
        host.CreateControl();
        editor.CreateControl();
        int saves = 0;
        int changes = 0;
        editor.SaveRequested += (sender, args) => { saves++; editor.MarkSaved(); };
        editor.ContentChanged += (sender, args) => changes++;
        ThemeManager.ApplyTheme("light");
        ThemeManager.Apply(host);
        editor.LoadNote(new NoteDetailDto { Id = 10000, ContentType = ContentType.RichText, PlainContent = original });
        RichTextBox native = Descendants(editor).OfType<RichTextBox>().Single();
        string nativeOriginal = native.Rtf ?? string.Empty;
        Color paper = native.BackColor;
        if (name == "white")
        {
            Check(paper.GetBrightness() < 0.5F, "white legacy ink uses a readable dark document canvas");
        }
        else if (name == "black")
        {
            Check(paper.GetBrightness() > 0.5F, "black legacy ink uses a readable light document canvas");
        }
        Check(!editor.IsDirty && changes == 0, name + ": loading is not an edit");
        Check(editor.CollectContent() == original, name + ": untouched source roundtrips exactly");
        foreach (Theme theme in ThemeManager.AvailableThemes)
        {
            native.Select(Math.Min(2, native.TextLength), Math.Min(3, Math.Max(0, native.TextLength - 2)));
            int start = native.SelectionStart;
            int length = native.SelectionLength;
            ThemeManager.ApplyTheme(theme.Id);
            ThemeManager.Apply(host);
            Application.DoEvents();
            Check(native.Rtf == nativeOriginal, name + ": native RTF unchanged in " + theme.Id);
            Check(editor.CollectContent() == original, name + ": stored source unchanged in " + theme.Id);
            Check(!editor.IsDirty && changes == 0, name + ": theme is not an edit in " + theme.Id);
            Check(native.SelectionStart == start && native.SelectionLength == length, name + ": selection preserved in " + theme.Id);
            Check(native.BackColor == paper, name + ": document paper preserved in " + theme.Id);
        }
        editor.FlushPendingSave();
        Check(saves == 0, name + ": viewing/theme changes never request a save");
        editor.NotifyTitleChanged();
        editor.FlushPendingSave();
        Check(saves == 1 && editor.CollectContent() == original, name + ": title-only edit preserves source");
        editor.SetReadOnly(true);
        ThemeManager.ApplyTheme("dark");
        ThemeManager.Apply(host);
        Application.DoEvents();
        Check(!editor.IsDirty && editor.CollectContent() == original, name + ": read-only theme preserves source");
        editor.SetReadOnly(false);
        native.Select(native.TextLength, 0);
        native.SelectedText = " appended";
        Check(editor.IsDirty && editor.CollectContent() != original, name + ": actual editing produces updated RTF");
        Check(editor.CollectContent().StartsWith(@"{\rtf"), name + ": edited document remains RTF");
        string edited = editor.CollectContent();
        ThemeManager.ApplyTheme("light");
        ThemeManager.Apply(host);
        Application.DoEvents();
        Check(editor.CollectContent() == edited, name + ": changing theme preserves pending edits");
        editor.MarkSaved();
        editor.NotifyTitleChanged();
        Check(editor.CollectContent() == edited, name + ": a later title edit retains the saved body instead of the old source");
        editor.MarkSaved();
    }

    private static void VerifyTextDocument(NoteEditorBase editor, ContentType type, string original)
    {
        int changes = 0;
        editor.ContentChanged += (sender, args) => changes++;
        editor.LoadNote(new NoteDetailDto { Id = 10001, ContentType = type, PlainContent = original });
        Check(changes == 0 && !editor.IsDirty, type + ": load is not an edit");
        TextBox native = Descendants(editor).OfType<TextBox>().Single();
        Check(native.Text == original.ReplaceLineEndings("\r\n"), type + ": LF-only and mixed line endings display as Windows multiline text");
        Check(native.Lines.Length == original.ReplaceLineEndings("\n").Split('\n').Length, type + ": historical line breaks remain visible");
        editor.SetEditorFont("Consolas", 14);
        Check(changes == 0 && !editor.IsDirty && editor.CollectContent() == original, type + ": editor font preference never changes the document");
        foreach (Theme theme in ThemeManager.AvailableThemes)
        {
            ThemeManager.ApplyTheme(theme.Id);
            ThemeManager.Apply(editor);
            Check(!editor.IsDirty, type + ": theme is not an edit in " + theme.Id);
            Check(editor.CollectContent() == original, type + ": source and line endings preserved in " + theme.Id);
        }
        editor.NotifyTitleChanged();
        Check(editor.CollectContent() == original, type + ": title-only edit preserves source");
        editor.MarkSaved();
        native.AppendText(" edited");
        Check(editor.IsDirty && editor.CollectContent().EndsWith(" edited"), type + ": actual edit is collected");
        string edited = editor.CollectContent();
        editor.MarkSaved();
        editor.NotifyTitleChanged();
        Check(editor.CollectContent() == edited, type + ": later title edit retains the saved body");
        editor.MarkSaved();
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void VerifyAttachmentModes()
    {
        using AttachmentPanel panel = new AttachmentPanel();
        panel.LoadAttachments(new List<NoteAttachment>
        {
            new NoteAttachment { Id = 10000, FileName = "旧附件名称.txt", FileSize = 2048 }
        });
        panel.SetReadOnly(true);
        Check(Descendants(panel).OfType<Button>().All(button => !button.Visible), "read-only attachment view hides add and remove actions");
        panel.SetReadOnly(false);
        Check(Descendants(panel).OfType<Button>().Count(button => button.Visible) == 2, "editing restores add and remove attachment actions");
        panel.SetReadOnly(true);
        Check(Descendants(panel).OfType<Button>().All(button => !button.Visible), "switching back hides previously visible attachment actions");
    }

    private static void VerifyRichToolbarStates()
    {
        using RichNoteEditor editor = new RichNoteEditor();
        const string original = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\pard\f0\fs24\b\i\ul Bold\ul0\i0\b0  normal\par}";
        editor.LoadNote(new NoteDetailDto { Id = 10002, ContentType = ContentType.RichText, PlainContent = original });
        RichTextBox native = Descendants(editor).OfType<RichTextBox>().Single();
        Button[] toggles = Descendants(editor).OfType<Button>().Where(button => button.Text == "B" || button.Text == "I" || button.Text == "U").ToArray();
        foreach (Theme theme in ThemeManager.AvailableThemes)
        {
            ThemeManager.ApplyTheme(theme.Id);
            native.Select(0, 4);
            ThemeManager.Apply(editor);
            Check(toggles.All(button => button.BackColor == ThemeManager.GetColor("selectionBack")), "rich toolbar keeps active formatting highlight in " + theme.Id);
            native.Select(6, 3);
            ThemeManager.Apply(editor);
            Check(toggles.All(button => button.BackColor == button.Parent!.BackColor), "rich toolbar clears inactive formatting highlight in " + theme.Id);
            Check(!editor.IsDirty && editor.CollectContent() == original, "toolbar selection never edits the document in " + theme.Id);
        }
    }

    private static void VerifyEditorDpiScaling()
    {
        using RichNoteEditor editor = new RichNoteEditor { Size = new Size(900, 600) };
        editor.CreateControl();
        MethodInfo load = typeof(NoteEditorBase).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!;
        load.Invoke(editor, new object[] { EventArgs.Empty });
        Button bold = Descendants(editor).OfType<Button>().Single(button => button.Text == "B");
        int expectedHeight = (int)Math.Round(28F * editor.DeviceDpi / 96F);
        Check(Math.Abs(bold.Height - expectedHeight) <= 1, "runtime rich toolbar uses the current DPI");
        int height = bold.Height;
        load.Invoke(editor, new object[] { EventArgs.Empty });
        Check(bold.Height == height, "repeated load never scales the toolbar twice");
    }

    private static void Check(bool passed, string description)
    {
        _checks++;
        if (!passed)
        {
            throw new InvalidOperationException("FAIL: " + description);
        }
    }
}
