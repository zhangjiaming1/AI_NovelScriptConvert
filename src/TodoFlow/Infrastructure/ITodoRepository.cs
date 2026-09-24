using TodoFlow.Models;

namespace TodoFlow.Infrastructure;

public interface ITodoRepository
{
    string FilePath { get; }

    Task<IReadOnlyList<TodoItem>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyCollection<TodoItem> items, CancellationToken cancellationToken = default);
}