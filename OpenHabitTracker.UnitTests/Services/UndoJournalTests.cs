using Markdig;
using NSubstitute;
using OpenHabitTracker.App;
using OpenHabitTracker.Data;
using OpenHabitTracker.Services;

namespace OpenHabitTracker.UnitTests.Services;

[TestFixture]
public class UndoJournalTests
{
    private static UndoJournal Create(ClientState state) => new(state);

    [Test]
    public async Task RecordUndoAndNoRecursiveRecording()
    {
        IDataAccess db = Substitute.For<IDataAccess>();
        db.DataLocation.Returns(DataLocation.Local);
        ClientState state = new([db], new MarkdownToHtml(new MarkdownPipelineBuilder().Build()));
        UndoJournal journal = Create(state);
        int count = 0;
        journal.Record("first", () =>
        {
            count++;
            journal.Record("should not record", () => Task.CompletedTask);
            return Task.CompletedTask;
        });

        Assert.That(await journal.UndoLast(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(journal.Count, Is.Zero);
        });
    }

    [Test]
    public async Task FailedUndoRemainsAvailable()
    {
        IDataAccess db = Substitute.For<IDataAccess>();
        db.DataLocation.Returns(DataLocation.Local);
        ClientState state = new([db], new MarkdownToHtml(new MarkdownPipelineBuilder().Build()));
        UndoJournal journal = Create(state);
        journal.Record("failure", () => throw new InvalidOperationException("No undo"));

        Assert.ThrowsAsync<InvalidOperationException>(async () => await journal.UndoLast());
        Assert.That(journal.Count, Is.EqualTo(1));
    }
}
