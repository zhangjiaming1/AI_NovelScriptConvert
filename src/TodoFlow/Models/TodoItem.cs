namespace TodoFlow.Models;

public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTimeOffset? DueDate { get; set; }

    public TodoPriority Priority { get; set; } = TodoPriority.Normal;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}