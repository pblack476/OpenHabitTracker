using System;
using System.Threading.Tasks;
using OpenHabitTracker.Services;
using Tmds.DBus.Protocol;

namespace OpenHabitTracker.Blazor.Photino;

/// <summary>Sends notifications via the portal available to the Flatpak sandbox.
/// No direct org.freedesktop.Notifications permission is needed.</summary>
public static class DesktopPortalNotificationSender
{
    private const string Service = "org.freedesktop.portal.Desktop";
    private const string Path = "/org/freedesktop/portal/desktop";
    private const string Interface = "org.freedesktop.portal.Notification";

    public static async Task Send(NotificationRequest request, bool withButtons = false, bool alreadyConnected = false)
    {
        DBusConnection connection = DBusConnection.Session;
        if (!alreadyConnected)
            await connection.ConnectAsync();

        MessageBuffer message;
        {
        using MessageWriter writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: Service, path: Path,
            @interface: Interface, member: "AddNotification", signature: "sa{sv}",
            flags: MessageFlags.None);
        writer.WriteString(request.Id);

        ArrayStart dictionary = writer.WriteDictionaryStart();
        writer.WriteDictionaryEntryStart();
        writer.WriteString("title");
        writer.WriteVariantString(request.Title);
        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString("body");
            writer.WriteVariantString(request.Body);
        }
        writer.WriteDictionaryEntryStart();
        writer.WriteString("default-action");
        writer.WriteVariantString("open");

        if (withButtons && request.Route.StartsWith("/tasks/", StringComparison.Ordinal))
        {
            var buttons = new Tmds.DBus.Protocol.Array<Tmds.DBus.Protocol.Dict<string, VariantValue>>
            {
                new Tmds.DBus.Protocol.Dict<string, VariantValue>
                {
                    { "label", request.SnoozeLabel },
                    { "action", "snooze" }
                },
                new Tmds.DBus.Protocol.Dict<string, VariantValue>
                {
                    { "label", request.CompleteLabel },
                    { "action", "complete" }
                }
            };
            writer.WriteDictionaryEntryStart();
            writer.WriteString("buttons");
            writer.WriteVariant(buttons);
        }
        writer.WriteDictionaryEnd(dictionary);
        message = writer.CreateMessage();
        }
        await connection.CallMethodAsync(message);
    }
}
