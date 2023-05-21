using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Invoice.Core;
using Microsoft.Data.Sqlite;

namespace Invoice.Tests.Core;

public class InvoiceDictionaryTests : IDisposable, IAsyncLifetime
{
    private const string ConnectionString = "Data Source=InvoiceDictionaryTests;Mode=Memory;Cache=Shared";

    private readonly InvoiceDictionary _invoiceDictionary;
    private readonly SqliteConnection _connection;
    private static readonly SHA256 Sha256 = SHA256.Create();

    public InvoiceDictionaryTests()
    {
        _connection = new SqliteConnection(ConnectionString);
        _invoiceDictionary = new InvoiceDictionary(ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await DropTable(_connection);
        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _connection.CloseAsync();
    }

    public void Dispose()
    {
        if (_connection.State != ConnectionState.Closed)
        {
            _connection.Close();
        }
        _connection.Dispose();

        GC.SuppressFinalize(this);
    }

    private static async Task DropTable(SqliteConnection connection)
    {
        await using (connection)
        {
            await connection.OpenAsync();

            var command = new SqliteCommand
            {
                Connection = connection,
                CommandText = "DROP TABLE IF EXISTS dict",
                CommandType = CommandType.Text,
            };

            await command.ExecuteNonQueryAsync();
        }
    }


    [Fact]
    public async Task SetValue_ShouldInsertValue_WhenKeyDoesNotExist()
    {
        // Arrange
        const string key = "foo";
        const string value = "bar";

        // Act
        var result = await _invoiceDictionary.SetValue(key, value);

        // Assert
        Assert.True(result);
        var command = new SqliteCommand
        {
            Connection = _connection,
            CommandText = "SELECT value FROM dict WHERE id = @Id",
            CommandType = CommandType.Text,
            Parameters =
            {
                new SqliteParameter("@Id", ComputeHash(Sha256, key))
            }
        };

        await using (command)
        {
            await EnsureOpen(_connection);
            var reader = await command.ExecuteReaderAsync();
            await using (reader)
            {
                Assert.True(reader.Read());
                Assert.Equal(value, reader.GetString(0));
            }
        }
    }

    [Fact]
    public async Task SetValue_ShouldUpdateValue_WhenKeyExists()
    {
        // Arrange
        const string key = "foo";
        const string oldValue = "bar";
        const string newValue = "baz";

        await CreateTable(_connection, "dict"); // Ensure database exists

        var command =
            new SqliteCommand
            {
                Connection = _connection,
                CommandText = "INSERT INTO dict (id, key, value) VALUES (@Id, @Key, @Value)",
                CommandType = CommandType.Text,
                Parameters =
                {
                    new SqliteParameter("@Id", ComputeHash(Sha256, key)),
                    new SqliteParameter("@Key", key),
                    new SqliteParameter("@Value", oldValue)
                }
            };

        await using (command)
        {
            await EnsureOpen(_connection);
            await command.ExecuteNonQueryAsync();
        }

        // Act
        var result = await _invoiceDictionary.SetValue(key, newValue);

        // Assert
        Assert.True(result);
        var selectCommand = new SqliteCommand
        {
            Connection = _connection,
            CommandText = "SELECT value FROM dict WHERE id = @Id",
            CommandType = CommandType.Text,
            Parameters =
            {
                new SqliteParameter("@Id", ComputeHash(Sha256, key))
            }
        };

        await using (selectCommand)
        {
            await EnsureOpen(_connection);
            var reader = await selectCommand.ExecuteReaderAsync();
            await using (reader)
            {
                Assert.True(reader.Read());
                Assert.Equal(newValue, reader.GetString(0));
            }
        }
    }

    [Fact]
    public async Task GetValue_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Arrange
        const string key = "foo";

        // Act
        var result = await _invoiceDictionary.GetValue(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetValue_ShouldReturnValue_WhenKeyExists()
    {
        // Arrange
        const string key = "foo";
        const string value = "bar";

        await CreateTable(_connection, "dict"); // Ensure database exists

        var command =
            new SqliteCommand
            {
                Connection = _connection,
                CommandText = "INSERT INTO dict (id, key, value) VALUES (@Id, @Key, @Value)",
                CommandType = CommandType.Text,
                Parameters =
                {
                    new SqliteParameter("@Id", ComputeHash(Sha256, key)),
                    new SqliteParameter("@Key", key),
                    new SqliteParameter("@Value", value)
                }
            };

        await using (command)
        {
            await EnsureOpen(_connection);
            await command.ExecuteNonQueryAsync();
        }

        // Act
        var result = await _invoiceDictionary.GetValue(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task RemoveValue_ShouldReturnTrueIfValueWasNotOnDatabase()
    {
        // Arrange
        var connectionString = ConnectionString;
        var invoiceDictionary = new InvoiceDictionary(connectionString);

        // Act
        var result = await invoiceDictionary.RemoveValue("non_existing_key");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveValue_Should_Remove_Value()
    {
        // Arrange
        var key = "testKey";
        var value = "testValue";
        await _invoiceDictionary.SetValue(key, value);

        // Act
        var result = await _invoiceDictionary.RemoveValue(key);

        // Assert
        Assert.True(result);

        // Check that the value was removed
        var retrievedValue = await _invoiceDictionary.GetValue(key);
        Assert.Null(retrievedValue);
    }

    [Fact]
    public async Task RemoveValue_Should_Return_True_If_Value_Does_Not_Exist()
    {
        // Arrange
        var key = "testKey";

        // Act
        var result = await _invoiceDictionary.RemoveValue(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetValues_Should_Return_All_Values()
    {
        // Arrange
        var expectedValues = new Dictionary<string, string>
        {
            {"key1", "value1"},
            {"key2", "value2"},
            {"key3", "value3"},
            {"key4", "value4"}
        };

        foreach (var (key, value) in expectedValues)
        {
            await _invoiceDictionary.SetValue(key, value);
        }

        // Act
        var retrievedValues = await _invoiceDictionary.GetValues(0, 10);

        // Assert
        var keyValuePairs = retrievedValues.ToList();
        Assert.Equal(expectedValues.Count, keyValuePairs.Count);

        foreach (var (key, value) in expectedValues)
        {
            Assert.Contains(keyValuePairs, x => x.Key == key && x.Value == value);
        }
    }

    private static async Task<bool> CreateTable(SqliteConnection connection, string tableName, CancellationToken cancellationToken = default)
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

    private static async Task EnsureOpen(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static long ComputeHash(HashAlgorithm algorithm, string key)
    {
        var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt64(hash, 0);
    }
}