using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.Services;

namespace OpenHabitTracker.UnitTests.Services;

[TestFixture]
public class ClosedAppReminderPlannerTests
{
    [Test]
    public void DueReminderFiresOnceInsideScanWindow()
    {
        DateTime now = new(2026, 11, 14, 13, 15, 0);
        TaskModel task = new() { Id = 4, PlannedAt = now.AddMinutes(10) };
        SettingsModel settings = SettingsModel.GetDefaultSettings(1);
        settings.NotificationTime = null;
        settings.NotificationLeadMinutes = 10;

        var due = ClosedAppReminderPlanner.Due([task], [], settings, now.AddMinutes(-1), now);
        Assert.That(due, Has.Count.EqualTo(1));
        Assert.That(due[0].TaskId, Is.EqualTo(4));
        Assert.That(ClosedAppReminderPlanner.Due([task], [], settings, now, now.AddMinutes(1)), Is.Empty);
    }

    [Test]
    public void PerTaskLeadOverrideIsHonored()
    {
        DateTime now = new(2026, 11, 14, 13, 0, 0);
        TaskModel task = new() { Id = 1, PlannedAt = now.AddMinutes(15) };
        SettingsModel settings = SettingsModel.GetDefaultSettings(1);
        settings.NotificationLeadMinutes = null;

        var due = ClosedAppReminderPlanner.Due([task], [], settings, now.AddSeconds(-30), now, _ => 15);
        Assert.That(due, Has.Count.EqualTo(1));
    }
}
