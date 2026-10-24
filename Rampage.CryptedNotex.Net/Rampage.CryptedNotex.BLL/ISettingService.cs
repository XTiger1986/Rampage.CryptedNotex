using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using Rampage.Model;
using System;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 系统设置服务：按 DTO 类型读取/保存 setting 表中的 JSON 配置，并提供进程级缓存
    /// </summary>
    /// <remarks>
    /// 设置行以 DTO 类型全名（type 列）定位；读取时对缺失/损坏数据返回 DTO 默认值，绝不抛出解析异常。
    /// </remarks>
    public interface ISettingService : IBaseService<Setting, ISettingDao>
    {
        /// <summary>
        /// 读取指定 DTO 类型的设置（直连数据库，不走缓存）
        /// </summary>
        /// <typeparam name="S">设置 DTO 类型（须有公共无参构造函数）</typeparam>
        /// <returns>设置实例；记录缺失或数据损坏时返回 DTO 默认值</returns>
        S GetSetting<S>() where S : class, new();

        /// <summary>
        /// 读取指定 DTO 类型的设置（走进程级缓存，缓存键 SETTING_{DTO类型全名}，随机过期 1800~2400 秒防雪崩）
        /// </summary>
        /// <typeparam name="S">设置 DTO 类型（须有公共无参构造函数）</typeparam>
        /// <returns>设置实例；记录缺失或数据损坏时返回 DTO 默认值</returns>
        S GetSettingFromCache<S>() where S : class, new();

        /// <summary>
        /// 保存指定 DTO 类型的设置（按 type 列存在则更新、否则插入），成功后使相关缓存失效
        /// </summary>
        /// <typeparam name="S">设置 DTO 类型（须有公共无参构造函数）</typeparam>
        /// <param name="setting">待保存的设置实例</param>
        /// <param name="adminId">操作管理员 Id（新增记入 create_admin_id，更新记入 update_admin_id）</param>
        /// <returns>操作结果；同时失效单项设置缓存与站点信息聚合缓存（SITE_INFO）</returns>
        R SaveSetting<S>(S setting, int adminId) where S : class, new();


    }
}
