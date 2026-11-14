using Microsoft.Data.Sqlite;
using OpenHabitTracker.EntityFrameworkCore;

namespace OpenHabitTracker.UnitTests.Data;

[TestFixture]
public class LocalBackupManagerTests
{
    private string _folder = null!;
    private string _database = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "openht-backup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _database = Path.Combine(_folder, "OpenHT.db");
        using SqliteConnection connection = new($"Data Source={_database}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Notes (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL); INSERT INTO Notes VALUES (1, 'Original');";
        command.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Test]
    public void DailyBackup_IsVerified_AndDoesNotRewriteSource()
    {
        LocalBackupManager manager = new(_database);
        DateTime today = new(2026, 11, 14, 9, 0, 0);
        byte[] original = File.ReadAllBytes(_database);

        string snapshot = manager.BackupIfDue(today)!;
        Assert.That(File.Exists(snapshot), Is.True);
        Assert.That(manager.VerifyBackup(Path.GetFileName(snapshot)), Is.True);
        Assert.That(manager.BackupIfDue(today.AddHours(1)), Is.Null);
        Assert.That(manager.BackupIfDue(today.AddDays(1)), Is.Not.Null);
        Assert.That(File.ReadAllBytes(_database), Is.EqualTo(original));
        Assert.That(manager.ListBackups(), Has.Count.EqualTo(2));
    }

    [Test]
    public void Restore_RecoversOriginalWithoutLosingPreRestoreSnapshot()
    {
        LocalBackupManager manager = new(_database);
        string snapshot = manager.BackupNow(new DateTime(2026, 11, 14, 9, 0, 0))!;
        using (SqliteConnection db = new($"Data Source={_database}"))
        {
            db.Open();
            using SqliteCommand command = db.CreateCommand();
            command.CommandText = "UPDATE Notes SET Title='Changed' WHERE Id=1;";
            command.ExecuteNonQuery();
        }

        string emergency = manager.Restore(Path.GetFileName(snapshot), new DateTime(2026, 11, 14, 9, 3, 0));

        Assert.That(manager.VerifyBackup(Path.GetFileName(emergency)), Is.True);
        using SqliteConnection verify = new($"Data Source={_database}");
        verify.Open();
        using SqliteCommand query = verify.CreateCommand();
        query.CommandText = "SELECT Title FROM Notes WHERE Id=1;";
        Assert.That(query.ExecuteScalar(), Is.EqualTo("Original"));
    }

    [Test]
    public void BackupRestoresWorkspacePreferencesAlongsideSqlite()
    {
        string workspace = Path.Combine(_folder, "workspace-v1");
        Directory.CreateDirectory(workspace);
        string preferences = Path.Combine(workspace, "user.json");
        File.WriteAllText(preferences, "{\\"Pinned\\":[\\"tasks:1\\"]}");

        LocalBackupManager manager = new(_database);
        string snapshot = manager.BackupNow(new DateTime(2026, 11, 14, 9, 0, 0))!;
        File.WriteAllText(preferences, "{\\"Pinned\\":[]}");

        manager.Restore(Path.GetFileName(snapshot), new DateTime(2026, 11, 14, 10, 0, 0));
        Assert.That(File.ReadAllText(preferences), Does.Contain("tasks:1"));
    }

    [Test]
    public void Restore_RejectsCorruptSnapshotsAndPathTraversal()
    {
        LocalBackupManager manager = new(_database);
        string snapshot = manager.BackupNow(new DateTime(2026, 11, 14, 9, 0, 0))!;

        Assert.Throws<ArgumentException>(() => manager.Restore("../OpenHT.db", DateTime.Now));
        File.AppendAllText(snapshot, "corrupt");
        Assert.That(manager.VerifyBackup(Path.GetFileName(snapshot)), Is.False);
        Assert.Throws<InvalidDataException>(() => manager.Restore(Path.GetFileName(snapshot), DateTime.Now));
    }
}
