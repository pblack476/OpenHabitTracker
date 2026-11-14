using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.Services;

public static class ClosedAppReminderPlanner
{
    public static IReadOnlyList<ScheduledNotification> Due(
        IEnumerable<TaskModel> tasks,
        IEnumerable<HabitModel> habits,
        SettingsModel settings,
        DateTime since,
        DateTime now,
        Func<long, int?>? getLead = null)
    {
        if (now <= since) return [];
        return NotificationSchedule.Build(tasks, habits, settings, since, getLead)
            .Where(item => item.NotifyAt > since && item.NotifyAt <= now)
            .OrderBy(item => item.NotifyAt)
            .ToArray();
    }
}
