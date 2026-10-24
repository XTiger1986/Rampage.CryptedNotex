using Rampage.CryptedNotex.Model.Enums;
using Rampage.Data;
using System;

namespace Rampage.CryptedNotex.Model.Po
{
    /// <summary>
    /// 笔记分类(单级,每用户独立)
    /// </summary>
    [TableMapping("category")]
    public class Category
    {
        /// <summary>
        /// 主键
        /// </summary>
        [FieldMapping(PrimaryKey = true, ColumnName = "id", AutoIncrement = true)]
        public int Id { get; set; }

        /// <summary>
        /// 所属用户 Id
        /// </summary>
        [FieldMapping(ColumnName = "user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// 分类名(明文,同用户内唯一)
        /// </summary>
        [FieldMapping(ColumnName = "name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 是否默认分类(0=否;1=是,创建用户时自动生成,不可删除)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "is_default")]
        public int IsDefault { get; set; }

        /// <summary>
        /// 加密模式(0=跟随全局:内容密钥由用户密码派生;1=独立密码:内容密钥由分类密码派生)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "encrypt_mode")]
        public int EncryptMode { get; set; }

        /// <summary>
        /// 独立密码验证哈希(encrypt_mode=1 时非空;与分类内容密钥派生相互独立)
        /// </summary>
        [FieldMapping(ColumnName = "password_hash")]
        public string? PasswordHash { get; set; }

        /// <summary>
        /// 独立密码内容密钥派生算法(当前恒为 PBKDF2-SHA256)；数据库默认值：PBKDF2-SHA256
        /// </summary>
        [FieldMapping(ColumnName = "kdf_algorithm")]
        public string KdfAlgorithm { get; set; } = "PBKDF2-SHA256";

        /// <summary>
        /// 独立密码内容密钥派生迭代次数；数据库默认值：600000
        /// </summary>
        [FieldMapping(ColumnName = "kdf_iterations")]
        public int KdfIterations { get; set; }

        /// <summary>
        /// 独立密码内容密钥派生盐(base64,16字节;encrypt_mode=1 时非空)
        /// </summary>
        [FieldMapping(ColumnName = "kdf_salt")]
        public string? KdfSalt { get; set; }

        /// <summary>
        /// 排序值(同用户内升序)；数据库默认值：0
        /// </summary>
        [FieldMapping(ColumnName = "sort_order")]
        public int SortOrder { get; set; }

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

        /// <summary>
        /// 加密模式枚举视图(映射列仍为 int 的 EncryptMode,此处仅换算)
        /// </summary>
        [FieldMapping(NotColumn = true)]
        public CategoryEncryptMode EncryptModeValue
        {
            get
            {
                return (CategoryEncryptMode)EncryptMode;
            }
        }
    }
}
