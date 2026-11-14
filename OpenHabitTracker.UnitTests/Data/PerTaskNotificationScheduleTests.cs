using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.UnitTests.Data;

[TestFixture]
public class PerTaskNotificationScheduleTests
{
    [Test]
    public void PerTaskOverride_CanEnableWhenGlobalDefaultIsOff()
    {
        DateTime now = new(2026, 11, 15, 8, 0, 0);
        TaskModel task = new() { Id = 73, Title = "Call", PlannedAt = now.AddHours(2) };
        SettingsModel settings = SettingsModel.GetDefaultSettings(1);
        settings.NotificationLeadMinutes = null;
        settings.NotificationTime = null;

        List<ScheduledNotification> notifications = NotificationSchedule.Build([task], [], settings, now,
            taskId => taskId == 73 ? 15 : null);

        Assert.That(notifications, Has.Count.EqualTo(1));
        Assert.That(notifications[0].NotifyAt, Is.EqualTo(now.AddHours(1).AddMinutes(45)));
    }

    [Test]
    public void PerTaskOverride_CanDisableGlobalReminder()
    {
        DateTime now = new(2026, 11, 15, 8, 0, 0);
        TaskModel task = new() { Id = 73, Title = "Call", PlannedAt = now.AddHours(2) };
        SettingsModel settings = SettingsModel.GetDefaultSettings(1);
        settings.NotificationLeadMinutes = 30;
        settings.NotificationTime = null;

        List<ScheduledNotification> notifications = NotificationSchedule.Build([task], [], settings, now, _ => null);
        Assert.That(notifications, Is.Empty);
    }
}
