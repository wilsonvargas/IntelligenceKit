using IntelligenceKit.Core.Configuration;
using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Core.Diagnostics;

/// <summary>
/// Thread-safe fixed-capacity breadcrumb buffer. Capacity comes from
/// <see cref="IntelligenceOptions.BreadcrumbCapacity"/>.
/// </summary>
public class BreadcrumbBuffer : IBreadcrumbBuffer
{
    private readonly int _capacity;
    private readonly LinkedList<Breadcrumb> _items = new();
    private readonly object _lock = new();

    public BreadcrumbBuffer(IntelligenceOptions options)
    {
        _capacity = Math.Max(1, options.BreadcrumbCapacity);
    }

    public void Add(Breadcrumb breadcrumb)
    {
        lock (_lock)
        {
            _items.AddLast(breadcrumb);
            while (_items.Count > _capacity)
                _items.RemoveFirst();
        }
    }

    public IReadOnlyList<Breadcrumb> Snapshot()
    {
        lock (_lock)
        {
            return _items.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }
}
