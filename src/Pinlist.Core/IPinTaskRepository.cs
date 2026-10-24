namespace Pinlist.Core;

public interface IPinTaskRepository
{
    Task<IReadOnlyList<PinTask>> ListAsync(PinTaskStatus status, CancellationToken cancellationToken = default);
    Task<PinTask?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(PinTask task, CancellationToken cancellationToken = default);
    Task SaveNewAtTopAsync(PinTask task, CancellationToken cancellationToken = default);
    Task SaveOrderAsync(IReadOnlyList<PinTask> tasks, CancellationToken cancellationToken = default);
    Task<long> GetNextTopSortOrderAsync(CancellationToken cancellationToken = default);
}
