using Microsoft.Extensions.Logging;
using OpenHabitTracker.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace OpenHabitTracker.Blazor.Photino;

// Linux has no OS scheduler for notifications: the portal exposes AddNotification and
// RemoveNotification and nothing else, so this host fires from a timer while it runs.
// The portal is used rather than org.freedesktop.Notifications directly, because Flatpak's
// default bus policy allows org.freedesktop.portal.* and the manifest carries no --talk-name.
public sealed class Notifications(ILogger<Notifications> logger, string dataDirectory) : RunningOnlyNotifications
{
    private const string PortalService = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string PortalInterface = "org.freedesktop.portal.Notification";

    private readonly ILogger<Notifications> _logger = logger;
    private readonly string _dataDirectory = dataDirectory;
    private readonly Dictionary<string, string> _titleByRoute = new();

    private bool _connected;

    private bool _watching;

    // The portal reports an invoked action by notification id, so the route is kept beside it.
    private readonly Dictionary<string, string> _routesById = new();

    public override bool CanNotify => OperatingSystem.IsLinux();

    // The portal has no permission prompt of its own; the desktop decides whether to show it.
    public override Task<bool> RequestPermission()
    {
        return Task.FromResult(CanNotify);
    }

    public override Task<NotificationPermission> GetPermission()
    {
        return Task.FromResult(NotificationPermission.Unknown);
    }

    protected override async Task Show(NotificationRequest request)
    {
        try
        {
            DBusConnection connection = DBusConnection.Session;
            if (!_connected)
            {
                await connection.ConnectAsync();
                _connected = true;
            }
            await WatchActions(connection);

            _routesById[request.Id] = request.Route;
            _titleByRoute[request.Route] = request.Title;
            await DesktopPortalNotificationSender.Send(request, withButtons: true, alreadyConnected: true);

            // A temporary snooze was delivered while the GUI was still running.
            // Remove its headless counterpart before the next background tick.
            if (request.Id.StartsWith("snooze-", StringComparison.Ordinal) &&
                TryTaskId(request.Route, out long taskId))
            {
                await DesktopReminderState.Change(_dataDirectory, state =>
                {
                    if (state.Snoozes.TryGetValue(taskId, out DateTime due) &&
                        due <= DateTime.Now)
                        state.Snoozes.Remove(taskId);
                    return Task.FromResult(0);
                });
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Showing a notification failed");
        }
    }

    public override async Task Snooze(string route, TimeSpan delay)
    {
        if (!TryTaskId(route, out long taskId) || delay <= TimeSpan.Zero)
            return;

        DateTime when = DateTime.Now.Add(delay);
        await DesktopReminderState.Change(_dataDirectory, state =>
        {
            state.Snoozes[taskId] = when;
            return Task.FromResult(0);
        });

        QueueTemporary(new NotificationRequest
        {
            Id = $"snooze-{taskId}-{when.Ticks}",
            Route = route,
            Title = _titleByRoute.TryGetValue(route, out string? title) ? title : "OpenHabitTracker",
            Body = "",
            NotifyAt = when,
            SnoozeLabel = "Adiar 10 min",
            CompleteLabel = "Concluir"
        });
    }

    private static bool TryTaskId(string route, out long taskId)
    {
        taskId = 0;
        return route.StartsWith("/tasks/", StringComparison.Ordinal) &&
               long.TryParse(route.Substring("/tasks/".Length), out taskId);
    }

    private async Task WatchActions(DBusConnection connection)
    {
        if (_watching)
            return;

        _watching = true;

        await connection.WatchSignalAsync(
            sender: PortalService,
            path: PortalPath,
            @interface: PortalInterface,
            signal: "ActionInvoked",
            reader: ReadNotificationAction,
            handler: OnActionInvoked,
            flags: ObserverFlags.None,
            emitOnCapturedContext: false);
    }

    private readonly record struct PortalAction(string Id, string Action);

    private static PortalAction ReadNotificationAction(Message message, object? state)
    {
        Reader reader = message.GetBodyReader();
        return new(reader.ReadString(), reader.ReadString());
    }

    private void OnActionInvoked(Notification<PortalAction> notification)
    {
        if (!notification.HasValue ||
            !_routesById.TryGetValue(notification.Value.Id, out string? route))
            return;

        if (notification.Value.Action is "snooze" or "complete")
        {
            _ = OnAction(route, notification.Value.Action).ContinueWith(task =>
            {
                if (task.IsFaulted)
                    _logger.LogError(task.Exception, "Notification action failed");
            });
        }
        else
        {
            OnActivated(route);
        }
    }
}
