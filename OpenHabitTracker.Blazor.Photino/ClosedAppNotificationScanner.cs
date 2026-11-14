using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenHabitTracker.Blazor;
using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Entities;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.EntityFrameworkCore;
using OpenHabitTracker.Services;

namespace OpenHabitTracker.Blazor.Photino;

/// <summary>
/// A short-lived, headless Flatpak invocation scheduled by systemd --user.
/// No WebView, no EF migration, and no mutation of user content.
/// </summary>
public static class ClosedAppNotificationScanner
{
    public static async Task<int> Run(string dataDirectory, string databasePath)
    {
        if (!File.Exists(databasePath))
            return 0;

        // The GUI delivers its own live notifications. A separate user timer
        // must not send the same alert at the same time.
        string guiLock = Path.Combine(dataDirectory, "gui-active.lock");
        try
        {
            using FileStream guiProbe = new(guiLock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return 0;
        }

        try
        {
            return await DesktopReminderState.Change(dataDirectory, async state =>
            {
                DateTime now = DateTime.Now;
                DateTime start = state.LastScan == default || state.LastScan > now
                    ? now.AddMinutes(-2)
                    : state.LastScan < now.AddMinutes(-30) ? now.AddMinutes(-30) : state.LastScan;

                ServiceCollection collection = new();
                collection.AddDataAccess(databasePath);
                using ServiceProvider provider = collection.BuildServiceProvider();
                using IServiceScope scope = provider.CreateScope();
                IDataAccess data = scope.ServiceProvider.GetRequiredService<IDataAccess>();

                IReadOnlyList<SettingsEntity> settingsRows = await data.GetSettings();
                IReadOnlyList<TaskEntity> taskRows = await data.GetTasks();
                IReadOnlyList<HabitEntity> habitRows = await data.GetHabits();
                IReadOnlyList<TimeEntity> timeRows = await data.GetTimes();
                Dictionary<long, List<TimeModel>> timesByHabit = timeRows
                    .GroupBy(row => row.HabitId)
                    .ToDictionary(g => g.Key, g => g.Select(row => row.ToModel()).ToList());

                int sentCount = 0;
                foreach (SettingsEntity settingEntity in settingsRows)
                {
                    SettingsModel settings = settingEntity.ToModel();
                    List<TaskModel> tasks = taskRows.Where(t => t.UserId == settingEntity.UserId)
                        .Select(t => t.ToModel()).ToList();
                    List<HabitModel> habits = habitRows.Where(h => h.UserId == settingEntity.UserId)
                        .Select(h =>
                        {
                            HabitModel habit = h.ToModel();
                            habit.TimesDone = timesByHabit.GetValueOrDefault(h.Id) ?? [];
                            habit.RefreshTimesDoneByDay();
                            return habit;
                        }).ToList();

                    Dictionary<long, int> overrides = [];
                    string? workspace = DesktopWorkspaceStorage.ReadFile(dataDirectory, $"Local:{settingEntity.UserId}");
                    if (workspace is not null)
                    {
                        try
                        {
                            overrides = JsonSerializer.Deserialize<PersonalWorkspaceState.WorkspaceData>(workspace)?.TaskLeadMinutes ?? [];
                        }
                        catch (JsonException exception)
                        {
                            Console.Error.WriteLine($"Skipping invalid workspace for user {settingEntity.UserId}: {exception.Message}");
                            continue;
                        }
                    }

                    int? Lead(long taskId) => overrides.TryGetValue(taskId, out int value)
                        ? (value == -1 ? null : Math.Clamp(value, 0, 43200))
                        : settings.NotificationLeadMinutes;

                    foreach (ScheduledNotification due in ClosedAppReminderPlanner.Due(tasks, habits, settings, start, now, Lead))
                    {
                        string fingerprint = $"u{settingEntity.UserId}-{due.Kind}-{due.TaskId}-{due.NotifyAt.Ticks}";
                        if (state.Sent.ContainsKey(fingerprint)) continue;

                        TaskEntity? task = taskRows.FirstOrDefault(t => t.Id == due.TaskId && t.UserId == settingEntity.UserId);
                        string title = due.Kind == NotificationKind.TaskReminder
                            ? task?.Title ?? "OpenHabitTracker"
                            : settings.Culture.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "Pendências de hoje" : "Due today";
                        string body = due.Kind == NotificationKind.TaskReminder
                            ? task?.PlannedAt?.ToString("g") ?? ""
                            : string.Join(", ", due.TaskIds.Select(id => taskRows.FirstOrDefault(t => t.Id == id && t.UserId == settingEntity.UserId)?.Title)
                                .Concat(due.HabitIds.Select(id => habitRows.FirstOrDefault(h => h.Id == id && h.UserId == settingEntity.UserId)?.Title))
                                .Where(x => !string.IsNullOrWhiteSpace(x)));

                        await DesktopPortalNotificationSender.Send(new NotificationRequest
                        {
                            Id = "headless-" + fingerprint,
                            Title = title,
                            Body = body,
                            Route = due.Kind == NotificationKind.TaskReminder ? $"/tasks/{due.TaskId}" : "/today",
                            NotifyAt = due.NotifyAt
                        });
                        state.Sent[fingerprint] = now;
                        sentCount++;
                    }
                }

                foreach ((long taskId, DateTime dueAt) in state.Snoozes.ToArray())
                {
                    if (dueAt > now) continue;
                    TaskEntity? task = taskRows.FirstOrDefault(t => t.Id == taskId && !t.IsDeleted && t.CompletedAt is null);
                    if (task is not null && dueAt >= now.AddMinutes(-30))
                    {
                        string fingerprint = $"snooze-{taskId}-{dueAt.Ticks}";
                        if (!state.Sent.ContainsKey(fingerprint))
                        {
                            await DesktopPortalNotificationSender.Send(new NotificationRequest
                            {
                                Id = "headless-" + fingerprint,
                                Title = task.Title,
                                Body = task.PlannedAt?.ToString("g") ?? "",
                                Route = $"/tasks/{taskId}",
                                NotifyAt = dueAt
                            });
                            state.Sent[fingerprint] = now;
                            sentCount++;
                        }
                    }
                    state.Snoozes.Remove(taskId);
                }

                foreach (string key in state.Sent.Where(x => x.Value < now.AddDays(-3)).Select(x => x.Key).ToArray())
                    state.Sent.Remove(key);
                state.LastScan = now;
                Console.WriteLine($"Notification scan: {sentCount} reminder(s) sent.");
                return 0;
            });
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Notification scan failed; it will retry on the next timer tick: " + exception);
            return 2;
        }
    }
}
