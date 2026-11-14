using OpenHabitTracker.Services;

namespace OpenHabitTracker.UnitTests.Services;

[TestFixture]
public class BulkTextParserTests
{
    [Test]
    public void Parse_SupportsPlainBulletsNumbersAndCheckboxes()
    {
        const string text = "Email client\r\n- [ ] Review contracts\n* [x] Finished?\n1. Schedule meeting\n• Send invoice\n\n";
        Assert.That(BulkTextParser.Parse(text), Is.EqualTo(new[]
        {
            "Email client", "Review contracts", "Finished?", "Schedule meeting", "Send invoice"
        }));
    }

    [Test]
    public void Parse_RejectsOversizedInputBeforeAnyWrites()
    {
        string many = string.Join("\n", Enumerable.Repeat("task", BulkTextParser.MaxItems + 1));
        Assert.Throws<ArgumentException>(() => BulkTextParser.Parse(many));
        Assert.Throws<ArgumentException>(() => BulkTextParser.Parse(new string('x', BulkTextParser.MaxTitleLength + 1)));
    }
}
