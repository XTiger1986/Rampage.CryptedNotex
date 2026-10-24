using Rampage.Data;
using System;

namespace Rampage.CryptedNotex.Model.Po
{
    /// <summary>
    /// 用户偏好设置(每用户一行,创建用户时生成默认行)
    /// </summary>
    [TableMapping("user_setting")]
    public class UserSetting
    {
        /// <summary>
        /// 主键
        /// </summary>
        [FieldMapping(PrimaryKey = true, ColumnName = "id", AutoIncrement = true)]
        public int Id { get; set; }

        /// <summary>
        /// 用户 Id
        /// </summary>
        [FieldMapping(ColumnName = "user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// 主题 id(内嵌或自定义主题的 id,如 light/dark/forest/parchment/violet/contrast)；数据库默认值：light
        /// </summary>
        [FieldMapping(ColumnName = "theme_id")]
        public string ThemeId { get; set; } = "light";

        /// <summary>
        /// 自动锁定分钟数(0=不锁定)；数据库默认值：10
        /// </summary>
        [FieldMapping(ColumnName = "auto_lock_minutes")]
        public int AutoLockMinutes { get; set; }

        /// <summary>
        /// 纯文本/Markdown 编辑器字体族；数据库默认值：Consolas
        /// </summary>
        [FieldMapping(ColumnName = "editor_font_family")]
        public string EditorFontFamily { get; set; } = "Consolas";

        /// <summary>
        /// 纯文本/Markdown 编辑器字号；数据库默认值：12
        /// </summary>
        [FieldMapping(ColumnName = "editor_font_size")]
        public double EditorFontSize { get; set; }

        /// <summary>
        /// 上次打开的笔记 id(0=无,登录后恢复)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "last_note_id")]
        public int LastNoteId { get; set; }

        /// <summary>
        /// 创建时间(UTC)；数据库默认值：CURRENT_TIMESTAMP
        /// </summary>
        /// <remarks>新增记录时由调用方设置 UTC 创建时间；数据库默认值仅在省略该列时生效。</remarks>
        [FieldMapping(ColumnName = "create_time")]
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 修改时间(UTC)
        /// </summary>
        [FieldMapping(ColumnName = "update_time")]
        public DateTime? UpdateTime { get; set; }
    }
}
