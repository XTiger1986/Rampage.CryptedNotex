using Rampage.Data;
using System;

namespace Rampage.CryptedNotex.Model.Po
{
    /// <summary>
    /// 系统设置
    /// </summary>
    [TableMapping("setting")]
    public class Setting
    {
        /// <summary>单项设置缓存键前缀。</summary>
        public static readonly string CACHE_KEY_ONE_PFX = "SETTING";

        /// <summary>单项设置缓存键模板。</summary>
        public static readonly string CACHE_KEY_ONE_TPL = CACHE_KEY_ONE_PFX + "_{0}";

        /// <summary>
        /// 主键
        /// </summary>
        [FieldMapping(PrimaryKey = true, ColumnName = "id", AutoIncrement = true)]
        public int Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [FieldMapping(ColumnName = "name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 对应的类型完整名称
        /// </summary>
        [FieldMapping(ColumnName = "type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 设置JSON(在程序代码中定义各类DTO模型);展馆配置含defaultThemeId/availableThemeIds/guideExhibitionId,主题素材与资源清单随前端发布
        /// </summary>
        [FieldMapping(ColumnName = "data", Jsonb = true)]
        public string Data { get; set; } = "{}";

        /// <summary>
        /// 创建管理员 Id
        /// </summary>
        [FieldMapping(ColumnName = "create_admin_id")]
        public int CreateAdminId { get; set; }

        /// <summary>
        /// 修改管理员 Id（0 表示未修改）；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "update_admin_id")]
        public int UpdateAdminId { get; set; }

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
