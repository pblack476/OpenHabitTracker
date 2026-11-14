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

    /// <summary>
    /// Calendar entries are independent of the list's HideCompletedTasks filter.
    /// A completed task remains visible on the day it was scheduled.
    /// </summary>
    public static IReadOnlyList<OpenHabitTracker.Data.Models.TaskModel> TasksForDay(
        IEnumerable<OpenHabitTracker.Data.Models.TaskModel> tasks, DateTime day) =>
        tasks.Where(task => !task.IsDeleted && task.PlannedAt?.Date == day.Date)
            .OrderBy(task => task.CompletedAt is not null)
            .ThenBy(task => task.PlannedAt)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    /// <summary>
    /// Do not assign a made-up date to new tasks. Show them separately so they
    /// can be scheduled from the calendar.
    /// </summary>
    public static IReadOnlyList<OpenHabitTracker.Data.Models.TaskModel> Unscheduled(
        IEnumerable<OpenHabitTracker.Data.Models.TaskModel> tasks) =>
        tasks.Where(task => !task.IsDeleted && task.CompletedAt is null && task.PlannedAt is null)
            .OrderByDescending(task => task.Priority)
            .ThenByDescending(task => task.CreatedAt)
            .ToArray();

    /// <summary>
    /// Explicit calendar rules take precedence. Recurring tasks that predate
    /// the rules feature receive an approximate projection based on their
    /// existing interval and repeat count. Actual completed days are always
    /// included, even when their date differs from the projection.
    /// </summary>
    public static IReadOnlyList<OpenHabitTracker.Data.Models.HabitModel> RecurringForDay(
        IEnumerable<OpenHabitTracker.Data.Models.HabitModel> habits,
        DateTime day, Func<long, OpenHabitTracker.Data.CalendarRecurrenceRule?> getRule) =>
        habits.Where(habit => IsRecurringOn(habit, day, getRule(habit.Id)))
            .OrderByDescending(habit => habit.Priority)
            .ThenBy(habit => habit.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public static bool IsRecurringOn(OpenHabitTracker.Data.Models.HabitModel habit, DateTime day,
        OpenHabitTracker.Data.CalendarRecurrenceRule? rule)
    {
        if (habit.IsDeleted)
            return false;

        // Historical completion must never disappear due to a changed rule.
        if (OpenHabitTracker.Data.CalendarRecurrence.IsCompletedOn(day, habit.TimesDone))
            return true;

        if (rule is { Kind: not OpenHabitTracker.Data.CalendarScheduleKind.None })
            return OpenHabitTracker.Data.CalendarRecurrence.IsDue(rule, day, habit.StartAt ?? habit.CreatedAt);

        return IsLegacyProjectedOccurrence(habit, day);
    }

    /// <summary>
    /// For interval-only recurrence, spread the expected RepeatCount
    /// occurrences evenly within each existing repeat period. These are
    /// projections, not new dates saved to the database. Month/year intervals
    /// use real calendar lengths rather than fixed 30/365-day approximations.
    /// </summary>
    public static bool IsLegacyProjectedOccurrence(OpenHabitTracker.Data.Models.HabitModel habit, DateTime day)
    {
        DateTime anchor = (habit.StartAt ?? habit.CreatedAt).Date;
        DateTime target = day.Date;
        if (anchor == DateTime.MinValue || target < anchor)
            return false;

        long interval = Math.Max(1L, habit.RepeatInterval);
        long repeats = Math.Max(1L, habit.RepeatCount);

        if (habit.RepeatPeriod is OpenHabitTracker.Data.Period.Day or OpenHabitTracker.Data.Period.Week)
        {
            long periodDays = interval * (habit.RepeatPeriod == OpenHabitTracker.Data.Period.Week ? 7L : 1L);
            long offset = (target - anchor).Days % periodDays;
            return IsExpectedOffset(offset, periodDays, repeats);
        }

        long intervalMonths = interval * (habit.RepeatPeriod == OpenHabitTracker.Data.Period.Year ? 12L : 1L);
        long monthsBetween = (target.Year - anchor.Year) * 12L + target.Month - anchor.Month;
        long blockIndex = monthsBetween / intervalMonths;
        DateTime periodStart = AddMonthsPreservingAnchor(anchor, blockIndex * intervalMonths);
        if (target < periodStart)
        {
            if (blockIndex == 0)
                return false;
            blockIndex--;
            periodStart = AddMonthsPreservingAnchor(anchor, blockIndex * intervalMonths);
        }

        DateTime next = AddMonthsPreservingAnchor(anchor, (blockIndex + 1) * intervalMonths);
        long periodLength = Math.Max(1, (next - periodStart).Days);
        return IsExpectedOffset((target - periodStart).Days, periodLength, repeats);
    }

    private static bool IsExpectedOffset(long offset, long days, long repetitions)
    {
        if (offset < 0 || offset >= days)
            return false;

        long slots = Math.Min(repetitions, days);
        // The integer positions floor(i * days / slots) represent equally
        // spaced expected completions without iterating through huge intervals.
        long candidate = (offset * slots + days - 1) / days;
        return candidate < slots && candidate * days / slots == offset;
    }

    private static DateTime AddMonthsPreservingAnchor(DateTime anchor, long months)
    {
        long monthIndex = (anchor.Year - 1L) * 12 + (anchor.Month - 1L) + months;
        if (monthIndex >= 9999L * 12)
            return DateTime.MaxValue.Date;
        int year = (int)(monthIndex / 12) + 1;
        int month = (int)(monthIndex % 12) + 1;
        return new DateTime(year, month, Math.Min(anchor.Day, DateTime.DaysInMonth(year, month)));
    }

    public static IReadOnlyList<DateTime> WeekGrid(DateTime day, DayOfWeek firstDay)
    {
        DateTime start = StartOfWeek(day, firstDay);
        return Enumerable.Range(0, 7).Select(index => start.AddDays(index)).ToArray();
    }
}
