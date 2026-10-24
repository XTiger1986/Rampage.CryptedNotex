using Microsoft.Data.Sqlite;
using Rampage;
using Rampage.CryptedNotex.BLL;
using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Enums;
using Rampage.CryptedNotex.Model.Po;
using Rampage.CryptedNotex.TestConsole;
using Rampage.DependencyInjection;
using Rampage.Model;
using System;
using System.Collections.Generic;
using System.Text;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    [STAThread]
    static void Main(string[] args)
    {
        NotexTestApplication app = RampageBooter.Run<NotexTestApplication>(args ?? new string[] { });
        using (RampageScope.UseGlobal())
        {
            RunAllTests(app);
        }
        Console.WriteLine();
        Console.WriteLine("===== 结果: " + _passed + " 通过, " + _failed + " 失败 =====");
        Environment.Exit(_failed == 0 ? 0 : 1);
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine("[PASS] " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("[FAIL] " + name);
        }
    }

    private static void RunAllTests(NotexTestApplication app)
    {
        IUserService userService = app.GetRequiredService<IUserService>();
        ICategoryService categoryService = app.GetRequiredService<ICategoryService>();
        INoteService noteService = app.GetRequiredService<INoteService>();
        INoteAttachmentService attachmentService = app.GetRequiredService<INoteAttachmentService>();

        // 1 首运行无用户
        Check(userService.CountUsers() == 0, "首运行用户数为 0");

        // 2 创建用户
        R<UserBriefDto> created = userService.CreateUser("Nicholas", "Nicholas Chow", "test-123456", out byte[]? userKey);
        Check(created.IsSuccess && created.Data != null && userKey != null, "创建用户成功并产出内容密钥");
        UserBriefDto user = created.Data!;

        // 3 登录验证
        Check(userService.VerifyUser("nicholas", "wrong-password", out byte[]? badKey) == null && badKey == null, "错误密码登录被拒");
        UserBriefDto? verified = userService.VerifyUser("nicholas", "test-123456", out byte[]? loginKey);
        Check(verified != null && loginKey != null, "正确密码登录成功并派生密钥");

        // 4 重复建用户被拒
        Check(!userService.CreateUser("nicholas", "x", "test-123456", out _).IsSuccess, "重复登录名被拒");

        // 5 默认分类存在
        List<Category> categories = categoryService.GetCategoryList(user.Id);
        Check(categories.Count == 1 && categories[0].IsDefault == 1 && categories[0].Name == UserService.DEFAULT_CATEGORY_NAME, "默认分类自动创建");

        // 6 新建分类
        R saveWork = categoryService.SaveCategory(new Category { UserId = user.Id, Name = "工作" });
        R saveDiary = categoryService.SaveCategory(new Category { UserId = user.Id, Name = "日记" });
        Check(saveWork.IsSuccess && saveDiary.IsSuccess, "新建两个分类");
        Check(!categoryService.SaveCategory(new Category { UserId = user.Id, Name = "工作" }).IsSuccess, "同名分类被拒");
        categories = categoryService.GetCategoryList(user.Id);
        Category defaultCategory = categories.Find(m => m.IsDefault == 1)!;
        Category workCategory = categories.Find(m => m.Name == "工作")!;

        // 7 三种类型笔记
        string chineseText = "这是一条中文笔记,包含敏感信息 🤫 bank-password=9527\n第二行";
        string markdownText = "# 标题\n\n- 列表项\n- **加粗**\n\n```csharp\nvar x = 1; // 代码块\n```";
        Note notePlain = NewNote(user, defaultCategory, "密码清单", ContentType.PlainText);
        R savePlain = noteService.SaveNote(notePlain, chineseText, userKey!);
        Note noteMarkdown = NewNote(user, defaultCategory, "关键-Markdown笔记", ContentType.Markdown);
        R saveMarkdown = noteService.SaveNote(noteMarkdown, markdownText, userKey!);
        Note noteRich = NewNote(user, workCategory, "会议记录", ContentType.RichText);
        R saveRich = noteService.SaveNote(noteRich, @"{\rtf1\ansi 富文本内容}", userKey!);
        Check(savePlain.IsSuccess && saveMarkdown.IsSuccess && saveRich.IsSuccess, "三种类型笔记保存成功");

        // 8 读取解密往返
        R<NoteDetailDto> detailPlain = noteService.GetNoteDetail(notePlain.Id, userKey!);
        Check(detailPlain.IsSuccess && detailPlain.Data != null && detailPlain.Data.PlainContent == chineseText, "纯文本笔记解密往返一致");
        R<NoteDetailDto> detailMarkdown = noteService.GetNoteDetail(noteMarkdown.Id, userKey!);
        Check(detailMarkdown.IsSuccess && detailMarkdown.Data!.PlainContent == markdownText, "Markdown 笔记解密往返一致");
        Check(detailMarkdown.Data!.WordCount == WordCountTool.CountNonWhitespace(markdownText), "字数统计正确");

        // 9 搜索
        List<Note> search = noteService.SearchByTitleList(user.Id, "关键");
        Check(search.Count == 1 && search[0].Id == noteMarkdown.Id, "标题 LIKE 搜索命中");
        List<Note> searchEscape = noteService.SearchByTitleList(user.Id, "%关键");
        Check(searchEscape.Count == 0, "LIKE 通配符已转义(不误匹配)");
        List<Note> searchCase = noteService.SearchByTitleList(user.Id, "markdown");
        Check(searchCase.Count == 1, "英文标题大小写不敏感搜索");

        // 10 置顶
        noteService.UpdatePinned(noteRich.Id, true);
        List<Note> workNotes = noteService.GetNoteList(user.Id, workCategory.Id);
        Check(workNotes.Count > 0 && workNotes[0].Id == noteRich.Id, "置顶笔记排前");

        // 11 附件加密
        byte[] attachmentBytes = Encoding.UTF8.GetBytes("附件明文内容 attachment-plain-123 📎");
        R addAttachment = attachmentService.AddAttachment(user.Id, notePlain.Id, "secret.txt", "text/plain", attachmentBytes, userKey!);
        Check(addAttachment.IsSuccess, "附件加密入库");
        List<NoteAttachment> attachments = attachmentService.GetAttachmentList(notePlain.Id);
        Check(attachments.Count == 1 && attachments[0].FileName == "secret.txt" && attachments[0].FileSize == attachmentBytes.Length, "附件列表(不含密文)正确");
        byte[]? decryptedAttachment = attachmentService.DecryptAttachment(attachments[0].Id, userKey!);
        Check(decryptedAttachment != null && Encoding.UTF8.GetString(decryptedAttachment) == Encoding.UTF8.GetString(attachmentBytes), "附件解密往返一致");

        // 12 数据库直查:标题明文/内容密文
        (byte[] plainNoteBlob, byte[] plainAttachBlob) = ReadBlobs(notePlain.Id);
        Check(plainNoteBlob.Length > 29 && plainNoteBlob[0] == CryptoTool.BLOB_VERSION_1, "note.content 为 0x01 版本头密文");
        Check(plainAttachBlob[0] == CryptoTool.BLOB_VERSION_1, "附件 content 为密文");
        string titleInDb = ReadTitle(notePlain.Id);
        Check(titleInDb == "密码清单", "标题在库中为明文");

        // 13 回收站
        noteService.MoveToTrashList(new List<int> { noteRich.Id }, user.Id);
        List<Note> trash = noteService.GetTrashList(user.Id);
        Check(trash.Count == 1 && trash[0].Id == noteRich.Id, "移入回收站可见");
        noteService.RestoreList(new List<int> { noteRich.Id }, user.Id);
        Check(noteService.GetTrashList(user.Id).Count == 0, "恢复后回收站为空");
        noteService.MoveToTrashList(new List<int> { noteRich.Id }, user.Id);
        noteService.PurgeList(new List<int> { noteRich.Id }, user.Id);
        Check(noteService.GetNoteById(noteRich.Id) == null, "彻底删除后笔记消失");

        // 14 改密全量重加密
        byte[] beforeChange = ReadBlobs(notePlain.Id).Item1;
        R changeResult = userService.ChangePassword(user.Id, "test-123456", "new-456789", new Progress<int>(v => { }));
        Check(changeResult.IsSuccess && changeResult.Data is byte[], "修改密码(全量重加密)成功");
        byte[] afterChange = ReadBlobs(notePlain.Id).Item1;
        Check(!beforeChange.AsSpan().SequenceEqual(afterChange), "改密后笔记密文已更新(重加密生效)");
        Check(userService.VerifyUser("nicholas", "test-123456", out _) == null, "旧密码不能登录");
        UserBriefDto? relogin = userService.VerifyUser("nicholas", "new-456789", out byte[]? newKey);
        Check(relogin != null && newKey != null, "新密码可登录并派生新钥");
        R<NoteDetailDto> detailAfter = noteService.GetNoteDetail(notePlain.Id, newKey!);
        Check(detailAfter.IsSuccess && detailAfter.Data!.PlainContent == chineseText, "新钥可解密改密前笔记");
        Check(userKey != null && !noteService.GetNoteDetail(notePlain.Id, userKey!).IsSuccess, "旧钥已无法解密(换钥彻底)");
        byte[]? attachmentAfter = attachmentService.DecryptAttachment(attachments[0].Id, newKey!);
        Check(attachmentAfter != null, "附件随改密重加密且新钥可解");

        // 15 独立密码分类三流程(工作分类笔记已在 13 步清空,先补建)
        Note workNote = NewNote(user, workCategory, "独立密码分类笔记", ContentType.PlainText);
        Check(noteService.SaveNote(workNote, "独立密码分类的正文内容 🔐", newKey!).IsSuccess, "为独立密码流程补建笔记");

        R setResult = categoryService.SetCategoryIndependentPassword(user.Id, workCategory.Id, "cat-999", newKey!, new Progress<int>(v => { }));
        Check(setResult.IsSuccess && setResult.Data is byte[] categoryKey, "设置独立密码(0→1 重加密)成功");
        Check(!noteService.GetNoteDetail(workNotes2(noteService, user, workCategory)[0].Id, newKey!).IsSuccess, "独立分类旧钥(用户钥)不可解");
        R<NoteDetailDto> catDetail = noteService.GetNoteDetail(workNotes2(noteService, user, workCategory)[0].Id, (byte[])setResult.Data!);
        Check(catDetail.IsSuccess, "分类钥可解独立分类笔记");
        Check(categoryService.TryUnlockCategory(workCategory.Id, "cat-999", out byte[]? unlockedKey) && unlockedKey != null, "独立密码解锁派生钥");
        Check(!categoryService.TryUnlockCategory(workCategory.Id, "bad", out byte[]? _), "错误独立密码被拒");

        R resetResult = categoryService.ResetCategoryPassword(user.Id, workCategory.Id, "cat-999", "cat-888", new Progress<int>(v => { }));
        Check(resetResult.IsSuccess && resetResult.Data is byte[], "重置独立密码(1→1 重加密)成功");
        Check(!categoryService.TryUnlockCategory(workCategory.Id, "cat-999", out _), "旧独立密码失效");
        Check(categoryService.TryUnlockCategory(workCategory.Id, "cat-888", out byte[]? resetKey) && resetKey != null
            && noteService.GetNoteDetail(workNotes2(noteService, user, workCategory)[0].Id, resetKey!).IsSuccess, "新独立密码可解");

        R followResult = categoryService.ChangeCategoryToFollowUser(user.Id, workCategory.Id, "cat-888", newKey!, new Progress<int>(v => { }));
        Check(followResult.IsSuccess, "切回跟随全局(1→0 重加密)成功");
        categories = categoryService.GetCategoryList(user.Id);
        Category backCategory = categories.Find(m => m.Name == "工作")!;
        Check(backCategory.EncryptMode == (int)CategoryEncryptMode.FollowUser, "分类加密模式已回到跟随全局");
        R<NoteDetailDto> followDetail = noteService.GetNoteDetail(workNotes2(noteService, user, backCategory)[0].Id, newKey!);
        Check(followDetail.IsSuccess, "切回后用户钥可解");

        // 16 清空回收站
        noteService.MoveToTrashList(new List<int> { notePlain.Id }, user.Id);
        noteService.EmptyTrash(user.Id);
        Check(noteService.GetTrashList(user.Id).Count == 0 && noteService.GetNoteById(notePlain.Id) == null, "清空回收站(级联附件)");

        // 17 删除分类带笔记迁移
        R removeCategory = categoryService.RemoveCategory(user.Id, workCategory.Id, true);
        Check(removeCategory.IsSuccess, "删除分类(笔记迁默认)成功");
        Check(categoryService.GetCategoryList(user.Id).Find(m => m.Name == "工作") == null, "分类已删除");

        Console.WriteLine();
        Console.WriteLine("原始密钥清零示例: 长度=" + (userKey != null ? userKey.Length : -1));
    }

    private static Note NewNote(UserBriefDto user, Category category, string title, ContentType type)
    {
        return new Note
        {
            UserId = user.Id,
            CategoryId = category.Id,
            Title = title,
            ContentType = (int)type
        };
    }

    private static List<Note> workNotes2(INoteService noteService, UserBriefDto user, Category category)
    {
        return noteService.GetNoteList(user.Id, category.Id);
    }

    private static (byte[], byte[]) ReadBlobs(int noteId)
    {
        using (SqliteConnection connection = new SqliteConnection("Data Source=e2e-test.db"))
        {
            connection.Open();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT content FROM note WHERE id=@Id";
                command.Parameters.AddWithValue("@Id", noteId);
                byte[] noteBlob = (byte[])command.ExecuteScalar()!;
                command.Parameters.Clear();
                command.CommandText = "SELECT content FROM note_attachment WHERE note_id=@Id LIMIT 1";
                command.Parameters.AddWithValue("@Id", noteId);
                object? attachResult = command.ExecuteScalar();
                return (noteBlob, attachResult == DBNull.Value || attachResult == null ? new byte[] { } : (byte[])attachResult);
            }
        }
    }

    private static string ReadTitle(int noteId)
    {
        using (SqliteConnection connection = new SqliteConnection("Data Source=e2e-test.db"))
        {
            connection.Open();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT title FROM note WHERE id=@Id";
                command.Parameters.AddWithValue("@Id", noteId);
                return (string)command.ExecuteScalar()!;
            }
        }
    }
}
