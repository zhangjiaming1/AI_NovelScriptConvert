using System.Text.Json;
using System.Text.Json.Serialization;
using TodoFlow.Models;

namespace TodoFlow.Infrastructure;

public sealed class JsonTodoRepository : ITodoRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonTodoRepository()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TodoFlow",
            "todos.json"))
    {
    }

    public JsonTodoRepository(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    public async Task<IReadOnlyList<TodoItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return Array.Empty<TodoItem>();
        }

        await using var stream = File.OpenRead(FilePath);
        var items = await JsonSerializer.DeserializeAsync<List<TodoItem>>(
            stream,
            SerializerOptions,
            cancellationToken);

        return items ?? new List<TodoItem>();

    }

    public async Task SaveAsync(
        IReadOnlyCollection<TodoItem> items,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var directory = Path.GetDirectoryName(FilePath)
                ?? throw new InvalidOperationException("The data file path has no parent directory.");

            Directory.CreateDirectory(directory);

            var temporaryPath = FilePath + ".tmp";

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    items,
                    SerializerOptions,
                    cancellationToken);

                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, FilePath, true);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}