using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.Query;

/// <summary>All lists use the current time when rendered; completed or deleted
/// items are excluded without changing how they are stored.</summary>
public static class TodayAgenda
{
    public static IEnumerable<TaskModel> Overdue(IEnumerable<TaskModel> tasks, DateTime now)
        => UrgencyOrder.OrderTasks(tasks.Where(t => !t.IsDeleted && t.CompletedAt is null &&
            UrgencyOrder.GetTaskBucket(t, now) == UrgencyOrder.TaskBucket.Overdue), now);

    public static IEnumerable<TaskModel> Today(IEnumerable<TaskModel> tasks, DateTime now)
        => UrgencyOrder.OrderTasks(tasks.Where(t => !t.IsDeleted && t.CompletedAt is null &&
            UrgencyOrder.GetTaskBucket(t, now) == UrgencyOrder.TaskBucket.Today), now);

    public static IEnumerable<TaskModel> Upcoming(IEnumerable<TaskModel> tasks, DateTime now)
        => UrgencyOrder.OrderTasks(tasks.Where(t => !t.IsDeleted && t.CompletedAt is null &&
            UrgencyOrder.GetTaskBucket(t, now) == UrgencyOrder.TaskBucket.Soon), now);

    public static IEnumerable<HabitModel> RecurringDue(IEnumerable<HabitModel> habits, Ratio ratio)
        => UrgencyOrder.OrderHabits(habits.Where(h => !h.IsDeleted &&
            double.IsFinite(h.GetRatio(ratio)) && h.GetRatio(ratio) >= 80), ratio);
}
