using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.Query;

namespace OpenHabitTracker.UnitTests.Query;

[TestFixture]
public class TodayAgendaTests
{
    [Test]
    public void ListsExcludeCompletedAndDeletedTasksAndKeepDateOnlyToday()
    {
        DateTime now = new(2026, 11, 14, 12, 0, 0);
        TaskModel late = new() { Title = "Late", PlannedAt = now.AddHours(-1) };
        TaskModel today = new() { Title = "Today", PlannedAt = now.Date.AddHours(23).AddMinutes(59).AddSeconds(59) };
        TaskModel upcoming = new() { Title = "Upcoming", PlannedAt = now.AddDays(2) };
        TaskModel done = new() { Title = "Done", PlannedAt = now.AddDays(-1), CompletedAt = now };
        TaskModel deleted = new() { Title = "Deleted", PlannedAt = now.AddHours(-1), IsDeleted = true };
        TaskModel[] tasks = [late, today, upcoming, done, deleted];

        Assert.Multiple(() =>
        {
            Assert.That(TodayAgenda.Overdue(tasks, now).Select(t => t.Title), Is.EqualTo(new[] { "Late" }));
            Assert.That(TodayAgenda.Today(tasks, now).Select(t => t.Title), Is.EqualTo(new[] { "Today" }));
            Assert.That(TodayAgenda.Upcoming(tasks, now).Select(t => t.Title), Is.EqualTo(new[] { "Upcoming" }));
        });
    }

    [Test]
    public void RecurringDueOnlyShowsItemsAtEightyPercentOrMore()
    {
        HabitModel recent = new() { CreatedAt = DateTime.Now, RepeatInterval = 1, RepeatPeriod = Period.Day };
        HabitModel overdue = new() { CreatedAt = DateTime.Now.AddDays(-3), RepeatInterval = 1, RepeatPeriod = Period.Day };
        Assert.That(TodayAgenda.RecurringDue([recent, overdue], Ratio.ElapsedToDesired), Is.EqualTo(new[] { overdue }));
    }
}
