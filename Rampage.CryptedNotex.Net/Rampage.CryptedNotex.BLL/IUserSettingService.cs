using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using Rampage.Model;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 用户偏好设置服务(每用户一行)
    /// </summary>
    public interface IUserSettingService : IBaseService<UserSetting, IUserSettingDao>
    {
        /// <summary>
        /// 读取用户偏好(行缺失时创建默认行)
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <returns>用户偏好(永不为 null)</returns>
        UserSetting GetUserSetting(int userId);

        /// <summary>
        /// 保存用户偏好
        /// </summary>
        /// <param name="setting">用户偏好</param>
        /// <returns>操作结果</returns>
        R SaveUserSetting(UserSetting setting);
    }
}
