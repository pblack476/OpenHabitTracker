using OpenHabitTracker.Query;
using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.UnitTests.Query;

[TestFixture]
public class PlannerCalendarTests
{
    [Test]
    public void MonthGrid_AlwaysShowsSixWeeksFromConfiguredWeekday()
    {
        IReadOnlyList<DateTime> grid = PlannerCalendar.MonthGrid(new DateTime(2026, 11, 14), DayOfWeek.Monday);
        Assert.Multiple(() =>
        {
            Assert.That(grid, Has.Count.EqualTo(42));
            Assert.That(grid.First(), Is.EqualTo(new DateTime(2026, 10, 26)));
            Assert.That(grid.Last(), Is.EqualTo(new DateTime(2026, 12, 6)));
        });
    }

    [Test]
    public void WeekGrid_FollowsSelectedFirstDay()
    {
        IReadOnlyList<DateTime> grid = PlannerCalendar.WeekGrid(new DateTime(2026, 11, 14), DayOfWeek.Sunday);
        Assert.Multiple(() =>
        {
            Assert.That(grid, Has.Count.EqualTo(7));
            Assert.That(grid[0], Is.EqualTo(new DateTime(2026, 11, 8)));
            Assert.That(grid[^1], Is.EqualTo(new DateTime(2026, 11, 14)));
        });
    }

    [Test]
    public void ExistingIntervalRecurringTasks_AppearWithoutExplicitCalendarRule()
    {
        DateTime anchor = new(2026, 11, 2); // Monday
        HabitModel daily = new() { Id = 1, Title = "Daily", CreatedAt = anchor, RepeatPeriod = Period.Day, RepeatInterval = 1 };
        HabitModel weekly = new() { Id = 2, Title = "Weekly", CreatedAt = anchor, RepeatPeriod = Period.Week, RepeatInterval = 1 };
        HabitModel fortnightly = new() { Id = 3, Title = "Every two weeks", CreatedAt = anchor, RepeatPeriod = Period.Week, RepeatInterval = 2 };

        Assert.Multiple(() =>
        {
            Assert.That(PlannerCalendar.RecurringForDay([daily, weekly, fortnightly], anchor, _ => null), Has.Count.EqualTo(3));
            Assert.That(PlannerCalendar.RecurringForDay([daily, weekly, fortnightly], anchor.AddDays(7), _ => null).Select(h => h.Id),
                Is.EquivalentTo(new[] { 1L, 2L }));
            Assert.That(PlannerCalendar.RecurringForDay([daily, weekly, fortnightly], anchor.AddDays(14), _ => null), Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void MultiCompletionInterval_SpreadsOccurrencesWithoutDuplicatingRows()
    {
        HabitModel habit = new() { CreatedAt = new DateTime(2026, 11, 2), RepeatPeriod = Period.Week, RepeatInterval = 1, RepeatCount = 3 };
        DateTime anchor = habit.CreatedAt;
        Assert.Multiple(() =>
        {
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, anchor), Is.True);
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, anchor.AddDays(1)), Is.False);
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, anchor.AddDays(2)), Is.True);
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, anchor.AddDays(4)), Is.True);
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, anchor.AddDays(7)), Is.True);
        });
    }

    [Test]
    public void MonthlyAnchorAtMonthEnd_ProjectsIntoShortMonths()
    {
        HabitModel habit = new() { CreatedAt = new DateTime(2026, 1, 31), RepeatPeriod = Period.Month, RepeatInterval = 1 };
        Assert.Multiple(() =>
        {
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, new DateTime(2026, 2, 28)), Is.True);
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, new DateTime(2026, 3, 31)), Is.True);
            Assert.That(PlannerCalendar.IsLegacyProjectedOccurrence(habit, new DateTime(2026, 2, 27)), Is.False);
        });
    }

    [Test]
    public void ExplicitRuleWinsAndCompletedHistoryIsNotHidden()
    {
        DateTime anchor = new(2026, 11, 2); // Monday
        HabitModel habit = new()
        {
            CreatedAt = anchor,
            RepeatPeriod = Period.Day,
            RepeatInterval = 1,
            TimesDone = [new TimeModel { StartedAt = anchor.AddDays(1), CompletedAt = anchor.AddDays(1).AddHours(1) }]
        };
        CalendarRecurrenceRule wednesday = new() { Kind = CalendarScheduleKind.Weekly, Weekdays = [DayOfWeek.Wednesday] };

        Assert.Multiple(() =>
        {
            Assert.That(PlannerCalendar.IsRecurringOn(habit, anchor, wednesday), Is.False);
            Assert.That(PlannerCalendar.IsRecurringOn(habit, anchor.AddDays(1), wednesday), Is.True); // completed Tuesday
            Assert.That(PlannerCalendar.IsRecurringOn(habit, anchor.AddDays(2), wednesday), Is.True);
        });
    }

    [Test]
    public void AllScheduledTasksRemainVisibleOnTheirOriginalDaysIncludingCompleted()
    {
        DateTime selected = new(2026, 11, 14);
        TaskModel open = new() { Id = 1, Title = "New", PlannedAt = selected.AddHours(10) };
        TaskModel completed = new() { Id = 2, Title = "Done", PlannedAt = selected, CompletedAt = selected.AddHours(11) };
        TaskModel noDate = new() { Id = 3, Title = "No date" };
        TaskModel deleted = new() { Id = 4, Title = "Deleted", PlannedAt = selected, IsDeleted = true };

        Assert.Multiple(() =>
        {
            Assert.That(PlannerCalendar.TasksForDay([open, completed, noDate, deleted], selected).Select(t => t.Id),
                Is.EquivalentTo(new[] { 1L, 2L }));
            Assert.That(PlannerCalendar.Unscheduled([open, completed, noDate, deleted]).Select(t => t.Id),
                Is.EqualTo(new[] { 3L }));
        });
    }

}
