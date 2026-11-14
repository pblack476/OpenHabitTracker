namespace OpenHabitTracker.Services;

/// <summary>
/// Optional per-account scheduling overrides. A null value disables a
/// specific task reminder; a missing override uses the global default.
/// Hosts without this service keep the original schedule unchanged.
/// </summary>
public interface ITaskReminderOverrides
{
    bool HasEnabledOverrides { get; }
    int? ResolveLeadMinutes(long taskId, int? globalMinutes);
}
