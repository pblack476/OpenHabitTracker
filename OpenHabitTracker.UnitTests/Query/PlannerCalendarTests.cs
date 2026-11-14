using OpenHabitTracker.Query;

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
}
