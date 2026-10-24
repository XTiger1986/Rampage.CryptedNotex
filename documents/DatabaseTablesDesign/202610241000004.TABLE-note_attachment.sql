-- 笔记附件(内容加密存储)
DROP TABLE IF EXISTS note_attachment;
CREATE TABLE note_attachment (
    -- id
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- 所属用户 id
    user_id INTEGER NOT NULL,
    -- 所属笔记 id(随笔记彻底删除而级联删除)
    note_id INTEGER NOT NULL,
    -- 文件名(明文,原上传文件名)
    file_name TEXT NOT NULL,
    -- MIME 类型(明文,用于图标与预览判断)
    mime_type TEXT NOT NULL DEFAULT 'application/octet-stream',
    -- 原始明文字节数
    file_size INTEGER NOT NULL DEFAULT 0,
    -- 附件密文(版本1字节+nonce12字节+密文N字节+tag16字节;密钥为所属笔记分类的内容密钥)
    content BLOB NOT NULL,
    -- 创建时间(UTC)
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- 修改时间(UTC)
    update_time DATETIME NULL
);

-- 普通索引(笔记附件列表)
CREATE INDEX idx_attachment_note ON note_attachment (note_id);

-- 普通索引(按用户重加密遍历)
CREATE INDEX idx_attachment_user ON note_attachment (user_id);

-- 设置自增 ID 从 9999 开始（SQLite 的自增计数器表）
INSERT INTO sqlite_sequence (name, seq) VALUES ('note_attachment', 9999);
