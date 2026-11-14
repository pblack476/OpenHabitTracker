namespace OpenHabitTracker.Data;

public enum CalendarScheduleKind
{
    None,
    Weekly,
    MonthlyDay,
    FirstBusinessDay,
    LastBusinessDay
}

/// <summary>
/// Additional optional calendar rule for a recurring task. The existing repeat
/// interval remains unchanged, so enabling/disabling a rule never rewrites the
/// original history or serialized habit model.
/// </summary>
public sealed class CalendarRecurrenceRule
{
    public CalendarScheduleKind Kind { get; set; }
    public List<DayOfWeek> Weekdays { get; set; } = [];
    public int DayOfMonth { get; set; } = 1;
    public HashSet<string> SkippedDates { get; set; } = [];
    public Dictionary<string, string> PostponedDates { get; set; } = [];

    public static string Key(DateTime date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

public static class CalendarRecurrence
{
    public static bool IsDue(CalendarRecurrenceRule? rule, DateTime date, DateTime? startDate = null)
    {
        if (rule is null || rule.Kind == CalendarScheduleKind.None)
            return false;

        date = date.Date;
        if (startDate is DateTime start && date < start.Date)
            return false;

        string key = CalendarRecurrenceRule.Key(date);
        if (rule.SkippedDates.Contains(key) || rule.PostponedDates.ContainsKey(key))
            return false;

        if (rule.PostponedDates.Values.Contains(key, StringComparer.Ordinal))
            return true;

        return rule.Kind switch
        {
            CalendarScheduleKind.Weekly => rule.Weekdays.Contains(date.DayOfWeek),
            CalendarScheduleKind.MonthlyDay => date.Day == Math.Clamp(rule.DayOfMonth, 1, DateTime.DaysInMonth(date.Year, date.Month)),
            CalendarScheduleKind.FirstBusinessDay => date.Date == FirstBusinessDay(date.Year, date.Month),
            CalendarScheduleKind.LastBusinessDay => date.Date == LastBusinessDay(date.Year, date.Month),
            _ => false
        };
    }

    public static bool IsCompletedOn(DateTime day, IEnumerable<Models.TimeModel>? completions)
        => completions?.Any(entry => entry.StartedAt.Date == day.Date && entry.CompletedAt.HasValue) == true;

    private static DateTime FirstBusinessDay(int year, int month)
    {
        DateTime date = new(year, month, 1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(1);
        return date;
    }

    private static DateTime LastBusinessDay(int year, int month)
    {
        DateTime date = new(year, month, DateTime.DaysInMonth(year, month));
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(-1);
        return date;
    }
}
