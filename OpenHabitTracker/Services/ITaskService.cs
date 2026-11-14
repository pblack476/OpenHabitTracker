using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.Services;

public interface ITaskService
{
    IReadOnlyCollection<TaskModel>? Tasks { get; }
    TaskModel? SelectedTask { get; set; }
    TaskModel? NewTask { get; set; }
    IEnumerable<TaskModel> GetTasks();
    Task Initialize();
    void SetSelectedTask(long? id);
    Task AddTask();
    Task<int> AddTasksFromLines(string text);
    Task UpdateTask();
    Task QuickUpdate(TaskModel task, string title, OpenHabitTracker.Data.Priority priority, DateTime? plannedAt);
    Task Start(TaskModel task);
    Task SetStartTime(TaskModel task, DateTime startedAt);
    Task MarkAsDone(TaskModel task);
    Task Reschedule(TaskModel task, DateTime newDay);
    Task ChangeCategory(TaskModel task, long categoryId);
    Task DeleteTask(TaskModel task);
}
