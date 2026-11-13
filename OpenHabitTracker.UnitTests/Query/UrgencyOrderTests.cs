using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.Query;

namespace OpenHabitTracker.UnitTests.Query;

[TestFixture]
public class UrgencyOrderTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 15, 0, 0);

    [Test]
    public void SortTasks_OverdueThenTodayThenSoonThenUnscheduledThenCompleted()
    {
        TaskModel[] models =
        [
            new() { Title = "No date" },
            new() { Title = "Later", PlannedAt = Now.AddDays(8) },
            new() { Title = "Today", PlannedAt = Now.AddHours(1) },
            new() { Title = "Overdue", PlannedAt = Now.AddHours(-1) },
            new() { Title = "Completed", CompletedAt = Now },
            new() { Title = "Soon", PlannedAt = Now.AddDays(2) }
        ];

        Assert.That(UrgencyOrder.OrderTasks(models, Now).Select(x => x.Title),
            Is.EqualTo(new[] { "Overdue", "Today", "Soon", "Later", "No date", "Completed" }));
    }

    [Test]
    public void DateOnlyToday_IsNotOverdue()
    {
        TaskModel allDay = new() { PlannedAt = Now.Date.AddHours(23).AddMinutes(59).AddSeconds(59) };
        Assert.That(UrgencyOrder.GetTaskBucket(allDay, Now), Is.EqualTo(UrgencyOrder.TaskBucket.Today));
    }

    [Test]
    public void HigherPriorityWinsWithinTheSameUrgencyBucket()
    {
        TaskModel low = new() { Priority = Priority.Low, PlannedAt = Now.AddHours(2) };
        TaskModel high = new() { Priority = Priority.High, PlannedAt = Now.AddHours(3) };
        Assert.That(UrgencyOrder.OrderTasks([low, high], Now).First(), Is.SameAs(high));
    }

    [Test]
    public void RecurringTaskUrgency_FollowsRatioOfElapsedToDesiredInterval()
    {
        HabitModel recent = new() { CreatedAt = DateTime.Now.AddMinutes(-5), RepeatInterval = 1 };
        HabitModel overdue = new() { CreatedAt = DateTime.Now.AddDays(-5), RepeatInterval = 1 };
        Assert.That(UrgencyOrder.OrderHabits([recent, overdue], Ratio.ElapsedToDesired).First(), Is.SameAs(overdue));
    }
}
