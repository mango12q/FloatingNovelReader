using System.Data;
using Microsoft.Data.Sqlite;

namespace FloatingNovelReader.Infrastructure;

/// <summary>
/// SQLite 连接工厂实现。每次 CreateConnection 返回一个新的 SqliteConnection 并自动开启外键约束。
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public SqliteTransaction BeginTransaction(SqliteConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return connection.BeginTransaction(isolationLevel);
    }
}
