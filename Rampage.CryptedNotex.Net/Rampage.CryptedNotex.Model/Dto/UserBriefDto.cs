using System;

namespace Rampage.CryptedNotex.Model.Dto
{
    /// <summary>
    /// 用户摘要(登录界面列表用；绝不携带哈希/盐等凭据材料)
    /// </summary>
    public class UserBriefDto
    {
        /// <summary>
        /// 用户 Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 登录名
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 显示名
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 最后登录时间(UTC,可空)
        /// </summary>
        public DateTime? LastLoginTime { get; set; }
    }
}
