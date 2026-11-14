using System;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using OpenHabitTracker.Blazor;

namespace OpenHabitTracker.Blazor.Photino;

/// <summary>
/// A durable native mirror of preexisting WebView localStorage preferences:
/// this allows a headless Flatpak notification scan to read individual task
/// lead times, and preserves QoL settings across WebView profile resets.
/// </summary>
public sealed class DesktopWorkspaceStorage(string dataDirectory, IJsInterop js) : IWorkspaceStorage
{
    private readonly IJsInterop _js = js;
    private readonly string _root = Path.GetFullPath(dataDirectory);

    public string PathForScope(string scope) => GetPath(_root, scope);

    public async Task<string?> Read(string scope)
    {
        string path = PathForScope(scope);
        if (File.Exists(path))
            return await File.ReadAllTextAsync(path);

        // One-time, non-destructive migration of previously saved preferences.
        string? legacy = await _js.GetWorkspaceState(scope);
        if (!string.IsNullOrWhiteSpace(legacy))
            await Write(scope, legacy);
        return legacy;
    }

    public async Task Write(string scope, string json)
    {
        await WriteFile(_root, scope, json);
    }

    public static string GetPath(string root, string scope)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
        return Path.Combine(root, "workspace-v1", hash + ".json");
    }

    public static async Task WriteFile(string root, string scope, string json)
    {
        string path = GetPath(root, scope);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string? ReadFile(string root, string scope)
    {
        string path = GetPath(root, scope);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
