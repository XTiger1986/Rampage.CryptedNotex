using Microsoft.Data.Sqlite;
using Rampage.AppLog;
using Rampage.Data.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Rampage.CryptedNotex.DAL
{
    /// <summary>
    /// 数据库结构初始化器：首次运行时按文件名序执行内嵌的表设计 SQL 脚本并记入 schema_history 台账
    /// </summary>
    /// <remarks>
    /// 脚本唯一权威来源为 documents\DatabaseTablesDesign\*.sql(以链接方式内嵌为本程序集资源,资源名前缀见 SCHEMA_RESOURCE_PFX)；
    /// 台账保证每个脚本只执行一次,因此脚本中的 DROP TABLE IF EXISTS 不会伤害既有数据。
    /// 必须在框架 InitializeDatabaseClients(启动时连库校验 Po 与表列匹配)之前调用,由 NotexApplication.InitializeServiceContainer 前置挂钩。
    /// </remarks>
    public static class SchemaInitializer
    {
        /// <summary>
        /// 内嵌表结构脚本的资源名前缀(DAL.csproj 中 LogicalName 统一约定)
        /// </summary>
        private const string SCHEMA_RESOURCE_PFX = "Rampage.CryptedNotex.DAL.Schema.";

        /// <summary>
        /// 建表台账表名(本初始化器自建自管,无对应 Po)
        /// </summary>
        private const string HISTORY_TABLE = "schema_history";

        /// <summary>
        /// 确保数据库文件与全部表结构就绪(幂等：已应用脚本跳过,失败大声抛出由启动流程终止程序)
        /// </summary>
        public static void EnsureSchema()
        {
            DatabaseClientConfig? clientConfig = FindSqliteClientConfig();
            if (clientConfig == null || string.IsNullOrEmpty(clientConfig.ConnectionString))
            {
                throw new InvalidOperationException("未在配置节 DatabaseFactory:Clients 中找到 Sqlite 客户端连接串,无法初始化数据库");
            }

            EnsureDataDirectory(clientConfig.ConnectionString);

            using (SqliteConnection connection = new SqliteConnection(clientConfig.ConnectionString))
            {
                connection.Open();
                ExecuteSimple(connection, "PRAGMA journal_mode=WAL");
                EnsureHistoryTable(connection);

                List<string> applied = GetAppliedVersions(connection);
                foreach (string resourceName in GetSchemaResourceNames())
                {
                    string version = resourceName.Substring(SCHEMA_RESOURCE_PFX.Length);
                    if (applied.Contains(version))
                    {
                        continue;
                    }

                    string sql = ReadResourceText(resourceName);
                    using (SqliteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            ExecuteInTransaction(connection, transaction, sql);
                        }
                        catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
                        {
                            // 库状态与台账脱节(手工改库/异常拷贝残留等)：新增列已存在说明变更实际已生效,
                            // 按已应用补记台账自愈,而不是让程序每次启动都崩在这里
                            RampageLogger.Warn("结构脚本 " + version + " 的目标列已存在，按已应用补记台账：" + ex.Message);
                        }
                        ExecuteInTransaction(connection, transaction,
                            "INSERT INTO " + HISTORY_TABLE + " (version, applied_time) VALUES (@Version, @AppliedTime)",
                            new SqliteParameter("@Version", version), new SqliteParameter("@AppliedTime", DateTime.UtcNow));
                        transaction.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// 从 DatabaseFactory 配置中查找 Sqlite 类型客户端(本项目单库,取第一个即唯一)
        /// </summary>
        /// <returns>Sqlite 客户端配置；不存在返回 null</returns>
        private static DatabaseClientConfig? FindSqliteClientConfig()
        {
            Dictionary<string, DatabaseClientConfig>? clients = DatabaseFactory.Config?.Clients;
            if (clients == null)
            {
                return null;
            }

            foreach (DatabaseClientConfig client in clients.Values)
            {
                if (string.Equals(client.Type, "Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    return client;
                }
            }
            return null;
        }

        /// <summary>
        /// 连接串使用相对路径 Data Source=data\cryptednotex.db 时,SQLite 不会自动创建目录,这里前置创建
        /// </summary>
        /// <param name="connectionString">SQLite 连接串</param>
        private static void EnsureDataDirectory(string connectionString)
        {
            string dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (string.IsNullOrEmpty(dataSource) || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
                || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// 创建建表台账表(若不存在)
        /// </summary>
        private static void EnsureHistoryTable(SqliteConnection connection)
        {
            ExecuteSimple(connection, @"CREATE TABLE IF NOT EXISTS " + HISTORY_TABLE + @" (
                -- 脚本文件名(版本键)
                version TEXT PRIMARY KEY,
                -- 应用时间(UTC)
                applied_time DATETIME NOT NULL
            )");
        }

        /// <summary>
        /// 读取已应用的脚本版本清单
        /// </summary>
        /// <returns>已应用版本(脚本文件名)集合</returns>
        private static List<string> GetAppliedVersions(SqliteConnection connection)
        {
            List<string> versions = new List<string>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT version FROM " + HISTORY_TABLE;
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        versions.Add(reader.GetString(0));
                    }
                }
            }
            return versions;
        }

        /// <summary>
        /// 枚举内嵌表结构脚本资源名(按名称升序即时间戳顺序)
        /// </summary>
        /// <returns>资源名列表</returns>
        private static List<string> GetSchemaResourceNames()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(name => name.StartsWith(SCHEMA_RESOURCE_PFX, StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 读取内嵌资源全文
        /// </summary>
        /// <param name="resourceName">资源名</param>
        /// <returns>脚本内容</returns>
        private static string ReadResourceText(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!)
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// 执行无事务单条命令
        /// </summary>
        private static void ExecuteSimple(SqliteConnection connection, string sql)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 在事务内执行命令(脚本可含多语句,SqliteCommand 按批次依次执行)
        /// </summary>
        private static void ExecuteInTransaction(SqliteConnection connection, SqliteTransaction transaction, string sql, params SqliteParameter[] parameters)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                foreach (SqliteParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
                command.ExecuteNonQuery();
            }
        }
    }
}
