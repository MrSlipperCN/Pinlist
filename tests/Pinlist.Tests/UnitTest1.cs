namespace Pinlist.Tests;

public sealed class PinTaskTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"pinlist-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public void CloseAndRestore_PreserveTaskAndUpdateStatus()
    {
        var task = new Core.PinTask { Title = "Ship Pinlist" };

        task.Close();
        Assert.Equal(Core.PinTaskStatus.Closed, task.Status);
        Assert.NotNull(task.ClosedAt);

        task.Restore();
        Assert.Equal(Core.PinTaskStatus.Open, task.Status);
        Assert.Null(task.ClosedAt);
    }

    [Fact]
    public async Task Repository_PersistsManualOrderIndependentlyOfImportanceAndUrgency()
    {
        var repository = new Storage.SQLite.SqlitePinTaskRepository(_databasePath);
        var normal = new Core.PinTask { Title = "Normal task" };
        normal.Update(normal.Title, "context", Core.TaskUrgency.Normal, false);
        var pinned = new Core.PinTask { Title = "Pinned task" };
        pinned.Update(pinned.Title, "important", Core.TaskUrgency.High, true);
        normal.SetSortOrder(2048);
        pinned.SetSortOrder(1024);

        await repository.SaveAsync(normal);
        await repository.SaveAsync(pinned);

        var tasks = await repository.ListAsync(Core.PinTaskStatus.Open);

        Assert.Equal(2, tasks.Count);
        Assert.Equal(pinned.Id, tasks[0].Id);
        Assert.Equal("important", tasks[0].Context);
    }

    [Fact]
    public async Task Repository_AtomicallyAddsNewTasksAtTopAndSavesManualOrder()
    {
        var repository = new Storage.SQLite.SqlitePinTaskRepository(_databasePath);
        var task = new Core.PinTask { Title = "Existing" };
        task.SetSortOrder(-1024);
        await repository.SaveAsync(task);

        var newTask = new Core.PinTask { Title = "New" };
        await repository.SaveNewAtTopAsync(newTask);
        var tasks = await repository.ListAsync(Core.PinTaskStatus.Open);

        Assert.Equal(newTask.Id, tasks[0].Id);

        await repository.SaveOrderAsync([task, newTask]);
        tasks = await repository.ListAsync(Core.PinTaskStatus.Open);
        Assert.Equal(task.Id, tasks[0].Id);
        Assert.Equal(newTask.Id, tasks[1].Id);
    }

    [Fact]
    public async Task McpTools_ProvideRecoverableTaskLifecycle()
    {
        var repository = new Storage.SQLite.SqlitePinTaskRepository(_databasePath);
        var tools = new Integration.Mcp.PinlistTools(repository);

        var created = await tools.CreateTaskAsync("MCP task", "Created by Codex", "high", true);
        var listed = await tools.ListTasksAsync();
        Assert.Contains(listed.Tasks, task => task.Id == created.Id);

        var closed = await tools.CloseTaskAsync(created.Id);
        Assert.Equal("closed", closed.Status);

        var trashed = await tools.TrashTaskAsync(created.Id);
        Assert.Equal("trashed", trashed.Status);

        var restored = await tools.RestoreTaskAsync(created.Id);
        Assert.Equal("open", restored.Status);
    }

    [Fact]
    public async Task McpStdioServer_AdvertisesExpectedTools()
    {
        var serverAssembly = Environment.GetEnvironmentVariable("PINLIST_MCP_SERVER_PATH")
                             ?? Path.Combine(AppContext.BaseDirectory, "Pinlist.Integration.Mcp.dll");
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"pinlist-mcp-{Guid.NewGuid():N}");
        var options = new ModelContextProtocol.Client.StdioClientTransportOptions
        {
            Name = "pinlist-test",
            Command = "dotnet",
            Arguments = [serverAssembly],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["PINLIST_DATA_DIRECTORY"] = dataDirectory
            }
        };

        await using var client = await ModelContextProtocol.Client.McpClient.CreateAsync(
            new ModelContextProtocol.Client.StdioClientTransport(options));
        var tools = await client.ListToolsAsync();
        var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Subset(names, new HashSet<string>(StringComparer.Ordinal)
        {
            "list_tasks", "get_task", "create_task", "update_task",
            "set_important", "move_task", "close_task", "restore_task", "trash_task"
        });

        if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, recursive: true);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }
}
