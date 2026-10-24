using Rampage.CryptedNotex.DAL;
using System;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 数据库结构门面：供宿主层(WinForm)在框架启动序列早期触发建表,而无需直接引用 DAL
    /// </summary>
    public static class DatabaseSchema
    {
        /// <summary>
        /// 确保数据库文件与全部表结构就绪(幂等；详见 DAL 的 SchemaInitializer)
        /// </summary>
        public static void EnsureCreated()
        {
            SchemaInitializer.EnsureSchema();
        }
    }
}
