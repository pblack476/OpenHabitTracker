using OpenHabitTracker.App;
using OpenHabitTracker.Data;

namespace OpenHabitTracker.Services;

/// <summary>
/// Session-only undo. Each record belongs to the exact user and data location
/// that performed the action, so switching accounts cannot replay stale actions.
/// </summary>
public sealed class UndoJournal(ClientState state)
{
    private sealed record Entry(string Label, long UserId, DataLocation Location, Func<Task> Revert);

    private readonly ClientState _state = state;
    private readonly List<Entry> _entries = [];
    private bool _applying;
    private List<Entry>? _batch;

    public event Action? Changed;

    public int Count => _entries.Count;
    public string? LastLabel => _entries.Count > 0 ? _entries[^1].Label : null;
    public bool IsApplying => _applying;

    public void Record(string label, Func<Task> revert)
    {
        if (_applying)
            return;
        Entry entry = new(label, _state.User.Id, _state.DataLocation, revert);
        if (_batch is not null)
        {
            _batch.Add(entry);
            return;
        }

        _entries.Add(entry);
        if (_entries.Count > 25)
            _entries.RemoveAt(0);
        Changed?.Invoke();
    }

    /// <summary>
    /// Groups multiple successful operations into one Undo. If an operation fails
    /// halfway through, the completed portion is still reversible.
    /// </summary>
    public async Task RunBatch(string label, Func<Task> action)
    {
        if (_batch is not null || _applying)
            throw new InvalidOperationException("Nested undo groups are not supported.");

        _batch = [];
        try
        {
            await action();
        }
        finally
        {
            List<Entry> completed = _batch;
            _batch = null;
            if (completed.Count > 0)
            {
                Record(label, async () =>
                {
                    for (int i = completed.Count - 1; i >= 0; i--)
                        await completed[i].Revert();
                });
            }
        }
    }

    public async Task<bool> UndoLast()
    {
        if (_applying || _entries.Count == 0)
            return false;

        Entry entry = _entries[^1];
        if (entry.UserId != _state.User.Id || entry.Location != _state.DataLocation)
        {
            _entries.Clear();
            Changed?.Invoke();
            return false;
        }

        _entries.RemoveAt(_entries.Count - 1);
        _applying = true;
        try
        {
            await entry.Revert();
        }
        catch
        {
            _entries.Add(entry);
            throw;
        }
        finally
        {
            _applying = false;
            Changed?.Invoke();
        }
        return true;
    }

    public void Clear()
    {
        if (_entries.Count == 0)
            return;
        _entries.Clear();
        Changed?.Invoke();
    }
}
