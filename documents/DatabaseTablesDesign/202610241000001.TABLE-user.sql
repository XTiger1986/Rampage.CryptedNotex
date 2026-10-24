-- 用户
DROP TABLE IF EXISTS user;
CREATE TABLE user (
    -- id
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- 登录名(小写唯一)
    username TEXT NOT NULL,
    -- 显示名
    display_name TEXT NOT NULL,
    -- 登录验证哈希(PBKDF2-SHA256$迭代$盐$哈希;与内容密钥派生相互独立,不能互推)
    password_hash TEXT NOT NULL,
    -- 内容密钥派生算法(当前恒为 PBKDF2-SHA256,预留算法迁移)
    kdf_algorithm TEXT NOT NULL DEFAULT 'PBKDF2-SHA256',
    -- 内容密钥派生迭代次数
    kdf_iterations INTEGER NOT NULL DEFAULT 600000,
    -- 内容密钥派生盐(base64,16字节,随机;与登录哈希盐相互独立)
    kdf_salt TEXT NOT NULL,
    -- 最后登录时间(UTC)
    last_login_time DATETIME NULL,
    -- 创建时间(UTC)
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- 修改时间(UTC)
    update_time DATETIME NULL
);

-- 唯一索引(登录名小写唯一)
CREATE UNIQUE INDEX uq_user_username ON user (username);

-- 设置自增 ID 从 9999 开始（SQLite 的自增计数器表）
INSERT INTO sqlite_sequence (name, seq) VALUES ('user', 9999);
