# CryptedNotex 数据表设计说明

**SQLite**（数据库文件 `Rampage.CryptedNotex.WinForm\data\cryptednotex.db`）

## 表清单

| 文件 | 表 | 说明 |
|---|---|---|
| 202607212100001.TABLE-setting.sql | setting | 全局应用设置（DTO JSON 模式） |
| 202610241000001.TABLE-user.sql | user | 用户（登录哈希 + 内容密钥 KDF 材料） |
| 202610241000002.TABLE-category.sql | category | 笔记分类（两种加密模式，独立密码时含自己的 KDF 材料） |
| 202610241000003.TABLE-note.sql | note | 笔记（标题明文，正文密文 BLOB） |
| 202610241000004.TABLE-note_attachment.sql | note_attachment | 附件（内容密文 BLOB，随笔记级联删除） |
| 202610241000005.TABLE-user_setting.sql | user_setting | 用户偏好（每用户一行） |

另：运行时台账表 `schema_history(version TEXT PRIMARY KEY, applied_time DATETIME NOT NULL)` 由
`Rampage.CryptedNotex.DAL\Implementations\SchemaInitializer.cs` 自动创建，无 SQL 文件、无 Po。

## 约定

- 每表一个 `yyyyMMddHHmmssN.TABLE-{表名小写}.sql`；`DROP TABLE IF EXISTS` + CREATE + 中文 `--` 列注释 + 索引 + `sqlite_sequence` 种子 9999（业务 id 从 10000 起）；
- 本目录 SQL 同时是**运行时建表的唯一权威来源**：DAL 工程以链接方式把它们内嵌为程序集资源，启动时按文件名序执行未应用脚本并记入 `schema_history` 台账（只执行一次）；
- **改表结构必须追加新的时间戳脚本**（ALTER 或新表），不得修改已应用的旧文件；开发期手工改库后须同步删除台账对应行才会重放；
- 枚举列一律 smallint 语义的 INTEGER 代码值，取值以 SQL 注释为权威，程序内建对应枚举（`Rampage.CryptedNotex.Model\Enums\`）；
- 时间一律 UTC；明文元数据边界（标题/分类名/附件文件名明文，正文/附件内容密文）见产品文档 §3.8。
