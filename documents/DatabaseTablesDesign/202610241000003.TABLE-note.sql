-- 笔记
DROP TABLE IF EXISTS note;
CREATE TABLE note (
    -- id
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- 所属用户 id
    user_id INTEGER NOT NULL,
    -- 分类 id
    category_id INTEGER NOT NULL,
    -- 标题(明文,供 LIKE 搜索;敏感信息请勿写入标题)
    title TEXT NOT NULL DEFAULT '',
    -- 内容类型(0=纯文本;1=富文本RTF;2=Markdown)
    content_type INTEGER NOT NULL DEFAULT 0,
    -- 正文密文(版本1字节+nonce12字节+密文N字节+tag16字节;密钥为所属分类的内容密钥)
    content BLOB NULL,
    -- 状态(0=正常;1=回收站)
    status INTEGER NOT NULL DEFAULT 0,
    -- 是否置顶(0=否;1=是)
    is_pinned INTEGER NOT NULL DEFAULT 0,
    -- 字数(正文非空白字符数,保存时统计)
    word_count INTEGER NOT NULL DEFAULT 0,
    -- 移入回收站时间(UTC)
    delete_time DATETIME NULL,
    -- 创建时间(UTC)
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- 修改时间(UTC)
    update_time DATETIME NULL
);

-- 普通索引(分类内笔记列表)
CREATE INDEX idx_note_user_category ON note (user_id, category_id, status);

-- 普通索引(回收站查询)
CREATE INDEX idx_note_user_status ON note (user_id, status);

-- 设置自增 ID 从 9999 开始（SQLite 的自增计数器表）
INSERT INTO sqlite_sequence (name, seq) VALUES ('note', 9999);
