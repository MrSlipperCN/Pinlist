using System.ComponentModel;
using ModelContextProtocol.Server;
using Pinlist.Core;

namespace Pinlist.Integration.Mcp;

[McpServerToolType]
public sealed class PinlistTools(IPinTaskRepository repository)
{
    [McpServerTool(Name = "list_tasks", Title = "List Pinlist tasks", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("List Pinlist tasks by status. Use this before changing or trashing a task when its stable task ID is not already known.")]
    public async Task<TaskListResult> ListTasksAsync(
        [Description("Task status: open, closed, or trashed. Defaults to open.")] string status = "open",
        CancellationToken cancellationToken = default)
    {
        var tasks = await repository.ListAsync(ParseStatus(status), cancellationToken);
        return new TaskListResult(tasks.Select(ToResult).ToArray(), tasks.Count);
    }

    [McpServerTool(Name = "get_task", Title = "Get a Pinlist task", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Get one Pinlist task by its stable ID, including title, context, urgency, important flag, manual order, and status.")]
    public async Task<TaskResult> GetTaskAsync(
        [Description("Stable Pinlist task ID returned by list_tasks or create_task.")] string taskId,
        CancellationToken cancellationToken = default) =>
        ToResult(await GetRequiredTaskAsync(taskId, cancellationToken));

    [McpServerTool(Name = "create_task", Title = "Create a Pinlist task", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Create a new Pinlist task. Use when the user asks to add, remember, note, or pin a new task.")]
    public async Task<TaskResult> CreateTaskAsync(
        [Description("Short task title, up to 200 characters.")] string title,
        [Description("Optional background, notes, paths, links, or execution context, up to 10000 characters.")] string? context = null,
        [Description("Urgency: low, normal, or high. Defaults to normal.")] string urgency = "normal",
        [Description("Whether the task should carry the important flag. This does not override the user's manual order.")] bool pinned = false,
        CancellationToken cancellationToken = default)
    {
        ValidateText(title, context);
        var task = new PinTask { Title = title.Trim() };
        task.Update(title, context, ParseUrgency(urgency), pinned);
        await repository.SaveNewAtTopAsync(task, cancellationToken);
        return ToResult(task);
    }

    [McpServerTool(Name = "update_task", Title = "Update a Pinlist task", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Update selected fields of an existing Pinlist task. First use list_tasks if the stable task ID is unknown.")]
    public async Task<TaskResult> UpdateTaskAsync(
        [Description("Stable Pinlist task ID.")] string taskId,
        [Description("Replacement title. Omit to keep the existing title.")] string? title = null,
        [Description("Replacement context. Omit to keep the existing context; pass an empty string to clear it.")] string? context = null,
        [Description("Replacement urgency: low, normal, or high. Omit to keep the existing urgency.")] string? urgency = null,
        [Description("Replacement important-flag state. Omit to keep the existing state.")] bool? pinned = null,
        CancellationToken cancellationToken = default)
    {
        var task = await GetRequiredTaskAsync(taskId, cancellationToken);
        var updatedTitle = title ?? task.Title;
        var updatedContext = context ?? task.Context;
        ValidateText(updatedTitle, updatedContext);
        task.Update(updatedTitle, updatedContext, urgency is null ? task.Urgency : ParseUrgency(urgency), pinned ?? task.IsPinned);
        await repository.SaveAsync(task, cancellationToken);
        return ToResult(task);
    }

    [McpServerTool(Name = "set_important", Title = "Set task importance", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Set or clear a task's important flag without changing its manual list position.")]
    public async Task<TaskResult> SetImportantAsync(
        [Description("Stable Pinlist task ID.")] string taskId,
        [Description("True to mark important; false to clear the flag.")] bool important,
        CancellationToken cancellationToken = default)
    {
        var task = await GetRequiredTaskAsync(taskId, cancellationToken);
        task.SetImportant(important);
        await repository.SaveAsync(task, cancellationToken);
        return ToResult(task);
    }

    [McpServerTool(Name = "move_task", Title = "Move a Pinlist task", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Move a task to a zero-based position in the currently visible Pinlist order.")]
    public async Task<TaskResult> MoveTaskAsync(
        [Description("Stable Pinlist task ID.")] string taskId,
        [Description("Zero-based target position; 0 is the top.")] int position,
        CancellationToken cancellationToken = default)
    {
        var task = await GetRequiredTaskAsync(taskId, cancellationToken);
        if (task.Status == PinTaskStatus.Trashed)
            throw new ArgumentException("Trashed tasks must be restored before they can be moved.", nameof(taskId));

        var open = await repository.ListAsync(PinTaskStatus.Open, cancellationToken);
        var completedToday = (await repository.ListAsync(PinTaskStatus.Closed, cancellationToken))
            .Where(item => item.ClosedAt?.ToLocalTime().Date == DateTime.Today);
        var visible = open.Concat(completedToday).OrderBy(item => item.SortOrder).ToList();
        visible.RemoveAll(item => item.Id == task.Id);
        if (position < 0 || position > visible.Count)
            throw new ArgumentOutOfRangeException(nameof(position), $"position must be between 0 and {visible.Count}.");
        visible.Insert(position, task);
        await repository.SaveOrderAsync(visible, cancellationToken);
        return ToResult(task);
    }

    [McpServerTool(Name = "close_task", Title = "Close a Pinlist task", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Mark a Pinlist task as closed. Closing preserves the task and is preferred when the user says it is done or completed.")]
    public async Task<TaskResult> CloseTaskAsync(
        [Description("Stable Pinlist task ID.")] string taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await GetRequiredTaskAsync(taskId, cancellationToken);
        if (task.Status != PinTaskStatus.Closed) task.Close();
        await repository.SaveAsync(task, cancellationToken);
        return ToResult(task);
    }

    [McpServerTool(Name = "restore_task", Title = "Restore a Pinlist task", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Restore a closed or trashed Pinlist task to the open list.")]
    public async Task<TaskResult> RestoreTaskAsync(
        [Description("Stable Pinlist task ID.")] string taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await GetRequiredTaskAsync(taskId, cancellationToken);
        if (task.Status != PinTaskStatus.Open) task.Restore();
        await repository.SaveAsync(task, cancellationToken);
        return ToResult(task);
    }

    [McpServerTool(Name = "trash_task", Title = "Move a Pinlist task to trash", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Move a Pinlist task to recoverable trash. Use only when the user explicitly asks to delete or remove it. This does not permanently erase data.")]
    public async Task<TaskResult> TrashTaskAsync(
        [Description("Stable Pinlist task ID. Resolve ambiguous or duplicate titles with list_tasks before calling.")] string taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await GetRequiredTaskAsync(taskId, cancellationToken);
        if (task.Status != PinTaskStatus.Trashed) task.Trash();
        await repository.SaveAsync(task, cancellationToken);
        return ToResult(task);
    }

    private async Task<PinTask> GetRequiredTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(taskId, out var id))
            throw new ArgumentException("taskId must be a valid Pinlist task ID.", nameof(taskId));
        return await repository.GetAsync(id, cancellationToken)
               ?? throw new KeyNotFoundException($"No Pinlist task exists with ID '{taskId}'.");
    }

    private static void ValidateText(string title, string? context)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A task title is required.", nameof(title));
        if (title.Trim().Length > 200) throw new ArgumentException("Task title cannot exceed 200 characters.", nameof(title));
        if (context?.Length > 10_000) throw new ArgumentException("Task context cannot exceed 10000 characters.", nameof(context));
    }

    private static TaskUrgency ParseUrgency(string urgency)
    {
        if (Enum.TryParse<TaskUrgency>(urgency, true, out var parsed)) return parsed;
        throw new ArgumentException("urgency must be low, normal, or high.", nameof(urgency));
    }

    private static PinTaskStatus ParseStatus(string status) => status.Trim().ToLowerInvariant() switch
    {
        "open" => PinTaskStatus.Open,
        "closed" => PinTaskStatus.Closed,
        "trashed" or "trash" => PinTaskStatus.Trashed,
        _ => throw new ArgumentException("status must be open, closed, or trashed.", nameof(status))
    };

    private static TaskResult ToResult(PinTask task) => new(
        task.Id.ToString("D"), task.Title, task.Context,
        task.Urgency.ToString().ToLowerInvariant(), task.Status.ToString().ToLowerInvariant(),
        task.IsPinned, task.SortOrder, task.CreatedAt, task.UpdatedAt, task.ClosedAt);
}

public sealed record TaskListResult(IReadOnlyList<TaskResult> Tasks, int Count);

public sealed record TaskResult(
    string Id,
    string Title,
    string Context,
    string Urgency,
    string Status,
    bool Pinned,
    long SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);
