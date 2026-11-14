using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.Services;

/// <summary>
/// An in-app clipboard for duplicating content. Copies are detached snapshots and
/// can only be pasted into the same account and data location that copied them.
/// It never stores existing entity IDs, completions, or history in the new model.
/// </summary>
public sealed class ContentClipboard
{
    private sealed record Entry<T>(T Model, long UserId, DataLocation Location);

    private Entry<TaskModel>? _task;
    private Entry<NoteModel>? _note;
    private Entry<HabitModel>? _habit;

    public bool CanPasteTask(long userId, DataLocation location) => Matches(_task, userId, location);
    public bool CanPasteNote(long userId, DataLocation location) => Matches(_note, userId, location);
    public bool CanPasteHabit(long userId, DataLocation location) => Matches(_habit, userId, location);

    private static bool Matches<T>(Entry<T>? entry, long userId, DataLocation location)
        => entry is not null && entry.UserId == userId && entry.Location == location;

    public void CopyTask(TaskModel source, long userId, DataLocation location)
        => _task = new(CloneTask(source), userId, location);

    public void CopyNote(NoteModel source, long userId, DataLocation location)
        => _note = new(CloneNote(source), userId, location);

    public void CopyHabit(HabitModel source, long userId, DataLocation location)
        => _habit = new(CloneHabit(source), userId, location);

    public TaskModel? PasteTask(long userId, DataLocation location)
        => CanPasteTask(userId, location) ? CloneTask(_task!.Model) : null;

    public NoteModel? PasteNote(long userId, DataLocation location)
        => CanPasteNote(userId, location) ? CloneNote(_note!.Model) : null;

    public HabitModel? PasteHabit(long userId, DataLocation location)
        => CanPasteHabit(userId, location) ? CloneHabit(_habit!.Model) : null;

    public static TaskModel CloneTask(TaskModel source) => new()
    {
        Title = source.Title,
        Priority = source.Priority,
        Color = source.Color,
        CategoryId = source.CategoryId,
        Duration = source.Duration,
        PlannedAt = source.PlannedAt,
        Items = CloneItems(source.Items)
        // StartedAt / CompletedAt deliberately not copied.
    };

    public static NoteModel CloneNote(NoteModel source) => new()
    {
        Title = source.Title,
        Content = source.Content,
        Priority = source.Priority,
        Color = source.Color,
        CategoryId = source.CategoryId
    };

    public static HabitModel CloneHabit(HabitModel source) => new()
    {
        Title = source.Title,
        Priority = source.Priority,
        Color = source.Color,
        CategoryId = source.CategoryId,
        RepeatCount = source.RepeatCount,
        RepeatInterval = source.RepeatInterval,
        RepeatPeriod = source.RepeatPeriod,
        DisplayMetric = source.DisplayMetric,
        TargetQuantity = source.TargetQuantity,
        Duration = source.Duration,
        StartAt = source.StartAt,
        Items = CloneItems(source.Items)
        // TimesDone / LastTimeDoneAt are intentionally not copied.
    };

    private static List<ItemModel>? CloneItems(List<ItemModel>? source)
        => source?.Select(item => new ItemModel { Title = item.Title }).ToList();
}
