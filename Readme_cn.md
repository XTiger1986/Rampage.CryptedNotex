# Rampage.CryptedNotex（秘密档案）

![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4) ![C#](https://img.shields.io/badge/C%23-12-239120) ![平台](https://img.shields.io/badge/%E5%B9%B3%E5%8F%B0-Windows-0078D6) ![存储](https://img.shields.io/badge/%E5%AD%98%E5%82%A8-SQLite-003B57) ![License](https://img.shields.io/badge/License-MIT-yellow)

**[English](Readme.md) | 简体中文**

Rampage.CryptedNotex（显示名「秘密档案」）是一款**本地完全离线**的 Windows 加密笔记本。笔记正文与附件在写入 SQLite 数据库**之前**，就先用由你的密码派生的密钥完成了加密——数据库文件就算被整份拷走，得到的也只是密文。无网络、无遥测、无云端账户。

项目基于开源 [Rampage 框架](https://github.com/XTiger1986/Rampage)构建（WinForms + .NET 8 + 自研 ORM），覆盖完整的笔记使用闭环：多用户本地账户、两种加密模式的分类、三种笔记类型（纯文本 / 富文本 / 带实时预览的 Markdown）、加密附件、回收站、自动锁定与托盘驻留，以及 11 套内置界面主题（可用纯 JSON 文件自行扩展）。

> **没有后门。** 内容密钥只存在于密码的派生结果中，忘记密码即数据不可恢复——这是设计使然。

---

## 核心特性

- **零明文存储** — 笔记正文与附件以 AES-256-GCM 密文落库；标题、分类名、时间戳保持明文以支撑列表与搜索
- **密码派生密钥** — PBKDF2-SHA256（600,000 次迭代、每用户随机盐）派生 256 位内容密钥；登录哈希使用独立盐，二者互不可推
- **改密即全量重加密** — 受影响的全部笔记（含回收站）与附件在单个事务内解密后用新密钥重加密；中途失败自动回滚，旧密码继续可用
- **分类独立密码** — 分类可选择使用自己的密码（与账户密码无关），设置 / 重置 / 切回跟随全局三种转换均伴随该分类的全量重加密
- **三种笔记类型** — 纯文本、富文本（RTF + 主题化格式工具条）、Markdown 左右分栏实时预览（`marked` + `highlight.js` 本地离线资源，WebView2 运行时缺失时优雅降级为仅编辑）
- **附件加密** — 文件加密入库；图片仅内存预览（不落临时文件），其他类型按需导出
- **回收站 / 置顶 / 标题搜索** — 软删除可恢复、彻底删除（附件级联）、清空回收站；置顶优先；大小写不敏感的 LIKE 搜索（通配符已转义）
- **自动锁定与托盘** — 可配置的空闲锁定（系统锁定 / 会话切换同样触发）清空全部内存密钥回到登录界面；可选最小化到托盘驻留
- **多用户** — 多个本地账户、笔记互相隔离；登录防爆破（5 次失败锁 5 分钟，可配置）
- **11 套内置主题** — 浅色 / 深色 / 墨绿 / 羊皮纸 / 紫罗兰 / 高对比，以及带金属纹理与壁纸的主题化皮肤（darkstar/xenomorph 等）；在 exe 旁 `themes\` 放 JSON 即可覆盖或新增

## 安全设计

完整威胁模型与加密规格见 [`documents/ProductDocuments/CryptedNotex.ProductDocument.md`](documents/ProductDocuments/CryptedNotex.ProductDocument.md)，要点：

| 项目 | 设计 |
| --- | --- |
| 密钥派生 | PBKDF2-HMAC-SHA256，600,000 次迭代，16 字节随机盐，32 字节输出；参数随用户入库，未来提高迭代数会在下一次改密时自动升级 |
| 内容加密 | AES-256-GCM，每次保存使用全新随机 96 位 nonce |
| 密文格式 | `版本(1) ‖ nonce(12) ‖ 密文(N) ‖ tag(16)` — 版本字节为未来的算法迁移预留 |
| 域分隔 | 笔记正文与附件使用不同 AAD 常量，密文无法跨表粘贴互换 |
| 登录 | 独立盐的 PBKDF2 哈希（定长时间比较）；旧哈希在成功登录时透明升级 |
| 内存密钥 | 登录时派生、仅由界面会话持有，锁定/登出/退出时清零 |
| 明文边界 | 标题、分类名、附件文件名、时间、字数为明文（列表与搜索由此可用），产品文档中明确声明 |

## 解决方案组成

| 项目 | 说明 |
| --- | --- |
| **Rampage.CryptedNotex.Model** | 实体（`Po\`）、枚举、DTO |
| **Rampage.CryptedNotex.Common** | 加密原语（`CryptoTool`：PBKDF2 派生、AES-GCM 加解密、blob 格式） |
| **Rampage.CryptedNotex.DAL** | 基于 `SuperDao` 的数据访问；SQL 只出现在此层；`SchemaInitializer` 负责首运行建库 |
| **Rampage.CryptedNotex.BLL** | 业务服务（用户/分类/笔记/附件/设置）；事务边界 |
| **Rampage.CryptedNotex.WinForm** | 窗体、三种笔记编辑器、主题引擎、托盘、加密会话、自动锁定 |

解决方案以**同级目录项目引用**的方式引用 [Rampage 框架](https://github.com/XTiger1986/Rampage)（`..\..\Rampage\...`），编译前请将两个仓库并排克隆。

## 快速开始

### 环境要求

- .NET 8.0 SDK 及以上（编译）；目标机需 .NET 8 Desktop Runtime (x64)（运行发布产物）
- Windows 10 1809+ / Windows 11
- WebView2 运行时——可选；缺失时仅 Markdown 预览不可用

### 源码编译运行

```bash
git clone https://github.com/XTiger1986/Rampage.git
git clone https://github.com/XTiger1986/Rampage.CryptedNotex.git
cd Rampage.CryptedNotex/Rampage.CryptedNotex.Net
dotnet run -c Debug --project Rampage.CryptedNotex.WinForm
```

首运行时建库器会在 exe 旁创建 `data\cryptednotex.db`（WAL 模式），首运行向导引导创建账户。

### 发布

单文件、依赖框架发布（一个约 20MB 的 exe + `config\` + `Assets\Markdown\`；`data\`、`__Logs\` 运行时生成）：

```powershell
cd installer
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

- 产物目录：`Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\bin\Release\net8.0-windows\win-x64\publish\`
- 便携使用请把 exe 放在可写目录（数据库与日志生成在 exe 旁）；正式分发走 `installer\秘密档案.iss` 的 per-user 安装包则无此限制
- 不得开启 `PublishTrimmed` / `PublishAot` —— 底层框架依赖运行时反射与 Reflection.Emit

### 配置

exe 旁的 `config\app.json`：

```json
{
  "DatabaseFactory": {
    "DataAccessTypeName": "Rampage.Dapper.DapperDataAccessor, Rampage.Dapper",
    "Clients": [
      { "Name": "CryptedNotex", "Type": "Sqlite", "ConnectionString": "Data Source=data/cryptednotex.db" }
    ]
  },
  "LoginProtection": { "Enabled": true, "MaxFailuresPerAccount": 5, "WindowSeconds": 300 }
}
```

## 数据、备份与迁移

- 用户数据全部在 `data\cryptednotex.db` —— 拷贝该文件（或整个目录）即完整备份，覆盖回原位即恢复
- **密码不存储于任何地方**：备份的数据库只能用原密码打开
- 数据库结构脚本按时间戳版本化存放在 [`documents/DatabaseTablesDesign/`](documents/DatabaseTablesDesign)，内嵌进程序集，启动时经 `schema_history` 台账保证每个脚本只执行一次（演进结构 = 追加新脚本；台账与实际结构意外脱节时初始化器可自愈）

## 主题

内置 11 套主题（浅色、深色、墨绿、羊皮纸、紫罗兰、高对比，以及 darkstar/xenomorph 等带金属纹理与壁纸的皮肤）。自定义：在 exe 旁 `themes\` 目录放 JSON 文件——相同 `id` 覆盖内置主题，新 `id` 即新增。完整令牌（颜色、字体、分类色板）见产品文档 §8。

## License

本项目基于 [MIT License](LICENSE) 开源。

## 反馈

- 个人网站：[http://www.1986z.com/](http://www.1986z.com/)
- GitHub：[@XTiger1986](https://github.com/XTiger1986)
- 欢迎通过 [issue](https://github.com/XTiger1986/Rampage.CryptedNotex/issues) 提交问题与建议
