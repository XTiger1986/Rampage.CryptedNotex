using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.DependencyInjection;
using Rampage.Hierarchy;
using Rampage.Model;
using System;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 用户偏好设置服务实现
    /// </summary>
    public class UserSettingService : BaseService<UserSetting, IUserSettingDao>, IUserSettingService
    {
        /// <summary>
        /// 用户偏好设置数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected IUserSettingDao UserSettingDao = default!;

        /// <inheritdoc />
        public UserSetting GetUserSetting(int userId)
        {
            UserSetting? setting = UserSettingDao.SelectOne(m => m.UserId == userId);
            if (setting != null)
            {
                return setting;
            }

            // 兼容历史数据：行缺失时补建默认行(CreateUser 正常路径已建)
            setting = new UserSetting
            {
                UserId = userId,
                CreateTime = UtcNow
            };
            UserSettingDao.Insert(setting);
            return setting;
        }

        /// <inheritdoc />
        public R SaveUserSetting(UserSetting setting)
        {
            if (setting.Id > 0)
            {
                setting.UpdateTime = UtcNow;
                return UserSettingDao.UpdateById(setting) ? R.Success() : R.Failure();
            }

            UserSetting? existing = UserSettingDao.SelectOne(m => m.UserId == setting.UserId);
            if (existing != null)
            {
                setting.Id = existing.Id;
                setting.UpdateTime = UtcNow;
                return UserSettingDao.UpdateById(setting) ? R.Success() : R.Failure();
            }

            setting.CreateTime = UtcNow;
            return UserSettingDao.Insert(setting) ? R.Success() : R.Failure();
        }
    }
}
