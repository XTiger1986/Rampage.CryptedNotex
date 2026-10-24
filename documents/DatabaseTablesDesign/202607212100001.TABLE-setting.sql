-- 系统设置
DROP TABLE IF EXISTS setting;
CREATE TABLE setting (
    -- id
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- 名称
    name TEXT NOT NULL,
    -- 对应的类型完整名称
    type TEXT NOT NULL,
    -- 设置JSON(在程序代码中定义各类DTO模型);展馆配置含defaultThemeId/availableThemeIds/guideExhibitionId,主题素材与资源清单随前端发布
    data JSON NOT NULL,
    create_admin_id INTEGER NOT NULL,
    update_admin_id INTEGER NOT NULL DEFAULT 0,
    -- 创建时间(UTC)
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- 修改时间(UTC)
    update_time DATETIME NULL
);

-- 唯一索引
CREATE UNIQUE INDEX uq_setting_name ON setting (name);

-- 设置自增 ID 从 9999 开始（SQLite 的自增计数器表）
INSERT INTO sqlite_sequence (name, seq) VALUES ('setting', 9999);