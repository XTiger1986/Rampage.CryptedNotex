using Rampage.CryptedNotex.Common;
using Rampage.CryptedNotex.Model.Dto;
using Rampage.CryptedNotex.Model.Po;
using System;
using System.Collections.Generic;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 加密会话：登录后持有用户内容密钥与各独立密码分类的解锁状态；锁定/登出时全部清零
    /// </summary>
    /// <remarks>
    /// BLL 服务对密钥来源零感知(显式传参),本会话是密钥在 UI 层的唯一权威持有者。
    /// 单实例由 NotexGuiContext 创建并传递给各窗体。
    /// </remarks>
    public sealed class CryptoSession
    {
        private byte[]? _userContentKey;
        private readonly Dictionary<int, byte[]> _unlockedCategoryKeys = new Dictionary<int, byte[]>();

        /// <summary>
        /// 会话被锁定(自动锁定/登出)时通知各窗体收尾
        /// </summary>
        public event Action? Locked;

        /// <summary>
        /// 当前登录用户摘要；未登录为 null
        /// </summary>
        public UserBriefDto? User
        {
            get;
            private set;
        }

        /// <summary>
        /// 是否已初始化(登录成功)
        /// </summary>
        public bool IsInitialized
        {
            get
            {
                return User != null && _userContentKey != null;
            }
        }

        /// <summary>
        /// 初始化会话(登录/创建用户成功后调用)
        /// </summary>
        /// <param name="user">用户摘要</param>
        /// <param name="userContentKey">用户内容密钥</param>
        public void Init(UserBriefDto user, byte[] userContentKey)
        {
            Lock();
            User = user;
            _userContentKey = userContentKey;
        }

        /// <summary>
        /// 取分类的内容密钥：跟随全局返回用户密钥；独立密码分类须已解锁,未解锁抛 InvalidOperationException
        /// </summary>
        /// <param name="category">分类</param>
        /// <returns>内容密钥(会话内部引用,调用方不得清零)</returns>
        /// <exception cref="InvalidOperationException">独立密码分类尚未解锁</exception>
        public byte[] GetContentKey(Category category)
        {
            if (category.EncryptMode == (int)Model.Enums.CategoryEncryptMode.IndependentPassword)
            {
                if (_unlockedCategoryKeys.TryGetValue(category.Id, out byte[]? key))
                {
                    return key;
                }
                throw new InvalidOperationException("分类「" + category.Name + "」已加独立密码，请先解锁");
            }
            return _userContentKey ?? throw new InvalidOperationException("会话未初始化");
        }

        /// <summary>
        /// 取用户内容密钥(改密/切模式等需要旧钥的场景)
        /// </summary>
        /// <returns>用户内容密钥</returns>
        public byte[] GetUserContentKey()
        {
            return _userContentKey ?? throw new InvalidOperationException("会话未初始化");
        }

        /// <summary>
        /// 独立密码分类是否已解锁
        /// </summary>
        /// <param name="categoryId">分类 Id</param>
        /// <returns>已解锁返回 true</returns>
        public bool IsCategoryUnlocked(int categoryId)
        {
            return _unlockedCategoryKeys.ContainsKey(categoryId);
        }

        /// <summary>
        /// 标记独立密码分类已解锁(解锁成功后调用)
        /// </summary>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="categoryKey">分类内容密钥</param>
        public void MarkCategoryUnlocked(int categoryId, byte[] categoryKey)
        {
            _unlockedCategoryKeys.TryGetValue(categoryId, out byte[]? old);
            CryptoTool.ClearKey(old);
            _unlockedCategoryKeys[categoryId] = categoryKey;
        }

        /// <summary>
        /// 改密成功后替换用户内容密钥
        /// </summary>
        /// <param name="newKey">新用户内容密钥</param>
        public void ReplaceUserKey(byte[] newKey)
        {
            CryptoTool.ClearKey(_userContentKey);
            _userContentKey = newKey;
        }

        /// <summary>
        /// 分类独立密码重置后替换该分类的解锁密钥
        /// </summary>
        /// <param name="categoryId">分类 Id</param>
        /// <param name="newKey">新分类内容密钥</param>
        public void ReplaceCategoryKey(int categoryId, byte[] newKey)
        {
            MarkCategoryUnlocked(categoryId, newKey);
        }

        /// <summary>
        /// 锁定会话：清零全部密钥与解锁表(自动锁定/登出/退出前调用)
        /// </summary>
        public void Lock()
        {
            CryptoTool.ClearKey(_userContentKey);
            _userContentKey = null;
            foreach (KeyValuePair<int, byte[]> pair in _unlockedCategoryKeys)
            {
                CryptoTool.ClearKey(pair.Value);
            }
            _unlockedCategoryKeys.Clear();
            User = null;
            Locked?.Invoke();
        }
    }
}
