using System.Text.Json;
using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Core.Storage;

/// <summary>
/// Dependency-free durable queue: one JSON file per event in a directory, named
/// <c>{ticks}_{id}.json</c> so a directory listing is already oldest-first. Used by
/// the desktop/server SDKs (no native SQLite needed). Writes go to a temp file
/// and are then renamed, so a crash mid-write never leaves a half-written event.
/// </summary>
public sealed class FileEventStore : IEventStore
{
    private readonly string _directory;

    public FileEventStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task SaveAsync(IntelligenceEvent intelligenceEvent)
    {
        await DeleteAsync(intelligenceEvent.Id); // INSERT OR REPLACE semantics

        var name = $"{intelligenceEvent.Timestamp.Ticks:D20}_{intelligenceEvent.Id:N}.json";
        var temp = Path.Combine(_directory, name + ".tmp");
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(intelligenceEvent)).ConfigureAwait(false);
        File.Move(temp, Path.Combine(_directory, name), overwrite: true);
    }

    public async Task<IReadOnlyList<IntelligenceEvent>> GetPendingAsync(int max = 50)
    {
        var result = new List<IntelligenceEvent>();
        foreach (var file in Files().Take(max))
        {
            try
            {
                var e = JsonSerializer.Deserialize<IntelligenceEvent>(await File.ReadAllTextAsync(file).ConfigureAwait(false));
                if (e is not null)
                    result.Add(e);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // Corrupt or vanished entry: drop it so the queue can't get stuck.
                TryDelete(file);
            }
        }
        return result;
    }

    public Task DeleteAsync(Guid id)
    {
        foreach (var file in Directory.EnumerateFiles(_directory, $"*_{id:N}.json"))
            TryDelete(file);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync() => Task.FromResult(Files().Count());

    private IEnumerable<string> Files()
        => Directory.EnumerateFiles(_directory, "*.json").Order(StringComparer.Ordinal);

    private static void TryDelete(string file)
    {
        try { File.Delete(file); } catch (IOException) { }
    }
}

/// <summary>
/// Non-durable queue for hosts without a writable file system (e.g. Blazor
/// WebAssembly): store-and-forward still smooths over transient network errors
/// while the app runs, but pending events are lost when it closes.
/// </summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly List<IntelligenceEvent> _events = new();
    private readonly int _capacity;

    public InMemoryEventStore(int capacity = 500) => _capacity = Math.Max(1, capacity);

    public Task SaveAsync(IntelligenceEvent intelligenceEvent)
    {
        lock (_events)
        {
            _events.RemoveAll(e => e.Id == intelligenceEvent.Id);
            _events.Add(intelligenceEvent);
            if (_events.Count > _capacity)
                _events.RemoveAt(0); // drop the oldest rather than grow unbounded
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IntelligenceEvent>> GetPendingAsync(int max = 50)
    {
        lock (_events)
            return Task.FromResult<IReadOnlyList<IntelligenceEvent>>(_events.OrderBy(e => e.Timestamp).Take(max).ToList());
    }

    public Task DeleteAsync(Guid id)
    {
        lock (_events)
            _events.RemoveAll(e => e.Id == id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync()
    {
        lock (_events)
            return Task.FromResult(_events.Count);
    }
}

/// <summary>Screenshot store for hosts that don't capture screens.</summary>
public sealed class NullScreenshotStore : IScreenshotStore
{
    public Task SaveAsync(Guid eventId, byte[] jpeg) => Task.CompletedTask;

    public Task<byte[]?> GetAsync(Guid eventId) => Task.FromResult<byte[]?>(null);

    public Task DeleteAsync(Guid eventId) => Task.CompletedTask;
}
