using Rampage.Data;
using System;

namespace Rampage.CryptedNotex.Model.Po
{
    /// <summary>
    /// 用户
    /// </summary>
    [TableMapping("user")]
    public class User
    {
        /// <summary>
        /// 主键
        /// </summary>
        [FieldMapping(PrimaryKey = true, ColumnName = "id", AutoIncrement = true)]
        public int Id { get; set; }

        /// <summary>
        /// 登录名(小写唯一)
        /// </summary>
        [FieldMapping(ColumnName = "username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 显示名
        /// </summary>
        [FieldMapping(ColumnName = "display_name")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 登录验证哈希(PBKDF2-SHA256$迭代$盐$哈希;与内容密钥派生相互独立,不能互推)
        /// </summary>
        [FieldMapping(ColumnName = "password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 内容密钥派生算法(当前恒为 PBKDF2-SHA256,预留算法迁移)；数据库默认值：PBKDF2-SHA256
        /// </summary>
        [FieldMapping(ColumnName = "kdf_algorithm")]
        public string KdfAlgorithm { get; set; } = "PBKDF2-SHA256";

        /// <summary>
        /// 内容密钥派生迭代次数；数据库默认值：600000
        /// </summary>
        [FieldMapping(ColumnName = "kdf_iterations")]
        public int KdfIterations { get; set; }

        /// <summary>
        /// 内容密钥派生盐(base64,16字节,随机;与登录哈希盐相互独立)
        /// </summary>
        [FieldMapping(ColumnName = "kdf_salt")]
        public string KdfSalt { get; set; } = string.Empty;

        /// <summary>
        /// 最后登录时间(UTC)
        /// </summary>
        [FieldMapping(ColumnName = "last_login_time")]
        public DateTime? LastLoginTime { get; set; }

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
