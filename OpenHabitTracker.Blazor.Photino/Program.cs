using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using OpenHabitTracker.App;
using OpenHabitTracker.Backup;
using OpenHabitTracker.Blazor.Auth;
using OpenHabitTracker.Blazor.Files;
using OpenHabitTracker.Blazor.Layout;
using OpenHabitTracker.Blazor.Web.ApiClient;
using OpenHabitTracker.Data;
using OpenHabitTracker.EntityFrameworkCore;
using OpenHabitTracker.SelfTest;
using OpenHabitTracker.Services;
using Photino.Blazor;
using Photino.NET;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace OpenHabitTracker.Blazor.Photino;

public class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        string databaseDirectory = Environment.GetEnvironmentVariable("SNAP_USER_COMMON") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".OpenHabitTracker");
        Directory.CreateDirectory(databaseDirectory);

        // Before anything Photino touches, so the checks run windowless inside whatever sandbox
        // the packaged app was installed into.
        if (SelfTestRunner.IsRequested(args))
        {
            return SelfTestRunner.RunSync(SelfTestChecks.Desktop(databaseDirectory), Console.Out);
        }

        PhotinoBlazorApp? app = null;

        // Registered first so startup and DB-init crashes are logged;
        // routed through the db directory so the write succeeds on sandboxed targets (Snap/Flatpak).
        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            try
            {
                string? message = error.ExceptionObject.ToString();

                Debug.WriteLine(message);

                FileLog.Write(databaseDirectory, LogLevel.Critical, typeof(Program).FullName!, message ?? "Unhandled exception");

                app?.MainWindow.ShowMessage("Error", message);
            }
            catch
            {
            }
        };

        string databasePath = Path.Combine(databaseDirectory, "OpenHT.db");
        LocalBackupManager backups = new(databasePath);

        if (args.Contains("--notification-scan"))
            return ClosedAppNotificationScanner.Run(databaseDirectory, databasePath).GetAwaiter().GetResult();

        if (args.Contains("--list-backups"))
        {
            foreach (LocalBackupManager.Snapshot entry in backups.ListBackups())
                Console.WriteLine($"{entry.FileName} | {entry.CreatedAt:O} | {entry.Length} bytes | {(backups.VerifyBackup(entry.FileName) ? "VERIFIED" : "INVALID")}");
            return 0;
        }

        if (args.Contains("--backup-now"))
        {
            string? path = backups.BackupNow(DateTime.Now);
            Console.WriteLine(path ?? "No local SQLite database exists yet.");
            return 0;
        }

        int restoreArg = Array.IndexOf(args, "--restore-backup");
        if (restoreArg >= 0)
        {
            if (!args.Contains("--confirm-restore") || restoreArg + 1 >= args.Length)
            {
                Console.Error.WriteLine("Restore requires --restore-backup <file name> --confirm-restore, with the app closed.");
                return 2;
            }

            try
            {
                string preRestoreBackup = backups.Restore(args[restoreArg + 1], DateTime.Now);
                Console.WriteLine($"Database restored. Pre-restore backup: {preRestoreBackup}");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Restore failed: {exception.Message}");
                return 3;
            }
        }

        // Back up a consistent snapshot BEFORE any automatic EF migrations.
        // Fail closed rather than upgrade a database that could not be backed up.
        try
        {
            backups.BackupIfDue(DateTime.Now);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Automatic backup failed; startup stopped to protect data: {exception}");
            return 4;
        }

        // While the app is open, refresh the daily snapshot if the date changes.
        using System.Threading.Timer backupTimer = new(_ =>
        {
            try { backups.BackupIfDue(DateTime.Now); }
            catch (Exception error) { FileLog.Write(databaseDirectory, LogLevel.Error, nameof(LocalBackupManager), error.ToString()); }
        }, null, TimeSpan.FromHours(6), TimeSpan.FromHours(6));

        // Single-instance marker prevents the headless scan from duplicating
        // a notification already managed by the live GUI timer.
        using FileStream guiActivity = new(
            Path.Combine(databaseDirectory, "gui-active.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);

        PhotinoBlazorAppBuilder builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        builder.Services.AddLogging(loggingBuilder =>
        {
#if DEBUG
            loggingBuilder.AddDebug();
            //loggingBuilder.SetMinimumLevel(LogLevel.Debug);
#endif
            loggingBuilder.AddConsole();
            loggingBuilder.AddProvider(new FileLoggerProvider(databaseDirectory));
        });

        builder.Services.AddServices<OnClickMarkdownExtension>();
        builder.Services.AddDataAccess(databasePath);
        builder.Services.AddBackup();
        builder.Services.AddBlazor();
        builder.Services.AddScoped<IWorkspaceStorage>(sp => new DesktopWorkspaceStorage(databaseDirectory, sp.GetRequiredService<IJsInterop>()));
        builder.Services.AddScoped<IAppReview, AppReview>(); // different in Maui
        builder.Services.AddScoped<INotifications>(sp => new OpenHabitTracker.Blazor.Photino.Notifications(
            sp.GetRequiredService<ILogger<OpenHabitTracker.Blazor.Photino.Notifications>>(), databaseDirectory));
        builder.Services.AddScoped<IOpenFile, OpenFile>();
        builder.Services.AddScoped<ISaveFile, SaveFile>();
        builder.Services.AddScoped<INavBarFragment, NavBarFragment>();
        builder.Services.AddScoped<IAssemblyProvider, AssemblyProvider>();
        builder.Services.AddScoped<ILinkAttributeService, LinkAttributeService>();
        builder.Services.AddScoped<IPreRenderService, PreRenderService>();
        builder.Services.AddScoped<IAuthFragment, OpenHabitTracker.Blazor.Auth.AuthFragment>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAccountService, ApiAccountService>();
        builder.Services.AddHttpClients();

        // register root component and selector
        builder.RootComponents.Add<Routes>("app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        app = builder.Build();

        //ILoggerFactory loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        // 0

        ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Initializing database");

        IDataAccess dataAccess = app.Services.GetServices<IDataAccess>().First(x => x.DataLocation == DataLocation.Local);
        dataAccess.Initialize().GetAwaiter().GetResult();

        //ClientState clientState = app.Services.GetRequiredService<ClientState>();
        //clientState.LoadUsers();
        //clientState.LoadSettings();

        //IAuthService authService = app.Services.GetRequiredService<IAuthService>();
        //authService.TryRefreshTokenLogin();

        // customize window
        app.MainWindow.SetIconFile(Path.Combine(AppContext.BaseDirectory, "favicon.ico"));
        app.MainWindow.SetTitle("OpenHabitTracker");
        app.MainWindow.SetUseOsDefaultSize(false);
        app.MainWindow.SetUseOsDefaultLocation(false);
#if !DEBUG
        app.MainWindow.SetDevToolsEnabled(false);
#endif

        string windowSettingsPath = Path.Combine(databaseDirectory, WindowSettings.FileName);

        WindowSettings? saved = WindowSettings.Load(windowSettingsPath);

        if (saved is not null)
        {
            // Applied before the native window exists, so these land in the startup parameters:
            // on X11 the position only survives as a pre-realize hint, a move after the window is
            // shown loses to the window manager's own placement. Top-left of 0,0 on first run is
            // already the startup default, so it takes the same path.
            app.MainWindow.SetLeft((int)saved.X);
            app.MainWindow.SetTop((int)saved.Y);
            app.MainWindow.SetSize((int)saved.Width, (int)saved.Height);
        }

        app.MainWindow.WindowCreated += (sender, e) =>
        {
            if (saved is null)
            {
                // First run: min(0.9 of the work area, the 1680x1050 logical cap scaled to pixels). Pixels are the native unit.
                // MainMonitor is only valid once the native window exists, so this size is set here.
                Monitor monitor = app.MainWindow.MainMonitor;
                Rectangle workArea = monitor.WorkArea;
                double scale = monitor.Scale;
                app.MainWindow.SetSize(
                    Math.Min((int)(workArea.Width * 0.9), (int)(1680 * scale)),
                    Math.Min((int)(workArea.Height * 0.9), (int)(1050 * scale)));
            }

            // Attach after the initial sizing so only user resizes/moves persist.
            app.MainWindow.WindowSizeChanged += (s, size) => SaveWindowSettings(app.MainWindow, windowSettingsPath);
            app.MainWindow.WindowLocationChanged += (s, location) => SaveWindowSettings(app.MainWindow, windowSettingsPath);
        };

        logger.LogInformation("Running app");

        app.Run();

        return 0;
    }

    static void SaveWindowSettings(PhotinoWindow window, string path)
    {
        new WindowSettings
        {
            X = window.Left,
            Y = window.Top,
            Width = window.Width,
            Height = window.Height
        }.Save(path);
    }

    [JSInvokable]
    public static void OpenLink(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
