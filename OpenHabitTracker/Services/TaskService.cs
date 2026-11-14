using OpenHabitTracker.App;
using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Entities;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.Query;

namespace OpenHabitTracker.Services;

public class TaskService(ClientState clientState, ISearchFilterService searchFilterService, IAppReview appReview, INotificationScheduler notificationScheduler, UndoJournal? undoJournal = null) : ITaskService
{
    private readonly ClientState _clientState = clientState;
    private readonly ISearchFilterService _searchFilterService = searchFilterService;
    private readonly IAppReview _appReview = appReview;
    private readonly INotificationScheduler _notificationScheduler = notificationScheduler;
    private readonly UndoJournal? _undo = undoJournal;

    public IReadOnlyCollection<TaskModel>? Tasks => _clientState.Tasks?.Values;

    public TaskModel? SelectedTask { get; set; }

    public TaskModel? NewTask { get; set; }

    public IEnumerable<TaskModel> GetTasks()
    {
        QueryParameters queryParameters = _searchFilterService.GetQueryParameters(_clientState.Settings);

        return Tasks!.FilterTasks(queryParameters);
    }

    public async Task Initialize()
    {
        await _clientState.LoadCategories();

        await _clientState.LoadTasks();
    }

    public void SetSelectedTask(long? id)
    {
        if (_clientState.Tasks is null)
            return;

        SelectedTask = id.HasValue && _clientState.Tasks.TryGetValue(id.Value, out TaskModel? task) ? task : null;

        if (SelectedTask is not null)
            NewTask = null;
    }

    public async Task AddTask()
    {
        if (_clientState.Tasks is null || NewTask is null)
            return;

        DateTime now = DateTime.Now;

        NewTask.CreatedAt = now;
        NewTask.UpdatedAt = now;

        TaskEntity task = NewTask.ToEntity();

        await _clientState.DataAccess.AddTask(task);

        NewTask.Id = task.Id;

        // New items copied from another task must be inserted with fresh IDs.
        await CopiedItemPersistence.SaveNewItems(_clientState, NewTask);

        _clientState.Tasks.Add(NewTask.Id, NewTask);

        if (NewTask.CategoryId != 0 && _clientState.Categories?.TryGetValue(NewTask.CategoryId, out CategoryModel? taskCategory) == true)
            taskCategory.Tasks.Add(NewTask);

        NewTask = null;

        await _appReview.RecordEngagement(EngagementKind.ContentCreated);

        await _notificationScheduler.Rebuild();
    }

    public async Task<int> AddTasksFromLines(string text)
    {
        IReadOnlyList<string> titles = BulkTextParser.Parse(text);
        if (titles.Count == 0)
            return 0;

        if (_clientState.Tasks is null)
            await Initialize();

        DateTime now = DateTime.Now;
        List<TaskModel> models = titles.Select(title => new TaskModel
        {
            Title = title,
            Color = "bg-info-subtle",
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();
        List<TaskEntity> entities = models.Select(model => model.ToEntity()).ToList();

        await _clientState.DataAccess.AddTasks(entities);

        for (int i = 0; i < models.Count; i++)
        {
            models[i].Id = entities[i].Id;
            _clientState.Tasks!.Add(models[i].Id, models[i]);
        }

        await _appReview.RecordEngagement(EngagementKind.ContentCreated);
        return models.Count;
    }

    public async Task UpdateTask()
    {
        // Escape saves the focused field and closes the task without waiting, and the close clears SelectedTask.
        if (Tasks is null || SelectedTask is not TaskModel selectedTask)
            return;

        if (await _clientState.DataAccess.GetTask(selectedTask.Id) is TaskEntity task)
        {
            selectedTask.CopyToEntity(task);

            await _clientState.DataAccess.UpdateTask(task);
            selectedTask.UpdatedAt = task.UpdatedAt;
        }

        await _notificationScheduler.Rebuild();
    }

    public async Task QuickUpdate(TaskModel task, string title, Priority priority, DateTime? plannedAt)
    {
        if (string.IsNullOrWhiteSpace(title) || task.IsDeleted)
            return;

        if (await _clientState.DataAccess.GetTask(task.Id) is not TaskEntity entity)
            return;

        string safeTitle = title.Trim();
        string previousTitle = task.Title;
        Priority previousPriority = task.Priority;
        DateTime? previousSchedule = task.PlannedAt;
        entity.Title = safeTitle;
        entity.Priority = priority;
        entity.PlannedAt = plannedAt;
        await _clientState.DataAccess.UpdateTask(entity);
        task.Title = safeTitle;
        task.Priority = priority;
        task.PlannedAt = plannedAt;
        task.UpdatedAt = entity.UpdatedAt;
        await _notificationScheduler.Rebuild();
        _undo?.Record("Edit task", () => QuickUpdate(task, previousTitle, previousPriority, previousSchedule));
    }

    public async Task Start(TaskModel task)
    {
        if (Tasks is null)
            return;

        DateTime now = DateTime.Now;

        task.StartedAt = now;
        task.CompletedAt = null;

        if (await _clientState.DataAccess.GetTask(task.Id) is TaskEntity taskEntity)
        {
            taskEntity.StartedAt = now;
            taskEntity.CompletedAt = null;

            await _clientState.DataAccess.UpdateTask(taskEntity);
            task.UpdatedAt = taskEntity.UpdatedAt;
        }
    }

    public async Task SetStartTime(TaskModel task, DateTime startedAt)
    {
        if (Tasks is null)
            return;

        if (task.StartedAt == null || task.CompletedAt != null)
            return;

        task.StartedAt = startedAt;

        if (await _clientState.DataAccess.GetTask(task.Id) is TaskEntity taskEntity)
        {
            taskEntity.StartedAt = startedAt;
            await _clientState.DataAccess.UpdateTask(taskEntity);
            task.UpdatedAt = taskEntity.UpdatedAt;
        }
    }

    public async Task MarkAsDone(TaskModel task)
    {
        if (Tasks is null)
            return;

        bool isCompleted = task.CompletedAt != null;
        DateTime? oldStarted = task.StartedAt;
        DateTime? oldCompleted = task.CompletedAt;
        Dictionary<long, DateTime?> oldItems = task.Items?.ToDictionary(item => item.Id, item => item.DoneAt) ?? new();

        DateTime now = DateTime.Now;
        DateTime? dateTime = isCompleted ? null : now;

        if (isCompleted)
        {
            task.StartedAt = null;
        }
        else if (task.StartedAt is null)
        {
            DateTime startedAt = task.Duration is TimeOnly duration ? now - duration.ToTimeSpan() : now;
            task.StartedAt = startedAt < now.Date ? now.Date : startedAt;
        }

        task.CompletedAt = dateTime;

        if (await _clientState.DataAccess.GetTask(task.Id) is TaskEntity taskEntity)
        {
            taskEntity.StartedAt = task.StartedAt;
            taskEntity.CompletedAt = task.CompletedAt;

            await _clientState.DataAccess.UpdateTask(taskEntity);
            task.UpdatedAt = taskEntity.UpdatedAt;
        }

        // MarkAsDone toggles - only the transition to done is an engagement event
        if (!isCompleted)
            await _appReview.RecordEngagement(EngagementKind.Completed);

        await _notificationScheduler.Rebuild();

        // Un-completing leaves the items as they are, because some were ticked by hand before the task was done.
        if (task.Items is null || isCompleted)
        {
            _undo?.Record("Task completion", () => RestoreCompletion(task, oldStarted, oldCompleted, oldItems));
            return;
        }

        foreach (ItemModel item in task.Items)
        {
            item.DoneAt = dateTime;

            if (await _clientState.DataAccess.GetItem(item.Id) is ItemEntity itemEntity)
            {
                itemEntity.DoneAt = dateTime;

                await _clientState.DataAccess.UpdateItem(itemEntity);
            }
        }

        _undo?.Record("Task completion", () => RestoreCompletion(task, oldStarted, oldCompleted, oldItems));
    }

    private async Task RestoreCompletion(TaskModel task, DateTime? oldStarted, DateTime? oldCompleted, Dictionary<long, DateTime?> oldItems)
    {
        if (await _clientState.DataAccess.GetTask(task.Id) is not TaskEntity entity)
            return;

        entity.StartedAt = oldStarted;
        entity.CompletedAt = oldCompleted;
        await _clientState.DataAccess.UpdateTask(entity);
        task.StartedAt = oldStarted;
        task.CompletedAt = oldCompleted;
        task.UpdatedAt = entity.UpdatedAt;

        foreach (KeyValuePair<long, DateTime?> old in oldItems)
        {
            if (task.Items?.FirstOrDefault(item => item.Id == old.Key) is ItemModel model)
                model.DoneAt = old.Value;

            if (await _clientState.DataAccess.GetItem(old.Key) is ItemEntity item)
            {
                item.DoneAt = old.Value;
                await _clientState.DataAccess.UpdateItem(item);
            }
        }

        await _notificationScheduler.Rebuild();
    }

    /// <summary>
    /// Move a planned task to another day while preserving its existing clock
    /// time or date-only sentinel. The original task ID and checklist remain.
    /// </summary>
    public async Task Reschedule(TaskModel task, DateTime newDay)
    {
        if (_clientState.Tasks is null || task.IsDeleted || task.CompletedAt is not null)
            return;

        if (await _clientState.DataAccess.GetTask(task.Id) is not TaskEntity entity)
            return;

        DateTime? previousSchedule = task.PlannedAt;
        task.PlannedDate = newDay.Date;
        entity.PlannedAt = task.PlannedAt;
        await _clientState.DataAccess.UpdateTask(entity);
        task.UpdatedAt = entity.UpdatedAt;
        await _notificationScheduler.Rebuild();
        _undo?.Record("Reschedule task", () => RestoreSchedule(task, previousSchedule));
    }

    private async Task RestoreSchedule(TaskModel task, DateTime? plannedAt)
    {
        if (await _clientState.DataAccess.GetTask(task.Id) is not TaskEntity entity)
            return;
        entity.PlannedAt = plannedAt;
        await _clientState.DataAccess.UpdateTask(entity);
        task.PlannedAt = plannedAt;
        task.UpdatedAt = entity.UpdatedAt;
        await _notificationScheduler.Rebuild();
    }

    public async Task DeleteTask(TaskModel task)
    {
        if (_clientState.Tasks is null || task.IsDeleted)
            return;

        if (await _clientState.DataAccess.GetTask(task.Id) is not TaskEntity taskEntity)
            return;

        taskEntity.IsDeleted = true;
        await _clientState.DataAccess.UpdateTask(taskEntity);
        task.IsDeleted = true;
        task.UpdatedAt = taskEntity.UpdatedAt;
        _clientState.TrashedTasks?.Add(task);
        await _notificationScheduler.Rebuild();

        _undo?.Record("Delete task", () => RestoreDeletedTask(task));
    }

    private async Task RestoreDeletedTask(TaskModel task)
    {
        if (await _clientState.DataAccess.GetTask(task.Id) is not TaskEntity entity)
            return;

        entity.IsDeleted = false;
        await _clientState.DataAccess.UpdateTask(entity);
        task.IsDeleted = false;
        task.UpdatedAt = entity.UpdatedAt;
        _clientState.TrashedTasks?.Remove(task);
        await _notificationScheduler.Rebuild();
    }
}
