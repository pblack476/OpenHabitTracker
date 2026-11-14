using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenHabitTracker.Blazor.Photino;

/// <summary>Agent state is separate from OpenHT.db, so scan/notification actions
/// cannot alter a user's notes, tasks, habits or EF migrations.</summary>
public static class DesktopReminderState
{
    public sealed class Data
    {
        public DateTime LastScan { get; set; }
        public Dictionary<string, DateTime> Sent { get; set; } = new();
        public Dictionary<long, DateTime> Snoozes { get; set; } = new();
    }

    public static async Task<T> Change<T>(string directory, Func<Data, Task<T>> action)
    {
        Directory.CreateDirectory(directory);
        string lockPath = Path.Combine(directory, "notification-agent.lock");
        FileStream? lease = null;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException) when (attempt < 39)
            {
                await Task.Delay(100);
            }
        }
        if (lease is null) throw new IOException("Notification agent lock could not be acquired.");

        using (lease)
        {
            string file = Path.Combine(directory, "notification-agent.json");
            Data data = File.Exists(file) ?
                JsonSerializer.Deserialize<Data>(await File.ReadAllTextAsync(file)) ?? new() : new();

            T result = await action(data);
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(data));
                if (OperatingSystem.IsLinux())
                    File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temp, file, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            return result;
        }
    }
}
