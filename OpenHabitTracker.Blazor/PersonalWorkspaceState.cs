using System.Text.Json;
using OpenHabitTracker.App;
using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.Services;

namespace OpenHabitTracker.Blazor;

/// <summary>
/// Device-local QoL metadata, intentionally separate from the application
/// SQLite schema. Account-scoped to prevent cross-user pin/template leakage.
/// Existing notes, tasks and history are never stored or modified here.
/// </summary>
public sealed class PersonalWorkspaceState(IJsInterop jsInterop, ClientState state)
{
    public sealed class SavedView
    {
        public string Name { get; set; } = "";
        public string? SearchTerm { get; set; }
        public bool HideCompleted { get; set; }
        public long? CategoryId { get; set; }
        public Priority? Priority { get; set; }
        public Dictionary<ContentType, Sort> SortBy { get; set; } = [];
    }

    public sealed class TaskTemplate
    {
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public List<string> Items { get; set; } = [];
        public Priority Priority { get; set; }
        public long CategoryId { get; set; }
        public bool Recurring { get; set; }
        public int RepeatCount { get; set; } = 1;
        public int RepeatInterval { get; set; } = 1;
        public Period RepeatPeriod { get; set; } = Period.Day;
    }

    public sealed class WorkspaceData
    {
        public HashSet<string> Pinned { get; set; } = [];
        public HashSet<string> ManualKinds { get; set; } = [];
        public Dictionary<string, List<long>> Orders { get; set; } = [];
        public List<SavedView> Views { get; set; } = [];
        public List<TaskTemplate> Templates { get; set; } = [];
    }

    private readonly IJsInterop _js = jsInterop;
    private readonly ClientState _state = state;
    private string? _scope;
    private WorkspaceData _data = new();

    private string CurrentScope => $"{_state.DataLocation}:{_state.User.Id}";
    public IReadOnlyList<SavedView> Views => _data.Views;
    public IReadOnlyList<TaskTemplate> Templates => _data.Templates;
    public bool IsLoaded => _scope == CurrentScope;

    public async Task Load()
    {
        string scope = CurrentScope;
        if (_scope == scope)
            return;

        string? raw = await _js.GetWorkspaceState(scope);
        try
        {
            _data = string.IsNullOrWhiteSpace(raw) ? new() :
                JsonSerializer.Deserialize<WorkspaceData>(raw) ?? new();
        }
        catch (JsonException)
        {
            // Do not overwrite invalid data automatically; users may recover
            // it externally before the next successful preference write.
            _data = new();
        }

        _scope = scope;
    }

    private async Task Save()
    {
        if (!IsLoaded)
            await Load();

        await _js.SetWorkspaceState(CurrentScope, JsonSerializer.Serialize(_data));
    }

    private static string PinKey(string kind, long id) => kind + ":" + id;

    public bool IsPinned(string kind, long id) => IsLoaded && _data.Pinned.Contains(PinKey(kind, id));

    public async Task TogglePinned(string kind, long id)
    {
        if (!IsLoaded)
            await Load();
        string key = PinKey(kind, id);
        if (!_data.Pinned.Add(key))
            _data.Pinned.Remove(key);
        await Save();
    }

    public bool IsManual(string kind) => IsLoaded && _data.ManualKinds.Contains(kind);

    public async Task ToggleManual(string kind)
    {
        if (!IsLoaded)
            await Load();
        if (!_data.ManualKinds.Add(kind))
            _data.ManualKinds.Remove(kind);
        await Save();
    }

    public IEnumerable<T> ApplyOrder<T>(string kind, IEnumerable<T> source, Func<T, long> id)
    {
        if (!IsLoaded)
            return source;

        IEnumerable<T> pinned = source.OrderByDescending(item => IsPinned(kind, id(item)));
        if (!IsManual(kind))
            return pinned;

        List<long> order = _data.Orders.TryGetValue(kind, out List<long>? existing) ? existing : [];
        return pinned.ThenBy(item =>
        {
            int index = order.IndexOf(id(item));
            return index >= 0 ? index : int.MaxValue;
        });
    }

    public async Task Move(string kind, long sourceId, long targetId, IEnumerable<long> currentIds)
    {
        if (!IsLoaded)
            await Load();
        if (sourceId == targetId)
            return;

        if (!_data.Orders.TryGetValue(kind, out List<long>? order))
        {
            order = [];
            _data.Orders[kind] = order;
        }

        foreach (long id in currentIds)
            if (!order.Contains(id))
                order.Add(id);

        if (!order.Remove(sourceId))
            return;

        int targetIndex = order.IndexOf(targetId);
        if (targetIndex < 0)
        {
            order.Add(sourceId);
            return;
        }

        order.Insert(targetIndex, sourceId);
        await Save();
    }

    public async Task SaveView(string name, SettingsModel settings, ISearchFilterService search)
    {
        if (!IsLoaded)
            await Load();

        string title = name.Trim();
        if (title.Length == 0 || title.Length > 48)
            throw new ArgumentException("View names must have 1 to 48 characters.");

        _data.Views.RemoveAll(view => string.Equals(view.Name, title, StringComparison.OrdinalIgnoreCase));
        if (_data.Views.Count >= 20)
            throw new InvalidOperationException("There can be at most 20 saved views.");

        _data.Views.Add(new SavedView
        {
            Name = title,
            SearchTerm = search.SearchTerm,
            HideCompleted = settings.HideCompletedTasks,
            CategoryId = settings.SelectedCategoryId,
            Priority = settings.SelectedPriority,
            SortBy = new(settings.SortBy)
        });
        await Save();
    }

    public bool ApplyView(string name, SettingsModel settings, ISearchFilterService search)
    {
        if (!IsLoaded)
            return false;

        SavedView? view = _data.Views.FirstOrDefault(v => v.Name == name);
        if (view is null)
            return false;

        search.SearchTerm = view.SearchTerm;
        settings.HideCompletedTasks = view.HideCompleted;
        settings.SelectedCategoryId = view.CategoryId;
        settings.SelectedPriority = view.Priority;
        foreach (KeyValuePair<ContentType, Sort> pair in view.SortBy)
            settings.SortBy[pair.Key] = pair.Value;
        return true;
    }

    public async Task RemoveView(string name)
    {
        if (!IsLoaded)
            await Load();
        _data.Views.RemoveAll(view => view.Name == name);
        await Save();
    }

    public async Task SaveTemplate(TaskTemplate template)
    {
        if (!IsLoaded)
            await Load();
        if (string.IsNullOrWhiteSpace(template.Name) || template.Name.Length > 60)
            throw new ArgumentException("Invalid template name.");

        _data.Templates.RemoveAll(x => x.Name == template.Name);
        _data.Templates.Add(template);
        await Save();
    }

    public async Task RemoveTemplate(string name)
    {
        if (!IsLoaded)
            await Load();
        _data.Templates.RemoveAll(x => x.Name == name);
        await Save();
    }
}
