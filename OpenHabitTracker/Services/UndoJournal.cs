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

    public event Action? Changed;

    public int Count => _entries.Count;
    public string? LastLabel => _entries.Count > 0 ? _entries[^1].Label : null;
    public bool IsApplying => _applying;

    public void Record(string label, Func<Task> revert)
    {
        if (_applying)
            return;
        _entries.Add(new(label, _state.User.Id, _state.DataLocation, revert));
        if (_entries.Count > 25)
            _entries.RemoveAt(0);
        Changed?.Invoke();
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
