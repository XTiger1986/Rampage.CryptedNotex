-- 用户偏好设置(每用户一行,创建用户时生成默认行)
DROP TABLE IF EXISTS user_setting;
CREATE TABLE user_setting (
    -- id
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- 用户 id
    user_id INTEGER NOT NULL,
    -- 主题 id(themes 内嵌或自定义主题的 id,如 light/dark/forest/parchment/violet/contrast)
    theme_id TEXT NOT NULL DEFAULT 'light',
    -- 自动锁定分钟数(0=不锁定)
    auto_lock_minutes INTEGER NOT NULL DEFAULT 10,
    -- 纯文本/Markdown 编辑器字体族
    editor_font_family TEXT NOT NULL DEFAULT 'Consolas',
    -- 纯文本/Markdown 编辑器字号
    editor_font_size REAL NOT NULL DEFAULT 12,
    -- 上次打开的笔记 id(0=无,登录后恢复)
    last_note_id INTEGER NOT NULL DEFAULT 0,
    -- 创建时间(UTC)
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- 修改时间(UTC)
    update_time DATETIME NULL
);

-- 唯一索引(每用户一行)
CREATE UNIQUE INDEX uq_user_setting_user ON user_setting (user_id);

-- 设置自增 ID 从 9999 开始（SQLite 的自增计数器表）
INSERT INTO sqlite_sequence (name, seq) VALUES ('user_setting', 9999);
