using Bunit;
using Markdig;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using OpenHabitTracker.App;
using OpenHabitTracker.Blazor;
using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Models;
using OpenHabitTracker.Services;

namespace OpenHabitTracker.UnitTests.Components;

[TestFixture]
public class PlannerCalendarPageTests
{
    private BunitContext _ctx = null!;
    private ITaskService _tasks = null!;
    private IHabitService _habits = null!;

    [SetUp]
    public void SetUp()
    {
        _ctx = new BunitContext();
        IDataAccess data = Substitute.For<IDataAccess>();
        data.DataLocation.Returns(DataLocation.Local);
        ClientState state = new([data], new MarkdownToHtml(new MarkdownPipelineBuilder().Build()));
        state.Settings.ShowHelp = false;
        _tasks = Substitute.For<ITaskService>();
        _habits = Substitute.For<IHabitService>();

        IStringLocalizer localizer = Substitute.For<IStringLocalizer>();
        localizer[Arg.Any<string>()].Returns(c => new LocalizedString(c.RequiredArg<string>(), c.RequiredArg<string>()));
        _ctx.Services.AddScoped(_ => state);
        _ctx.Services.AddScoped(_ => _tasks);
        _ctx.Services.AddScoped(_ => _habits);
        _ctx.Services.AddSingleton(localizer);
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    [Test]
    public void LegacyRecurringTaskIsRenderedInDayAgendaWithoutRule()
    {
        HabitModel legacy = new()
        {
            Id = 42,
            Title = "Recurring without new rule",
            CreatedAt = DateTime.Today.AddDays(-2),
            RepeatPeriod = Period.Day,
            RepeatInterval = 1,
            TimesDone = []
        };
        _tasks.Tasks.Returns(Array.Empty<TaskModel>());
        _habits.Habits.Returns(new[] { legacy });

        IRenderedComponent<OpenHabitTracker.Blazor.Pages.Calendar> cut =
            _ctx.Render<OpenHabitTracker.Blazor.Pages.Calendar>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".app-calendar-preview-recurring"), Is.Not.Empty);
            Assert.That(cut.FindAll(".app-calendar-agenda a[href='/habits/42']"), Has.Count.EqualTo(1));
            Assert.That(cut.Markup, Does.Contain("Recurring without new rule"));
        });
    }

    [Test]
    public async Task NewTaskWithoutDateIsVisibleAndCanBeScheduledForSelectedDay()
    {
        TaskModel newTask = new() { Id = 7, Title = "New unplanned task", CreatedAt = DateTime.Now };
        _tasks.Tasks.Returns(new[] { newTask });
        _habits.Habits.Returns(Array.Empty<HabitModel>());

        IRenderedComponent<OpenHabitTracker.Blazor.Pages.Calendar> cut =
            _ctx.Render<OpenHabitTracker.Blazor.Pages.Calendar>();

        Assert.That(cut.FindAll(".app-calendar-unscheduled a[href='/tasks/7']"), Has.Count.EqualTo(1));
        await cut.Find(".app-calendar-unscheduled button").ClickAsync(new MouseEventArgs());

        await _tasks.Received(1).Reschedule(newTask, DateTime.Today);
    }

    [Test]
    public void AlreadyCompletedTaskIsStillVisibleOnPlannedDay()
    {
        TaskModel done = new()
        {
            Id = 91,
            Title = "Already done",
            PlannedAt = DateTime.Today.AddHours(10),
            CompletedAt = DateTime.Today.AddHours(11)
        };
        _tasks.Tasks.Returns(new[] { done });
        _habits.Habits.Returns(Array.Empty<HabitModel>());

        IRenderedComponent<OpenHabitTracker.Blazor.Pages.Calendar> cut =
            _ctx.Render<OpenHabitTracker.Blazor.Pages.Calendar>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".app-calendar-agenda a[href='/tasks/91']"), Has.Count.EqualTo(1));
            Assert.That(cut.Find(".app-calendar-agenda").TextContent, Does.Contain("Completed"));
            Assert.That(cut.FindAll(".app-calendar-agenda button"), Is.Empty);
        });
    }
}
