using Rampage.CryptedNotex.DAL;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Po;
using Rampage.Hierarchy;
using Rampage.Model;
using System;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 用户服务：多用户创建/登录验证/改密全量重加密
    /// </summary>
    public interface IUserService : IBaseService<User, IUserDao>
    {
        /// <summary>
        /// 统计用户总数(首运行判断是否进入创建用户向导)
        /// </summary>
        /// <returns>用户总数</returns>
        int CountUsers();

        /// <summary>
        /// 获取用户摘要列表(登录界面选择账号；不含哈希/盐)
        /// </summary>
        /// <returns>用户摘要列表(按登录名排序)</returns>
        List<UserBriefDto> GetUserBriefList();

        /// <summary>
        /// 创建用户：事务内建用户行(登录哈希+内容密钥派生材料)+默认分类+用户偏好默认行
        /// </summary>
        /// <param name="username">登录名(小写唯一)</param>
        /// <param name="displayName">显示名(空则取登录名)</param>
        /// <param name="password">明文密码</param>
        /// <param name="contentKey">成功时输出用户内容密钥(直接进入已登录态；调用方负责会话持有与清零)</param>
        /// <returns>成功携带新建用户摘要；失败(重名/密码过短等)返回原因</returns>
        R<UserBriefDto> CreateUser(string username, string displayName, string password, out byte[]? contentKey);

        /// <summary>
        /// 验证登录(PBKDF2 定长比较)；成功更新最后登录时间并派生用户内容密钥
        /// </summary>
        /// <param name="username">登录名(小写)</param>
        /// <param name="password">明文密码</param>
        /// <param name="contentKey">成功时输出用户内容密钥(调用方负责会话持有与清零)；失败为 null</param>
        /// <returns>用户摘要；用户不存在或密码错误返回 null(防爆破计数由界面层负责)</returns>
        UserBriefDto? VerifyUser(string username, string password, out byte[]? contentKey);

        /// <summary>
        /// 修改用户密码并全量重加密：事务内更新 user 凭据行,并把跟随全局(encrypt_mode=0)分类的全部笔记(含回收站)与附件用旧钥解密、新钥重加密写回；独立密码分类不受影响
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="oldPassword">旧密码</param>
        /// <param name="newPassword">新密码</param>
        /// <param name="progress">进度回调(0~100,界面进度条)</param>
        /// <returns>成功时 Data 为新内容密钥(byte[],供会话替换)；失败(旧密码错误/重加密失败)数据库回滚保持旧密码可用</returns>
        R ChangePassword(int userId, string oldPassword, string newPassword, IProgress<int>? progress);

        /// <summary>
        /// 修改显示名
        /// </summary>
        /// <param name="userId">用户 Id</param>
        /// <param name="displayName">新显示名</param>
        /// <returns>操作结果</returns>
        R UpdateDisplayName(int userId, string displayName);
    }
}
