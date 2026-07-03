using System.Data;
using Microsoft.Data.Sqlite;

namespace FloatingNovelReader.Infrastructure;

/// <summary>
/// SQLite 连接工厂。抽象连接创建逻辑，便于测试 Mock 和未来切换数据库引擎。
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>创建已打开且已执行 PRAGMA foreign_keys=ON 的连接</summary>
    SqliteConnection CreateConnection();

    /// <summary>创建事务</summary>
    SqliteTransaction BeginTransaction(SqliteConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}
