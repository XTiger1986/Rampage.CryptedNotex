-- 笔记分类(单级,每用户独立)
DROP TABLE IF EXISTS category;
CREATE TABLE category (
    -- id
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- 所属用户 id
    user_id INTEGER NOT NULL,
    -- 分类名(明文,同用户内唯一)
    name TEXT NOT NULL,
    -- 是否默认分类(0=否;1=是,创建用户时自动生成,不可删除)
    is_default INTEGER NOT NULL DEFAULT 0,
    -- 加密模式(0=跟随全局:内容密钥由用户密码派生;1=独立密码:内容密钥由分类密码派生)
    encrypt_mode INTEGER NOT NULL DEFAULT 0,
    -- 独立密码验证哈希(encrypt_mode=1 时非空;与分类内容密钥派生相互独立)
    password_hash TEXT NULL,
    -- 独立密码内容密钥派生算法(当前恒为 PBKDF2-SHA256)
    kdf_algorithm TEXT NOT NULL DEFAULT 'PBKDF2-SHA256',
    -- 独立密码内容密钥派生迭代次数
    kdf_iterations INTEGER NOT NULL DEFAULT 600000,
    -- 独立密码内容密钥派生盐(base64,16字节;encrypt_mode=1 时非空)
    kdf_salt TEXT NULL,
    -- 排序值(同用户内升序)
    sort_order INTEGER NOT NULL DEFAULT 0,
    -- 创建时间(UTC)
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- 修改时间(UTC)
    update_time DATETIME NULL
);

-- 唯一索引(同用户分类名不重复)
CREATE UNIQUE INDEX uq_category_user_name ON category (user_id, name);

-- 普通索引(侧栏按排序值加载)
CREATE INDEX idx_category_user ON category (user_id, sort_order);

-- 设置自增 ID 从 9999 开始（SQLite 的自增计数器表）
INSERT INTO sqlite_sequence (name, seq) VALUES ('category', 9999);
