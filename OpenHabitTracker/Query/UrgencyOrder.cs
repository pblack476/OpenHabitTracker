using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.Query;

/// <summary>
/// Tasks: overdue, today, within three days, later, unscheduled, completed.
/// Higher priority breaks ties within each urgency bucket.
/// Recurring tasks: most overdue relative to their expected interval first.
/// </summary>
public static class UrgencyOrder
{
    public enum TaskBucket { Overdue, Today, Soon, Later, Unscheduled, Completed }

    public static TaskBucket GetTaskBucket(TaskModel task, DateTime now)
    {
        if (task.CompletedAt is not null)
            return TaskBucket.Completed;

        if (task.PlannedAt is not DateTime date)
            return TaskBucket.Unscheduled;

        // Today's date-only tasks have a 23:59:59 sentinel, so they stay due today.
        if (date < now && (!task.IsPlannedDateOnly || date.Date < now.Date))
            return TaskBucket.Overdue;

        int days = (date.Date - now.Date).Days;
        return days <= 0 ? TaskBucket.Today : days <= 3 ? TaskBucket.Soon : TaskBucket.Later;
    }

    public static IEnumerable<TaskModel> OrderTasks(IEnumerable<TaskModel> tasks, DateTime now)
        => tasks.OrderBy(task => GetTaskBucket(task, now))
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.PlannedAt ?? DateTime.MaxValue)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase);

    public static IEnumerable<HabitModel> OrderHabits(IEnumerable<HabitModel> habits, Ratio ratio)
        => habits.OrderByDescending(habit => SafeRatio(habit.GetRatio(ratio)))
            .ThenByDescending(habit => habit.Priority)
            .ThenBy(habit => habit.Title, StringComparer.CurrentCultureIgnoreCase);

    private static double SafeRatio(double value) => double.IsFinite(value) ? value : 0;
}
