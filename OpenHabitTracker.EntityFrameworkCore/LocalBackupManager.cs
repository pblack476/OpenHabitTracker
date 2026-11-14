using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace OpenHabitTracker.EntityFrameworkCore;

/// <summary>
/// Consistent snapshots of the unchanged local SQLite database. This is intentionally
/// outside EF migrations and can run before the database is migrated at startup.
/// </summary>
public sealed class LocalBackupManager(string databasePath)
{
    private readonly string _databasePath = Path.GetFullPath(databasePath);
    private readonly object _gate = new();

    public string BackupDirectory => Path.Combine(Path.GetDirectoryName(_databasePath)!, "automatic-backups");

    public sealed record Snapshot(string FileName, DateTime CreatedAt, long Length);

    /// <summary>One verified snapshot per calendar day; no database means first launch.</summary>
    public string? BackupIfDue(DateTime now)
    {
        lock (_gate)
        {
            if (!File.Exists(_databasePath))
                return null;

            string prefix = $"OpenHT-{now:yyyyMMdd}-";
            if (Directory.Exists(BackupDirectory) &&
                Directory.EnumerateFiles(BackupDirectory, prefix + "*.db")
                    .Any(path => IsHealthy(path, requireChecksum: true)))
                return null;

            return BackupCore(now);
        }
    }

    public string? BackupNow(DateTime now)
    {
        lock (_gate)
            return File.Exists(_databasePath) ? BackupCore(now) : null;
    }

    public IReadOnlyList<Snapshot> ListBackups()
    {
        lock (_gate)
        {
            if (!Directory.Exists(BackupDirectory))
                return [];

            return Directory.EnumerateFiles(BackupDirectory, "OpenHT-*.db")
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.Name, StringComparer.Ordinal)
                .Select(info => new Snapshot(info.Name, info.LastWriteTime, info.Length))
                .ToArray();
        }
    }

    /// <summary>
    /// Restores a named, verified snapshot, first saving another snapshot of the
    /// live database. Must be called in a windowless process with the GUI closed.
    /// Returns the emergency backup path for auditing or manual recovery.
    /// </summary>
    public string Restore(string backupName, DateTime now)
    {
        lock (_gate)
        {
            // Only snapshots in our own backup directory can be restored, not arbitrary paths.
            if (string.IsNullOrWhiteSpace(backupName) ||
                backupName != Path.GetFileName(backupName) ||
                !backupName.StartsWith("OpenHT-", StringComparison.Ordinal) ||
                !backupName.EndsWith(".db", StringComparison.Ordinal))
                throw new ArgumentException("Choose a backup name from --list-backups.", nameof(backupName));

            string snapshotPath = Path.Combine(BackupDirectory, backupName);
            if (!IsHealthy(snapshotPath, requireChecksum: true))
                throw new InvalidDataException("Backup failed checksum or SQLite integrity verification.");

            string? emergency = BackupNow(now);
            if (File.Exists(_databasePath) && emergency is null)
                throw new IOException("Could not create the mandatory pre-restore backup.");

            try
            {
                using SqliteConnection source = Open(snapshotPath, SqliteOpenMode.ReadOnly);
                using SqliteConnection destination = Open(_databasePath, SqliteOpenMode.ReadWriteCreate);
                source.BackupDatabase(destination);
            }
            catch
            {
                // The emergency snapshot is retained. SQLite's backup API performs
                // transactional writes; never remove the live database here.
                throw;
            }

            if (!IsHealthy(_databasePath, requireChecksum: false))
                throw new InvalidDataException("Restored database failed SQLite integrity verification. The emergency backup was retained.");

            return emergency ?? string.Empty;
        }
    }

    public bool VerifyBackup(string backupName)
    {
        if (backupName != Path.GetFileName(backupName) || !backupName.StartsWith("OpenHT-", StringComparison.Ordinal))
            return false;

        return IsHealthy(Path.Combine(BackupDirectory, backupName), requireChecksum: true);
    }

    private string BackupCore(DateTime now)
    {
        Directory.CreateDirectory(BackupDirectory);
        string filename = $"OpenHT-{now:yyyyMMdd}-{now:HHmmss}-{Guid.NewGuid():N}.db";
        string destinationPath = Path.Combine(BackupDirectory, filename);
        string temporaryPath = destinationPath + ".partial";

        try
        {
            using (SqliteConnection source = Open(_databasePath, SqliteOpenMode.ReadOnly))
            using (SqliteConnection destination = Open(temporaryPath, SqliteOpenMode.ReadWriteCreate))
            {
                source.BackupDatabase(destination);
            }

            if (!IsHealthy(temporaryPath, requireChecksum: false))
                throw new InvalidDataException("SQLite integrity_check failed for the new snapshot.");

            string checksum = Sha256(temporaryPath);
            File.Move(temporaryPath, destinationPath);
            File.WriteAllText(destinationPath + ".sha256", checksum + "\n");
            if (!IsHealthy(destinationPath, requireChecksum: true))
                throw new InvalidDataException("Checksum verification failed after backup finalization.");

            PruneVerifiedBackups();
            return destinationPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private void PruneVerifiedBackups()
    {
        // Keep the 30 newest plus one for each of the 12 most recent distinct
        // months. Never delete the last good snapshot or anything unverified.
        string[] all = Directory.EnumerateFiles(BackupDirectory, "OpenHT-*.db")
            .OrderByDescending(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray();
        if (all.Length <= 30)
            return;

        HashSet<string> keep = new(all.Take(30), StringComparer.Ordinal);
        HashSet<string> months = new(StringComparer.Ordinal);
        foreach (string path in all)
        {
            string name = Path.GetFileName(path);
            if (name.Length < 13)
                continue;
            string month = name.Substring(7, 6);
            if (months.Count >= 12 && !months.Contains(month))
                continue;
            if (months.Add(month) || months.Contains(month) && keep.Contains(path))
                keep.Add(path);
        }

        foreach (string path in all.Except(keep))
        {
            if (!IsHealthy(path, requireChecksum: true))
                continue;
            File.Delete(path);
            File.Delete(path + ".sha256");
        }
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = path,
            Mode = mode,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        SqliteConnection connection = new(builder.ToString());
        connection.Open();
        return connection;
    }

    private static bool IsHealthy(string path, bool requireChecksum)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            if (requireChecksum)
            {
                string checksumPath = path + ".sha256";
                if (!File.Exists(checksumPath) ||
                    !string.Equals(File.ReadAllText(checksumPath).Trim(), Sha256(path), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            using SqliteConnection connection = Open(path, SqliteOpenMode.ReadOnly);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
