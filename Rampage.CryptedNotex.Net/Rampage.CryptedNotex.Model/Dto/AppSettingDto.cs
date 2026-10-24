namespace Rampage.CryptedNotex.Model.Dto
{
    /// <summary>
    /// 全局应用设置(setting 表的 JSON DTO；adminId 语义为当前操作用户 Id)
    /// </summary>
    public class AppSettingDto
    {
        /// <summary>
        /// 默认主题 id(登录前界面使用；登录后切换为用户个人偏好 user_setting.theme_id)
        /// </summary>
        public string DefaultThemeId { get; set; } = "light";
    }
}
