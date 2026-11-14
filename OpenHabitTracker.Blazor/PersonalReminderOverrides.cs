using OpenHabitTracker.Services;

namespace OpenHabitTracker.Blazor;

/// <summary>
/// Blazor/WebView provider. The existing global notification setting is used
/// until the account-specific workspace has been loaded after first render.
/// </summary>
public sealed class PersonalReminderOverrides(PersonalWorkspaceState workspace) : ITaskReminderOverrides
{
    private readonly PersonalWorkspaceState _workspace = workspace;
    public bool HasEnabledOverrides => _workspace.HasIndividualReminders;
    public int? ResolveLeadMinutes(long taskId, int? globalMinutes)
        => _workspace.ResolveReminderMinutes(taskId, globalMinutes);
}
