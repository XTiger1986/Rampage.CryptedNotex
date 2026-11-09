# 秘密档案（CryptedNotex）· 产品文档

- 版本：v1.0（2026-10-24）
- 状态：设计定稿，随实现同步修订
- 平台：Windows 10 1809+ / .NET 8 / WinForms / SQLite
- 仓库：`D:\Studio\XTiger1986\Rampage.CryptedNotex`（解决方案 `Rampage.CryptedNotex.Net\Rampage.CryptedNotex.sln`）

---

## 1. 产品概述

### 1.1 定位与目标用户

秘密档案（工程名 CryptedNotex）是一款**本地离线**的加密笔记本：笔记内容以用户密码派生的密钥加密后才写入 SQLite 数据库，数据库文件即使被整份拷走，正文与附件也无法被读取。面向对数据自主与隐私有较高要求的个人用户。界面中文名是「秘密档案」；程序集、密文附加认证数据和数据库文件名仍用 CryptedNotex，已有密文不因改名失效。

### 1.2 核心价值主张

- 密码不落库、正文/附件全部密文存储；
- 多用户（本机多账号，各自独立密钥体系）；
- 分类两种加密模式：跟随全局密码 / 独立密码；
- 改密码即全量重加密（所见即所改，安全心智简单）；
- 三种笔记类型：纯文本、富文本（RTF）、Markdown（WebView2 预览）；
- 多套界面主题，JSON 皮肤可扩展；
- 完全离线，无任何网络依赖（WebView2 预览资源本地打包）。

### 1.3 术语表

| 术语 | 含义 |
|---|---|
| 登录哈希 | 存库的 PBKDF2 口令验证串（格式 `PBKDF2-SHA256$迭代$盐$哈希`），仅用于验证登录，不能推出内容密钥 |
| 内容密钥 | 由密码经 PBKDF2 派生的 256 位 AES-GCM 密钥，仅在内存会话中存在 |
| 密文 blob | 单列存储的二进制密文结构：`版本1B + nonce12B + 密文N + tag16B` |
| AAD | AES-GCM 附加认证数据，用于域分隔（笔记与附件各一个常量），防跨表粘贴密文 |
| 独立密码分类 | 加密模式为 1 的分类：内容密钥由**分类自己的密码**派生，与用户登录密码无关 |
| 全局分类 | 加密模式为 0 的分类：内容密钥由用户登录密码派生 |
| 回收站 | 笔记软删除状态（status=1），可恢复、可彻底删除 |

### 1.4 运行环境

- Windows 10 1809 及以上 / Windows 11（深色标题栏效果需 Win11 或较新 Win10）；
- .NET 8 Desktop Runtime；
- WebView2 Runtime（可选）：缺失时 Markdown 仅编辑无预览，功能不阻塞；
- 无网络要求。

---

## 2. 总体架构

### 2.1 五项目分层

```
Rampage.CryptedNotex.WinForm (net8.0-windows)   窗体、主题、会话（CryptoSession/AutoLock）、编辑器控件
        │ 引用
Rampage.CryptedNotex.BLL                        业务服务（用户/分类/笔记/附件/设置；事务边界）
        │ 引用
Rampage.CryptedNotex.DAL                        数据访问（SuperDao + 原生 SQL；SchemaInitializer 建表）
        │ 引用
Rampage.CryptedNotex.Common                     加密原语 CryptoTool、字数工具（无 DB 依赖）
        │ 引用
Rampage.CryptedNotex.Model                      Po 实体 / Enums / Dto
```

依赖链：WinForm → BLL → DAL → Common → Model → Rampage 框架（禁改 `D:\Studio\XTiger1986\Rampage`）。

### 2.2 Rampage 框架复用点

| 框架设施 | 用途 |
|---|---|
| `RampageBooter` / `RampageBasicApplication` | 启动管线（环境→日志→配置→容器→数据库客户端→服务→运行） |
| `SuperDao<T>` / `ISuperDao<T>` / `LambdaWrapper` | ORM CRUD 与条件查询 |
| `BaseService<M,D>` / `[Auto]` 属性注入 / `R`/`R<T>` | 业务层标准形态 |
| `SecurityTool.PBKDF2Hash/PBKDF2Verify` | 登录哈希（框架标准格式） |
| `RampageLogger`（log4net） | 日志（`config\log4net.config`，输出 `__Logs\`） |
| `JsonTool` / `MathTool` / `MemoryLoginAttemptTracker` | JSON、随机数、登录防爆破 |
| `DatabaseFactory`（Type=Sqlite） | SQLite 客户端（桌面默认 `KeepScopeConnection=true`） |

### 2.3 关键目录约定

```
Rampage.CryptedNotex.Net\
  Rampage.CryptedNotex.Model\Enums\   枚举（与 SQL 注释的 smallint 代码一致）
  Rampage.CryptedNotex.Model\Po\      实体
  Rampage.CryptedNotex.Model\Dto\     传输对象
  Rampage.CryptedNotex.Common\        CryptoTool 等纯算法
  Rampage.CryptedNotex.DAL\           IXxxDao + Implementations\XxxDao（SQL 只在此层）
  Rampage.CryptedNotex.BLL\           IXxxService + Implementations\XxxService
  Rampage.CryptedNotex.WinForm\
    Internal\                         NotexFormBase / CryptoSession / AutoLockManager / ThemeManager / NotexGuiContext / ServiceRefs
    Internal\Themes\                  Theme 模型 + 11 套内嵌主题 JSON
    Assets\Chrome\                    金属纹理、壁纸、档案徽记（内嵌）
    Forms\                            登录/主窗体/各对话框
    Controls\                         编辑器与附件面板
    Assets\Markdown\                  marked/highlight.js 本地资源
    config\                           app.json / log4net.config（复制到输出）
    data\                             cryptednotex.db（运行时生成）
    themes\                           用户自定义主题（覆盖内嵌）
```

---

## 3. 安全与加密方案（核心章节）

### 3.1 威胁模型

- **在范围内**：数据库文件（`data\cryptednotex.db`）被拷贝、同步盘泄露、磁盘被读取——攻击者只能得到密文与少量明文元数据（标题、分类名、附件文件名、时间戳、字数）。
- **不在范围内**：攻击者已在运行中程序的内存里（可读到解密后内容与密钥）；本机键盘记录器；用户把密码写在别处。
- **结论**：内存中密钥用后清零（尽最大努力），但不承诺对抗内存取证；文件级安全由加密保证。

### 3.2 密钥体系

每个用户（及每个独立密码分类）持有一对互相独立的凭据材料：

| 用途 | 算法 | 参数 | 存储 |
|---|---|---|---|
| 登录验证 | PBKDF2-HMAC-SHA256 | 迭代 100000，盐 16B（框架 `SecurityTool.PBKDF2Hash` 生成） | `user.password_hash`（框架格式字符串） |
| 内容加密密钥 | PBKDF2-HMAC-SHA256 | 迭代 `kdf_iterations`（默认 600000），盐 16B，输出 32B | 盐 base64 存 `kdf_salt`；密钥**永不落盘** |

- 两个盐随机独立，登录哈希不能推出内容密钥（反之亦然）。
- 独立密码分类：`category.password_hash / kdf_salt / kdf_iterations` 三列同构，密码为分类独立密码。
- `kdf_algorithm` 列预留算法迁移（当前恒为 `PBKDF2-SHA256`）。

### 3.3 KDF 参数升级策略

参数全部入库；程序默认迭代数提高后，**老用户在下一次修改密码（或改分类密码）时自动升到新默认值**——无需迁移脚本，blob 不受影响。

### 3.4 密文 blob 二进制格式

| 偏移 | 长度 | 字段 | 说明 |
|---|---|---|---|
| 0 | 1 | version | 格式版本，当前 `0x01`；换算法升版本，解码器按版本分派 |
| 1 | 12 | nonce | 每次 `RandomNumberGenerator` 随机生成 |
| 13 | N | ciphertext | AES-256-GCM 密文（N = 明文字节数） |
| 13+N | 16 | tag | GCM 认证标签 |

- AAD 域分隔：笔记正文 `"Rampage.CryptedNotex/Note/V1"`；附件 `"Rampage.CryptedNotex/Attachment/V1"`。AAD 不绑行 id（因此可**先加密后插入**，无需两步写）。
- AesGcm 使用双参构造 `new AesGcm(key, 16)`。
- 解密失败（版本不符 / tag 校验失败）＝密钥错误或数据被篡改：记 Warning 日志，向 UI 返回失败并提示"该内容无法解密（密码变更未完成或数据损坏）"。

### 3.5 修改密码 = 全量重加密

1. 验证旧密码 → 由旧 `kdf_salt` 派生旧内容密钥；
2. 生成新登录哈希（新盐）与新 `kdf_salt`，派生新内容密钥；
3. 事务内：更新 `user` 行 → 遍历该用户 `encrypt_mode=0` 分类的**全部**笔记（含回收站）与附件：旧钥解密 → 新钥加密写回（进度回调）→ 提交；
4. 任一步失败回滚，数据库保持旧密码可用；
5. 独立密码分类不受用户改密影响（其密钥不来自用户密码）。

分类独立密码的三个流程同构，作用域缩小到单分类：
- 设置独立密码（0→1）：需先解锁该分类（当前用全局钥可解），然后 userKey→categoryKey 重加密全部分类内容；
- 重置独立密码（1→1）：旧 categoryKey→新 categoryKey；
- 切回跟随全局（1→0）：categoryKey→userKey，清空独立密码列。

### 3.6 登录与防爆破

- 进程内 `MemoryLoginAttemptTracker`：同一用户名连续失败 5 次锁定 5 分钟（UI 倒计时）；
- 不落库：本地库攻击者可直接读文件，落库防爆破无实际增益；
- 密码验证 `PBKDF2Verify`（定长比较，防时序攻击）。

### 3.7 内存密钥生命周期

登录成功 → 派生内容密钥存 `CryptoSession` → 分类解锁表按需补充 → 自动锁定/登出时 `Array.Clear` 清零全部密钥并清空引用；重新进入需再次输入密码。
**忘记密码 = 数据不可恢复**（无后门、无恢复码），CreateUserForm 与文档 FAQ 明示。

### 3.8 备份与恢复

- 直接拷贝 `data\cryptednotex.db`（及整个程序目录）即为完整备份；恢复 = 覆盖回原位；
- 明文元数据边界：标题/分类名/附件文件名/时间戳/字数为明文，介意者应避免在标题中写敏感信息（正文始终密文）。

---

## 4. 功能规格

### 4.1 首运行引导与多用户登录

- 首运行（无用户）：LoginForm 自动转 CreateUserForm 向导（登录名/显示名/密码×2/强度提示）；成功后直接进入主界面；
- 登录：用户列表（显示名 + 最后登录时间）→ 输密码 → 防爆破锁定；
- 登出/自动锁定回登录界面；关闭主窗体＝退出程序。

### 4.2 分类管理

- 单级分类（不做树），每用户独立；`is_default=1` 的"默认分类"建用户时自动创建、不可删除；
- 新建/重命名（同用户内名称唯一，冲突报错）；上移/下移调整顺序；
- 右键菜单：重命名 / 设置独立密码 / 重置独立密码 / 切回跟随全局 / 上移 / 下移 / 删除；
- 删除分类：有笔记（含回收站）时必须勾选"将笔记移入默认分类"才允许删除，否则拒绝；默认分类拒绝删除；
- 独立密码分类在会话中未解锁时：列表正常显示（分类名明文），打开其中笔记前弹 UnlockCategoryForm。

### 4.3 笔记与三种类型编辑

- 新建笔记：选择分类与类型（纯文本/富文本/Markdown），初始标题 = `无标题`（可改，标题明文存库），创建后直接进入编辑；
- 打开已有笔记默认只读查看（标题、正文不可改，附件可打开但不可增删；Markdown 有预览时只显示渲染结果）。点「编辑」、双击列表或右键「编辑」才进入编辑；「完成」先保存再回到只读。回收站只可查看；
- 编辑区：PlainNoteEditor（多行文本）、RichNoteEditor（RichTextBox：加粗/斜体/下划线/删除线/字号/前景色/项目符号，RTF 格式存取）、MarkdownNoteEditor（等宽编辑 + WebView2 实时预览，`marked`+`highlight.js` 本地资源；预览可折叠；缺 WebView2 Runtime 时折叠预览并显示降级横幅）；
- 自动保存：停止输入 2 秒防抖保存；Ctrl+S 立即保存；切换笔记/分类/关窗/锁定前强制 flush；
- 字数：正文**非空白字符数**（RTF 按 PlainText 口径），保存时明文统计存 `word_count`；
- RTF 类型不保证完整保留 emoji（WinForms RichTextBox 限制），需要 emoji 请用 Markdown/纯文本类型。

### 4.4 搜索与置顶

- 搜索框按标题 `LIKE`（中英文、大小写不敏感、`%`/`_`/`\` 已转义），仅搜当前用户正常状态笔记；
- 笔记可置顶/取消置顶；列表排序：置顶优先 → update_time 倒序。

### 4.5 回收站

- 删除笔记 → `status=1` + `delete_time`（附件随笔记保留）；
- 回收站视图：按 delete_time 倒序，可恢复（回原分类）/ 彻底删除（附件级联硬删）/ 清空回收站；
- 恢复时原分类一定存在（删除分类必先将笔记迁入默认分类）。

### 4.6 附件

- 每条笔记可挂任意个附件：文件名/mime/大小明文，**内容加密**存 `note_attachment.content`；
- 图片（按 mime 判断）双击 → 内存解密 → ImagePreviewForm 预览（不落临时文件）；
- 其他文件 → 导出对话框（解密到用户指定路径）；
- 单文件 > 50MB 给出性能警告（SQLite blob 上限与内存考虑）。

### 4.7 自动锁定

- `user_setting.auto_lock_minutes`（默认 10，0=关闭）；空闲判定：键盘/鼠标/滚轮任一活动（IMessageFilter）；
- 系统锁定/会话切换同样触发；
- 触发：flush 未保存内容 → 清空全部密钥与解锁表 → 回登录界面（原密码可回，无需重启）；
- 重加密等长任务运行期间暂停判定。

### 4.8 主题系统

- JSON 主题：`id/name/description(一句身份描述)/darkTitleBar/colors(21 键,含 dangerText、success)/fonts(categoryPalette 8 色板)`，可选 `chrome`（`metal` none/silver/black、`wallpaper`、`glow`）（Schema 见附录）；
- `fonts.ui`（窗体基础字体）与 `fonts.listTitle`（列表字体）随主题应用；全部颜色配对（文字/底色、按钮、链接、回收站行、提示条）按 WCAG AA ≥4.5 校验通过；
- 内嵌扁平 6 套：light 浅色 / dark 深色 / forest 墨绿 / parchment 羊皮纸 / violet 紫罗兰 / contrast 高对比；
- 内嵌金属 5 套：invader 入侵者 / alienmorph 异形形态 / darkstar 暗星 / alx ALX / xenomorph 异形。新用户与尚未写入的全局默认主题为 xenomorph；已保存的偏好不改；
- 金属主题用原创徽记、拉丝底和壁纸（不使用第三方外壳商标）。编辑器正文区仍是纯色，壁纸只出现在窗体边缘和登录底；
- exe 旁 `themes\*.json` 按 id 覆盖或追加自定义主题；
- 登录前使用全局默认主题（setting 表），登录后读取个人偏好；实时切换（暗色标题栏 DWM 双向处理，暗→亮会写回浅色）；
- Markdown 预览画布按 `previewBack` 亮度自动选明暗 CSS，并把画布底/正文色着色为主题家族色；
- 暗色主题的系统滚动条与下拉切到 DarkMode_Explorer 资源，浅色切回默认；
- 分类色：色板按分类序号取模，用于侧栏色点与列表标记。

### 4.9 设置

用户级（user_setting 表）：主题、自动锁定分钟、编辑器字体族/字号（纯文本与 Markdown 编辑器，实时预览）、上次打开笔记（自动恢复）。
全局（setting 表）：默认主题 id 等。显示名修改、修改密码入口在设置窗。

---

## 5. 数据模型

表清单与完整 DDL 见 `documents\DatabaseTablesDesign\`（每表一个 `yyyyMMddHHmmssN.TABLE-{表名}.sql`，与运行时 SchemaInitializer 的内嵌资源同源）：

| 表 | 说明 | 主键 |
|---|---|---|
| setting | 全局应用设置（已有） | id 自增（种子 9999） |
| user | 用户 | id 自增 |
| category | 分类（加密模式/KDF 材料） | id 自增 |
| note | 笔记（标题明文/正文密文 BLOB） | id 自增 |
| note_attachment | 附件（内容密文 BLOB） | id 自增 |
| user_setting | 用户偏好（每用户一行） | id 自增 |
| schema_history | 建表台账（运行时自建，无 Po） | version TEXT |

枚举代码值（SQL 注释即权威）：
- `content_type`：0=纯文本，1=富文本，2=Markdown；
- `encrypt_mode`：0=跟随全局，1=独立密码；
- `status`：0=正常，1=回收站。

迁移机制：新增/变更表结构 = 追加新时间戳 SQL 文件（ALTER/新表），由 `schema_history` 台账保证只执行一次；**不改已应用的旧文件**。

---

## 6. 界面设计

### 6.1 窗体清单

| 窗体 | 说明 |
|---|---|
| LoginForm | 用户列表 + 密码框 + 登录/退出；防爆破倒计时；首运行自动转创建用户 |
| CreateUserForm | 首运行向导；成功直接进入主界面 |
| Notex（主窗体） | 三栏：分类侧栏 ｜ 工具栏+笔记列表 ｜ 编辑区+附件面板 |
| CategoryEditForm | 新建/重命名分类（新建可选独立密码） |
| UnlockCategoryForm | 独立密码解锁 |
| ChangePasswordForm | 改密码（进度条，模态） |
| SettingsForm | 主题画廊/自动锁定/编辑器字体/显示名/改密入口 |
| AboutForm | 版本/技术栈/加密方案摘要；DEBUG 加密自检 |
| ImagePreviewForm | 附件图片内存预览 |

### 6.2 主窗体布局（文字线框）

```
┌────────────┬──────────────────────┬───────────────────────────┐
│ + 新建分类  │ [搜索框____________]  │ 标题 [无标题笔记_______]    │
│────────────│ [新建▼][置顶][删除]   │ 类型标签  ·  修改时间  ·  字数│
│ ● 默认      │──────────────────────│───────────────────────────│
│ ● 工作  🔒2 │ 📌 笔记A   10-24 12:00│                           │
│ ● 日记      │    笔记B   10-23 18:31│   [编辑器区（按类型）]      │
│            │    笔记C   10-20 09:12│                           │
│────────────│                      │                           │
│ 🗑 回收站   │                      │───────────────────────────│
│ ⚙ 设置 ⋯   │                      │ [附件: 🖼a.png ×] [＋]     │
└────────────┴──────────────────────┴───────────────────────────┘
```

### 6.3 快捷键

- `Ctrl+S` 保存当前笔记；`Ctrl+F` 聚焦搜索；`Del` 在笔记列表上把选中笔记移入回收站（编辑器内不拦截）；`Ctrl+1` 默认分类，`Ctrl+2` 回收站，`Ctrl+3` 设置。

### 6.4 视觉

登录与首运行是壁纸上的金属卡片，顶部为档案徽记和「秘密档案」。主窗体三栏外留一圈壁纸；侧栏为金属品牌条、分类色点和绘制的锁；列表行自绘（置顶菱形、选中光）。扁平主题不加载壁纸，只换配色。

交互光影语言：「光」是界面的反馈动词——悬停点亮辉光（金属主题普通按钮悬停时边缘亮起主题辉光，强调按钮辉光加粗），按下时光熄灭下沉；列表行悬停有选中色洗版高亮；输入框聚焦时底色向强调色轻微混合。设置窗主题画廊为富预览卡片（壁纸条 + 名称 + 一句身份描述 + 三色点），点击即时全窗体生效。

---

## 7. 非功能需求

- 性能：万条笔记下列表/搜索（标题 LIKE 全扫）< 300ms；首窗体冷启动 < 3s；改密重加密 1000 条 < 10s（进度可见）；
- DPI：PerMonitorV2，125%/150% 无错位；编辑器字号独立可调；
- 完全离线：无遥测、无更新检查、无网络请求；
- 构建门禁：`dotnet build -c Debug -warnaserror` 零警告零错误。

---

## 8. 附录

### 8.1 主题 JSON Schema（示例）

```jsonc
{
  "id": "dark",
  "name": "深色",
  "darkTitleBar": true,
  "colors": {
    "windowBack": "#1E1E1E", "panelBack": "#252526", "panelAltBack": "#2D2D30",
    "border": "#3F3F46", "inputBack": "#333333", "inputBorder": "#3C3C3C",
    "text": "#F0F0F0", "textSecondary": "#A0A0A0",
    "accent": "#007ACC", "accentText": "#FFFFFF", "selectionBack": "#094771",
    "pinnedMark": "#FFB900", "trashTint": "#5A3A3A",
    "warning": "#D7BA7D", "danger": "#F14C4C", "link": "#4EC9B0",
    "editorBack": "#1E1E1E", "editorText": "#D4D4D4", "previewBack": "#FFFFFF"
  },
  "fonts": {
    "ui": { "family": "Microsoft YaHei UI", "size": 9.0 },
    "listTitle": { "family": "Microsoft YaHei UI", "size": 9.5 }
  },
  "categoryPalette": ["#5B8FF9", "#61DDAA", "#F6BD16", "#7262FD", "#78D3F8", "#9661BC", "#F6903D", "#008685"],
  "chrome": { "metal": "black", "wallpaper": "xenomorph", "glow": "#3EC6FF" }
}
```

`chrome` 可省略。`metal` 为 `none` 时与旧主题一样只铺纯色。

### 8.2 FAQ

- **忘记密码怎么办？** 无法恢复。内容密钥只能由密码派生，系统无后门/恢复码。
- **换电脑迁移？** 拷贝整个程序目录（含 `data\cryptednotex.db`）即可。
- **数据库里能看到什么？** 用户名/显示名、分类名、笔记标题、附件文件名、时间、字数；**看不到任何正文与附件内容**。
- **没装 WebView2 会怎样？** Markdown 笔记照常编辑，仅无预览（横幅提示安装地址）。
