using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Invoice.Core;

internal sealed class InvoiceDictionary : IDisposable
{
    private const string TableName = "dict";
    private readonly SHA256 _sha256 = SHA256.Create();
    private readonly SqliteConnection _connection;

    public InvoiceDictionary(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
    }

    public async Task<string?> GetValue(string key, CancellationToken cancellationToken = default)
    {
        var command = new SqliteCommand
        {
            Connection = _connection,
            CommandText = $"SELECT value FROM {TableName} WHERE id = @Id LIMIT 1",
            CommandType = CommandType.Text,
            Parameters =
            {
                new SqliteParameter("@Id", ComputeHash(_sha256, key)),
            }
        };

        if (false == await CheckTableExists(_connection, TableName, cancellationToken))
        {
            return null;
        }

        await using (command)
        {
            await EnsureOpen(_connection, cancellationToken);

            var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
            await using (reader)
            {
                if (!reader.HasRows)
                {
                    return null;
                }

                if (false == await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                return reader.GetString("value");
            }
        }
    }

    public async Task<IEnumerable<KeyValuePair<string, string>>> GetValues(int offset, int limit, CancellationToken cancellationToken = default)
    {
        var command = new SqliteCommand
        {
            Connection = _connection,
            CommandText = $"SELECT key,value FROM {TableName} LIMIT @Limit OFFSET @Offset",
            CommandType = CommandType.Text,
            Parameters =
            {
                new SqliteParameter("@Limit", limit),
                new SqliteParameter("@Offset", offset),
            }
        };

        if (false == await CheckTableExists(_connection, TableName, cancellationToken))
        {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        var values = new List<KeyValuePair<string, string>>();
        await using (command)
        {
            await EnsureOpen(_connection, cancellationToken);

            var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
            await using (reader)
            {
                if (false == reader.HasRows)
                {
                    return Enumerable.Empty<KeyValuePair<string, string>>();
                }

                while (await reader.ReadAsync(cancellationToken))
                {
                    var key = reader.GetString("key");
                    var value = reader.GetString("value");
                    values.Add(new KeyValuePair<string, string>(key, value));
                }
            }
        }

        return values;
    }

    public async Task<bool> SetValue(string key, string value, CancellationToken cancellationToken = default)
    {
        var command = new SqliteCommand
        {
            Connection = _connection,
            CommandText = $"INSERT INTO {TableName} (id,key,value) VALUES (@Id,@Key,@Value) ON CONFLICT(id) DO UPDATE SET value=excluded.value",
            CommandType = CommandType.Text,
            Parameters =
            {
                new SqliteParameter("@Id", ComputeHash(_sha256, key)),
                new SqliteParameter("@Key", key),
                new SqliteParameter("@Value", value),
            }
        };

        if (false == await CheckTableExists(_connection, TableName, cancellationToken))
        {
            if (false == await CreateTable(_connection, TableName, cancellationToken))
            {
                return false;
            }
        }

        await using (command)
        {
            await EnsureOpen(_connection, cancellationToken);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
    }

    public async Task<bool> RemoveValue(string key, CancellationToken cancellationToken = default)
    {
        if (false == await CheckTableExists(_connection, TableName, cancellationToken))
        {
            // It wasn't on the db to start with.
            return true;
        }

        var command = new SqliteCommand
        {
            Connection = _connection,
            CommandText = $"DELETE FROM {TableName} WHERE id=@Id",
            CommandType = CommandType.Text,
            Parameters =
            {
                new SqliteParameter("@Id", ComputeHash(_sha256, key)),
            }
        };

        await using (command)
        {
            await EnsureOpen(_connection, cancellationToken);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
    }

    private static async Task EnsureOpen(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static async Task<bool> CheckTableExists(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var command = new SqliteCommand
        {
            CommandText = $"SELECT 1 FROM sqlite_master WHERE type='table' AND name='{tableName}';",
            CommandType = CommandType.Text,
            Connection = connection
        };

        await using (command)
        {
            await EnsureOpen(connection, cancellationToken);

            var reader = await command.ExecuteReaderAsync(cancellationToken);
            await using (reader)
            {
                return reader.HasRows;
            }
        }
    }

    private static async Task<bool> CreateTable(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var command = new SqliteCommand
        {
            CommandText = @$"CREATE TABLE IF NOT EXISTS {tableName} (
                                id INTEGER PRIMARY KEY,
                                key TEXT NOT NULL,
                                value TEXT NOT NULL
                            )",
            CommandType = CommandType.Text,
            Connection = connection
        };

        await using (command)
        {
            await EnsureOpen(connection, cancellationToken);

            return await command.ExecuteNonQueryAsync(cancellationToken) == 0;
        }
    }

    private static long ComputeHash(HashAlgorithm algorithm, string key)
    {
        var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt64(hash, 0);
    }

    [ExcludeFromCodeCoverage]
    public void Dispose()
    {
        _sha256.Dispose();
        _connection.Dispose();
    }
}