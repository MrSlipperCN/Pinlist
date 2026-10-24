using Microsoft.Data.Sqlite;
using Pinlist.Core;

namespace Pinlist.Storage.SQLite;

public sealed class SqlitePinTaskRepository : IPinTaskRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public SqlitePinTaskRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
    }

    public async Task<IReadOnlyList<PinTask>> ListAsync(
        PinTaskStatus status,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, context, urgency, status, is_pinned, created_at, updated_at, closed_at, sort_order
            FROM pin_tasks
            WHERE status = $status
            ORDER BY sort_order ASC, created_at DESC;
            """;
        command.Parameters.AddWithValue("$status", (int)status);

        var tasks = new List<PinTask>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks;
    }

    public async Task<PinTask?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, context, urgency, status, is_pinned, created_at, updated_at, closed_at, sort_order
            FROM pin_tasks
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTask(reader) : null;
    }

    public async Task SaveAsync(PinTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = CreateSaveCommand(connection, task);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveNewAtTopAsync(PinTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = transaction;
        orderCommand.CommandText = "SELECT COALESCE(MIN(sort_order), 0) - 1024 FROM pin_tasks WHERE status != $trashed;";
        orderCommand.Parameters.AddWithValue("$trashed", (int)PinTaskStatus.Trashed);
        task.SetSortOrder(Convert.ToInt64(await orderCommand.ExecuteScalarAsync(cancellationToken)));
        await using var saveCommand = CreateSaveCommand(connection, task, transaction);
        await saveCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveOrderAsync(IReadOnlyList<PinTask> tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        for (var index = 0; index < tasks.Count; index++)
        {
            tasks[index].SetSortOrder(index * 1024L);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE pin_tasks SET sort_order = $sortOrder, updated_at = $updatedAt WHERE id = $id;";
            command.Parameters.AddWithValue("$sortOrder", tasks[index].SortOrder);
            command.Parameters.AddWithValue("$updatedAt", tasks[index].UpdatedAt.ToString("O"));
            command.Parameters.AddWithValue("$id", tasks[index].Id.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static SqliteCommand CreateSaveCommand(SqliteConnection connection, PinTask task, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO pin_tasks
                (id, title, context, urgency, status, is_pinned, created_at, updated_at, closed_at, sort_order)
            VALUES
                ($id, $title, $context, $urgency, $status, $isPinned, $createdAt, $updatedAt, $closedAt, $sortOrder)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                context = excluded.context,
                urgency = excluded.urgency,
                status = excluded.status,
                is_pinned = excluded.is_pinned,
                updated_at = excluded.updated_at,
                closed_at = excluded.closed_at,
                sort_order = excluded.sort_order;
            """;
        command.Parameters.AddWithValue("$id", task.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$context", task.Context);
        command.Parameters.AddWithValue("$urgency", (int)task.Urgency);
        command.Parameters.AddWithValue("$status", (int)task.Status);
        command.Parameters.AddWithValue("$isPinned", task.IsPinned ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", task.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$closedAt", task.ClosedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", task.SortOrder);
        return command;
    }

    public async Task<long> GetNextTopSortOrderAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MIN(sort_order), 0) - 1024 FROM pin_tasks WHERE status != $trashed;";
        command.Parameters.AddWithValue("$trashed", (int)PinTaskStatus.Trashed);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA busy_timeout = 5000;
                CREATE TABLE IF NOT EXISTS pin_tasks (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    context TEXT NOT NULL DEFAULT '',
                    urgency INTEGER NOT NULL,
                    status INTEGER NOT NULL,
                    is_pinned INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    closed_at TEXT NULL,
                    sort_order INTEGER NOT NULL DEFAULT 0
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);

            await using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = "PRAGMA table_info(pin_tasks);";
            var hasSortOrder = false;
            await using (var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    hasSortOrder |= string.Equals(reader.GetString(1), "sort_order", StringComparison.OrdinalIgnoreCase);
            }

            if (!hasSortOrder)
            {
                await using var migration = connection.CreateCommand();
                migration.CommandText = "ALTER TABLE pin_tasks ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0;";
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "CREATE INDEX IF NOT EXISTS ix_pin_tasks_manual_sort ON pin_tasks(status, sort_order ASC);";
            await indexCommand.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static PinTask ReadTask(SqliteDataReader reader)
    {
        return PinTask.Rehydrate(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            (TaskUrgency)reader.GetInt32(3),
            (PinTaskStatus)reader.GetInt32(4),
            reader.GetInt32(5) == 1,
            DateTimeOffset.Parse(reader.GetString(6)),
            DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
            reader.GetInt64(9));
    }
}
