using Microsoft.Extensions.Caching.Memory;
using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.DependencyInjection;
using Rampage.Hierarchy;
using Rampage.Json;
using Rampage.Model;
using Rampage.Utilities;
using System;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 系统设置服务实现：按 DTO 类型全名定位 setting 行，JSON 数据防御式解析（损坏时返回默认值）
    /// </summary>
    public class SettingService : BaseService<Setting, ISettingDao>, ISettingService
    {
        /// <summary>
        /// 设置数据访问，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected ISettingDao SettingDao = default!;

        /// <inheritdoc />
        public S GetSetting<S>() where S : class, new()
        {
            Type type = typeof(S);
            string typeName = type.FullName!;
            Setting? setting = SettingDao.SelectOne(m => m.Type == typeName);
            if (setting != null && !string.IsNullOrEmpty(setting.Data))
            {
                // JsonTool.Parse 解析失败时静默返回 default，兜底为 DTO 默认值，绝不抛出解析异常
                S? model = JsonTool.Parse<S>(setting.Data);
                return model ?? new S();
            }
            return new S();
        }

        /// <inheritdoc />
        public S GetSettingFromCache<S>() where S : class, new()
        {
            Type type = typeof(S);
            string typeName = type.FullName!;
            // 过期时间取 1800~2400 秒随机值，避免多个设置键同时失效造成雪崩
            return HandleCache<S>(() => {
                return GetSetting<S>();
            }, Setting.CACHE_KEY_ONE_TPL, MathTool.GenerateInt(1800, 2400), typeName)!;
        }

        /// <inheritdoc />
        public R SaveSetting<S>(S setting, int adminId) where S : class, new()
        {
            Type type = typeof(S);
            string typeName = type.FullName!;

            Setting? model = SettingDao.SelectOne(s => s.Type == typeName);
            if (model == null)
            {
                model = new Setting
                {
                    Name = type.Name,
                    Type = typeName,
                    CreateTime = UtcNow
                };
            }

            model.Data = JsonTool.Stringify(setting);
            model.UpdateTime = UtcNow;

            bool result;
            if (model.Id > 0)
            {
                model.UpdateAdminId = adminId;
                result = SettingDao.UpdateById(model);
            }
            else
            {
                model.CreateAdminId = adminId;
                result = SettingDao.Insert(model);
            }
            if (result)
            {
                MemoryCache.Remove(string.Format(Setting.CACHE_KEY_ONE_TPL, typeName));
            }

            return result ? R.Success() : R.Failure();
        }


    }
}
