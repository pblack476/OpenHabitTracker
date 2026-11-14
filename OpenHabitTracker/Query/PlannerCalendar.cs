namespace OpenHabitTracker.Query;

public static class PlannerCalendar
{
    public static DateTime StartOfWeek(DateTime day, DayOfWeek firstDay)
    {
        int offset = ((int)day.DayOfWeek - (int)firstDay + 7) % 7;
        return day.Date.AddDays(-offset);
    }

    public static IReadOnlyList<DateTime> MonthGrid(DateTime month, DayOfWeek firstDay)
    {
        DateTime start = StartOfWeek(new DateTime(month.Year, month.Month, 1), firstDay);
        return Enumerable.Range(0, 42).Select(index => start.AddDays(index)).ToArray();
    }

    public static IReadOnlyList<DateTime> WeekGrid(DateTime day, DayOfWeek firstDay)
    {
        DateTime start = StartOfWeek(day, firstDay);
        return Enumerable.Range(0, 7).Select(index => start.AddDays(index)).ToArray();
    }
}
