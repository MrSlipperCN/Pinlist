namespace Pinlist.Core;

public enum TaskUrgency
{
    Low,
    Normal,
    High
}

public enum PinTaskStatus
{
    Open,
    Closed,
    Trashed
}

public sealed class PinTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; set; }
    public string Context { get; set; } = string.Empty;
    public TaskUrgency Urgency { get; set; } = TaskUrgency.Normal;
    public PinTaskStatus Status { get; private set; } = PinTaskStatus.Open;
    public bool IsPinned { get; set; }
    public long SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; private set; }

    public static PinTask Rehydrate(
        Guid id,
        string title,
        string context,
        TaskUrgency urgency,
        PinTaskStatus status,
        bool isPinned,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? closedAt,
        long sortOrder = 0)
    {
        return new PinTask
        {
            Id = id,
            Title = title,
            Context = context,
            Urgency = urgency,
            Status = status,
            IsPinned = isPinned,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ClosedAt = closedAt,
            SortOrder = sortOrder
        };
    }

    public void Update(string title, string? context, TaskUrgency urgency, bool isPinned)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A task title is required.", nameof(title));
        }

        Title = title.Trim();
        Context = context?.Trim() ?? string.Empty;
        Urgency = urgency;
        IsPinned = isPinned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Close()
    {
        Status = PinTaskStatus.Closed;
        ClosedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ClosedAt.Value;
    }

    public void SetImportant(bool isImportant)
    {
        IsPinned = isImportant;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetSortOrder(long sortOrder)
    {
        SortOrder = sortOrder;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Restore()
    {
        Status = PinTaskStatus.Open;
        ClosedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Trash()
    {
        Status = PinTaskStatus.Trashed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
