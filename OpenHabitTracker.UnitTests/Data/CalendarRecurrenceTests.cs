using OpenHabitTracker.Data;

namespace OpenHabitTracker.UnitTests.Data;

[TestFixture]
public class CalendarRecurrenceTests
{
    [Test]
    public void WeeklyRuleHonorsSelectedWeekdaysAndStartDate()
    {
        CalendarRecurrenceRule rule = new() { Kind = CalendarScheduleKind.Weekly, Weekdays = [DayOfWeek.Monday, DayOfWeek.Friday] };
        Assert.Multiple(() =>
        {
            Assert.That(CalendarRecurrence.IsDue(rule, new DateTime(2026, 11, 16), new DateTime(2026, 11, 14)), Is.True);
            Assert.That(CalendarRecurrence.IsDue(rule, new DateTime(2026, 11, 17)), Is.False);
            Assert.That(CalendarRecurrence.IsDue(rule, new DateTime(2026, 11, 13), new DateTime(2026, 11, 14)), Is.False);
        });
    }

    [Test]
    public void MonthlyThirtyFirstUsesEndOfShorterMonth()
    {
        CalendarRecurrenceRule rule = new() { Kind = CalendarScheduleKind.MonthlyDay, DayOfMonth = 31 };
        Assert.That(CalendarRecurrence.IsDue(rule, new DateTime(2027, 2, 28)), Is.True);
    }

    [Test]
    public void FirstAndLastBusinessDaysIgnoreWeekends()
    {
        CalendarRecurrenceRule first = new() { Kind = CalendarScheduleKind.FirstBusinessDay };
        CalendarRecurrenceRule last = new() { Kind = CalendarScheduleKind.LastBusinessDay };
        Assert.Multiple(() =>
        {
            Assert.That(CalendarRecurrence.IsDue(first, new DateTime(2026, 11, 2)), Is.True);
            Assert.That(CalendarRecurrence.IsDue(last, new DateTime(2026, 10, 30)), Is.True);
            Assert.That(CalendarRecurrence.IsDue(last, new DateTime(2026, 10, 31)), Is.False);
        });
    }

    [Test]
    public void SkipAndPostponeAreDateSpecific()
    {
        CalendarRecurrenceRule rule = new()
        {
            Kind = CalendarScheduleKind.Weekly,
            Weekdays = [DayOfWeek.Monday],
            SkippedDates = [CalendarRecurrenceRule.Key(new DateTime(2026, 11, 23))],
            PostponedDates = { [CalendarRecurrenceRule.Key(new DateTime(2026, 11, 16))] = CalendarRecurrenceRule.Key(new DateTime(2026, 11, 17)) }
        };
        Assert.Multiple(() =>
        {
            Assert.That(CalendarRecurrence.IsDue(rule, new DateTime(2026, 11, 16)), Is.False);
            Assert.That(CalendarRecurrence.IsDue(rule, new DateTime(2026, 11, 17)), Is.True);
            Assert.That(CalendarRecurrence.IsDue(rule, new DateTime(2026, 11, 23)), Is.False);
        });
    }
}
