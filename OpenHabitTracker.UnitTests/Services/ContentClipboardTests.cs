using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.Services;

namespace OpenHabitTracker.UnitTests.Services;

[TestFixture]
public class ContentClipboardTests
{
    [Test]
    public void TaskCopy_IsDetachedAndDoesNotCarryCompletion()
    {
        DateTime scheduled = new(2026, 11, 20, 9, 0, 0);
        TaskModel original = new()
        {
            Title = "Relatorio",
            Priority = Priority.High,
            Color = "bg-info-subtle",
            PlannedAt = scheduled,
            CompletedAt = DateTime.Now,
            Items = [new ItemModel { Title = "Revisar", DoneAt = DateTime.Now }]
        };
        ContentClipboard clipboard = new();
        clipboard.CopyTask(original, 1, DataLocation.Local);
        original.Title = "Alterado";
        original.Items[0].Title = "Alterado";

        TaskModel? pasted = clipboard.PasteTask(1, DataLocation.Local);
        Assert.That(pasted, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(pasted!.Title, Is.EqualTo("Relatorio"));
            Assert.That(pasted.Priority, Is.EqualTo(Priority.High));
            Assert.That(pasted.PlannedAt, Is.EqualTo(scheduled));
            Assert.That(pasted.CompletedAt, Is.Null);
            Assert.That(pasted.StartedAt, Is.Null);
            Assert.That(pasted.Items, Has.Count.EqualTo(1));
            Assert.That(pasted.Items![0].Title, Is.EqualTo("Revisar"));
            Assert.That(pasted.Items[0].DoneAt, Is.Null);
        });
        pasted.Items![0].Title = "Outra copia";
        Assert.That(clipboard.PasteTask(1, DataLocation.Local)!.Items![0].Title, Is.EqualTo("Revisar"));
    }

    [Test]
    public void DirectClone_DoesNotChangeOrReuseTheInAppClipboard()
    {
        ContentClipboard clipboard = new();
        clipboard.CopyTask(new TaskModel { Title = "Clipboard" }, 1, DataLocation.Local);

        TaskModel source = new() { Title = "Independent", Items = [new ItemModel { Title = "item", DoneAt = DateTime.Now }] };
        TaskModel clone = ContentClipboard.CloneTask(source);

        Assert.Multiple(() =>
        {
            Assert.That(clone.Title, Is.EqualTo("Independent"));
            Assert.That(clone.Items![0].DoneAt, Is.Null);
            Assert.That(clipboard.PasteTask(1, DataLocation.Local)!.Title, Is.EqualTo("Clipboard"));
        });
    }

    [Test]
    public void NoteCopy_KeepsContentAndSeparatesAccounts()
    {
        ContentClipboard clipboard = new();
        clipboard.CopyNote(new NoteModel { Title = "Plano", Content = "**texto**" }, 1, DataLocation.Local);

        Assert.That(clipboard.CanPasteNote(2, DataLocation.Local), Is.False);
        Assert.That(clipboard.PasteNote(1, DataLocation.Remote), Is.Null);
        Assert.That(clipboard.PasteNote(1, DataLocation.Local)!.Content, Is.EqualTo("**texto**"));
        Assert.That(clipboard.CanPasteTask(1, DataLocation.Local), Is.False);
    }

    [Test]
    public void HabitCopy_PreservesFrequencyButNotHistory()
    {
        ContentClipboard clipboard = new();
        HabitModel original = new()
        {
            Title = "Exercício",
            RepeatCount = 3,
            RepeatInterval = 2,
            RepeatPeriod = Period.Week,
            LastTimeDoneAt = DateTime.Now,
            TimesDone = [new TimeModel { StartedAt = DateTime.Now }],
            Items = [new ItemModel { Title = "Aquecimento", DoneAt = DateTime.Now }]
        };

        clipboard.CopyHabit(original, 1, DataLocation.Local);
        HabitModel pasted = clipboard.PasteHabit(1, DataLocation.Local)!;

        Assert.Multiple(() =>
        {
            Assert.That(pasted.RepeatCount, Is.EqualTo(3));
            Assert.That(pasted.RepeatInterval, Is.EqualTo(2));
            Assert.That(pasted.RepeatPeriod, Is.EqualTo(Period.Week));
            Assert.That(pasted.LastTimeDoneAt, Is.Null);
            Assert.That(pasted.TimesDone, Is.Null);
            Assert.That(pasted.Items![0].DoneAt, Is.Null);
        });
    }
}
