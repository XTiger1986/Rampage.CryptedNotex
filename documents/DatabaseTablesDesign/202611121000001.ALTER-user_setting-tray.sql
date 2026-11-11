-- 用户偏好追加:最小化到系统托盘
ALTER TABLE user_setting ADD COLUMN minimize_to_tray INTEGER NOT NULL DEFAULT 1;
