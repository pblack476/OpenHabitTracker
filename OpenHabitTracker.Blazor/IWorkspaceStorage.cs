namespace OpenHabitTracker.Blazor;

/// <summary>
/// Account-scoped QoL workspace preferences. The browser implementation stays
/// compatible with pre-existing localStorage keys for non-desktop hosts.
/// </summary>
public interface IWorkspaceStorage
{
    Task<string?> Read(string scope);
    Task Write(string scope, string json);
}

public sealed class BrowserWorkspaceStorage(IJsInterop js) : IWorkspaceStorage
{
    public async Task<string?> Read(string scope) => await js.GetWorkspaceState(scope);
    public async Task Write(string scope, string json) => await js.SetWorkspaceState(scope, json);
}
