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
        /// 主题 id(内嵌或自定义主题的 id)。新用户代码初值为 xenomorph；已有行保持库中的值。历史 SQL 列默认仍是 light。
        /// </summary>
        [FieldMapping(ColumnName = "theme_id")]
        public string ThemeId { get; set; } = "xenomorph";

        /// <summary>
        /// 自动锁定分钟数(0=不锁定)；数据库默认值：10
        /// </summary>
        [FieldMapping(ColumnName = "auto_lock_minutes")]
        public int AutoLockMinutes { get; set; } = 10;

        /// <summary>
        /// 纯文本/Markdown 编辑器字体族；数据库默认值：Consolas
        /// </summary>
        [FieldMapping(ColumnName = "editor_font_family")]
        public string EditorFontFamily { get; set; } = "Consolas";

        /// <summary>
        /// 纯文本/Markdown 编辑器字号；数据库默认值：12
        /// </summary>
        /// <remarks>属性默认值须与数据库列默认值一致：ORM 插入时会带上属性值,若此处缺省为 0 会覆盖列默认值并导致 Font 构造异常。</remarks>
        [FieldMapping(ColumnName = "editor_font_size")]
        public double EditorFontSize { get; set; } = 12;

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
