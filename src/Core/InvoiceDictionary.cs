using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace invoice.Core;

internal sealed class InvoiceDictionary
{
    private const string TableName = "dict";
    private readonly SHA256 _sha256 = SHA256.Create();
    private string ConnectionString { get; }

    public InvoiceDictionary(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public async Task<string?> GetValue(string key, CancellationToken cancellationToken = default)
    {
        string? value = null;
        var connection = CreateConnection(ConnectionString);
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);

            if (false == await CheckTableExists(connection, TableName, cancellationToken))
            {
                return null;
            }

            /* Get value or default from DB */
            var hashedKey = ComputeHash(_sha256, key);
            var command = new SqliteCommand
            {
                Connection = connection,
                CommandText = $"SELECT value FROM {TableName} WHERE id = @Id LIMIT 1",
                CommandType = CommandType.Text,
                Parameters =
                {
                    new SqliteParameter("@Id", hashedKey),

                }
            };

            var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
            if (reader.HasRows)
            {
                if (false == reader.Read())
                {
                    return null;
                }

                value = reader.GetString("value");
            }
        }

        return value;
    }

    public async Task<IEnumerable<KeyValuePair<string, string>>> GetValues(int offset, int limit, CancellationToken cancellationToken = default)
    {
        Dictionary<string,string>? values = null;
        var connection = CreateConnection(ConnectionString);
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);

            if (false == await CheckTableExists(connection, TableName, cancellationToken))
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            /* Get value or default from DB */
            var command = new SqliteCommand
            {
                Connection = connection,
                CommandText = $"SELECT key,value FROM {TableName} LIMIT @Limit OFFSET @Offset",
                CommandType = CommandType.Text,
                Parameters =
                {
                    new SqliteParameter("@Limit", limit),
                    new SqliteParameter("@Offset", offset),
                }
            };

            var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
            if (false == reader.HasRows)
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            if (false == reader.Read())
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            values = new Dictionary<string, string>();
            do
            {
                var key = reader.GetString("key");
                var value = reader.GetString("value");

                values.Add(key, value);
            } while (reader.Read());
        }

        return values;
    }

    public async Task<bool> SetValue(string key, string value, CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection(ConnectionString);
        var result = false;
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);

            if (false == await CheckTableExists(connection, TableName, cancellationToken))
            {
                result = await CreateTable(connection, TableName, cancellationToken);
                if (false == result)
                {
                    return false;
                }
            }

            /* Upsert value to DB */
            var hashedKey = ComputeHash(_sha256, key);
            var command = new SqliteCommand
            {
                Connection = connection,
                CommandText = $"INSERT INTO {TableName} (id,key,value) VALUES (@Id,@Key,@Value) ON CONFLICT(id) DO UPDATE SET value=excluded.value",
                CommandType = CommandType.Text,
                Parameters =
                { 
                    new SqliteParameter("@Id", hashedKey),
                    new SqliteParameter("@Key", key),
                    new SqliteParameter("@Value", value),
                }
            };

            result = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        return result;
    }

    public async Task<bool> RemoveValue(string key, CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection(ConnectionString);
        var result = false;
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);

            if (false == await CheckTableExists(connection, TableName, cancellationToken))
            {
                // It wasn't on the db to start with.
                return true;
            }

            /* Check value to DB */
            var hashedKey = ComputeHash(_sha256, key);
            var command = new SqliteCommand
            {
                Connection = connection,
                CommandText = $"DELETE FROM {TableName} WHERE id=@Id",
                CommandType = CommandType.Text,
                Parameters =
                {
                    new SqliteParameter("@Id", hashedKey),
                }
            };

            result = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        return result;
    }

    private static async Task<bool> CheckTableExists(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var command = new SqliteCommand
        {
            CommandText = $"SELECT 1 FROM sqlite_master WHERE type='table' AND name='{tableName}';",
            CommandType = CommandType.Text,
            Connection = connection
        };

        var reader = await command.ExecuteReaderAsync(cancellationToken);
        return reader.HasRows;
    }

    private static SqliteConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);

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

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static long ComputeHash(HashAlgorithm algorithm, string key)
    {
        var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt64(hash, 0);
    }
}