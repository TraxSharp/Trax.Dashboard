using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Trax.Api.DTOs;
using Trax.Core.Exceptions;
using Trax.Dashboard.Components.Pages.Data;
using Trax.Dashboard.Components.Shared;
using Trax.Dashboard.Tests.Integration.Fakes.Data;
using Trax.Dashboard.Tests.Integration.Fakes.Services;
using Trax.Effect.Enums;
using Trax.Effect.Models.JunctionRun;
using Trax.Effect.Models.Manifest;
using Trax.Effect.Models.Manifest.DTOs;
using Trax.Effect.Models.ManifestGroup;
using Trax.Effect.Models.Metadata;
using Trax.Effect.Models.Metadata.DTOs;
using Trax.Scheduler.Services.TraxScheduler;
using static Trax.Dashboard.Tests.Integration.Utils.GridInteraction;

namespace Trax.Dashboard.Tests.Integration.UnitTests.Components;

/// <summary>
/// The run page's junction timeline: one row per step <c>AddJunctionEvents</c> recorded, read
/// through <c>JunctionRunQueries.ForRun</c> as the API reads it, with a bar placed against the
/// run's start. It shows only the step's own fields: a withheld answer reads "withheld", a failed
/// step shows its failure class and exception type, never a message, and question and answer text
/// is encoded rather than rendered as markup.
/// </summary>
[TestFixture]
public class JunctionTimelineTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private Bunit.TestContext _ctx = null!;
    private InMemoryDataContextFactory _data = null!;

    [SetUp]
    public void SetUp()
    {
        _ctx = new Bunit.TestContext();
        _ctx.Services.AddRadzenComponents();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _data = new InMemoryDataContextFactory();
        _ctx.Services.AddDashboardPageServices(_data);
        _ctx.Services.AddSingleton(UnusedService<ITraxScheduler>.Create());
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    [Test]
    public void A_run_with_no_steps_names_AddJunctionEvents_rather_than_showing_an_empty_box()
    {
        var timeline = Render(Run(TrainState.Completed, ended: 10), []);

        timeline.FindAll(".cs-jt-row").Should().BeEmpty();
        timeline.Find(".cs-jt-hint").TextContent.Should().Contain("AddJunctionEvents()");
    }

    [Test]
    public void A_finished_junction_is_a_bar_placed_against_the_runs_start()
    {
        var timeline = Render(
            Run(TrainState.Completed, ended: 10),
            [Junction(0, "LoadOrder", JunctionRunState.Completed, from: 2, to: 6)]
        );

        var bar = timeline.Find(".cs-jt-bar");
        bar.ClassList.Should().Contain("cs-jt--completed");
        bar.GetAttribute("style").Should().Contain("left: 20%").And.Contain("width: 40%");
        timeline.Find(".cs-jt-name").TextContent.Should().Contain("LoadOrder");
        timeline.Find(".cs-jt-duration").TextContent.Should().Be("4.0s");
    }

    [Test]
    public void A_running_junction_extends_to_now()
    {
        var timeline = Render(
            Run(TrainState.InProgress, ended: null),
            [Junction(0, "Charge", JunctionRunState.InProgress, from: 5, to: null)],
            now: Start.AddSeconds(10)
        );

        var bar = timeline.Find(".cs-jt-bar");
        bar.ClassList.Should().Contain("cs-jt--in-progress");
        bar.GetAttribute("style").Should().Contain("left: 50%").And.Contain("width: 50%");
        timeline.Find(".cs-jt-duration").TextContent.Should().Be("5.0s so far");
    }

    [Test]
    public void A_decision_shows_its_question_answer_confidence_and_replay()
    {
        var step = Decision(1, JunctionRunKind.Choice, "Acme.Routing.Refund");
        step.Answer = "Approve";
        step.Confidence = 0.82;
        step.Replayed = true;

        var timeline = Render(Run(TrainState.Completed, ended: 10), [step]);

        var detail = timeline.Find(".cs-jt-detail").TextContent;
        timeline.Find(".cs-jt-question").TextContent.Should().Be("Acme.Routing.Refund");
        timeline.Find(".cs-jt-answer").TextContent.Should().Be("Approve");
        detail.Should().Contain("82% confidence");
        timeline.FindAll(".cs-jt-replayed").Should().ContainSingle();
        timeline
            .FindAll(".cs-jt-point")
            .Should()
            .ContainSingle("a question is a moment, not a span");
    }

    [Test]
    public void A_withheld_answer_reads_withheld_and_nothing_else()
    {
        // The writer stores neither when the answer is withheld; the page must not show them even
        // if a row carried them.
        var step = Decision(1, JunctionRunKind.Score, "Acme.Sensitive.Salary");
        step.AnswerWithheld = true;
        step.Answer = "MARKER-ANSWER-91234";
        step.Confidence = 0.77;

        var timeline = Render(Run(TrainState.Completed, ended: 10), [step]);

        timeline.Find(".cs-jt-answer").TextContent.Should().Be("withheld");
        timeline.Markup.Should().NotContain("MARKER-ANSWER-91234").And.NotContain("77%");
    }

    [Test]
    public void A_withheld_step_reads_withheld_even_when_handed_an_answer()
    {
        // JunctionStep.From already drops a withheld answer; the component does not rely on it.
        var step = new JunctionStep(
            1,
            JunctionRunKind.Choice,
            "Acme.Sensitive.Plan",
            JunctionRunState.Completed,
            Start.AddSeconds(2),
            null,
            null,
            null,
            null,
            "Acme.Sensitive.Plan",
            "MARKER-ANSWER-7781",
            0.66,
            false,
            null,
            AnswerWithheld: true,
            null
        );

        var timeline = _ctx.RenderComponent<JunctionTimeline>(p =>
            p.Add(x => x.Metadata, Run(TrainState.Completed, ended: 10))
                .Add(x => x.Steps, new[] { step })
                .Add(x => x.Now, Start.AddMinutes(5))
        );

        timeline.Find(".cs-jt-answer").TextContent.Should().Be("withheld");
        timeline.Markup.Should().NotContain("MARKER-ANSWER-7781").And.NotContain("66%");
    }

    [Test]
    public void A_failed_junction_shows_its_failure_class_and_exception_type_only()
    {
        const string marker = "MARKER-EXCEPTION-MESSAGE-5521";
        var run = Run(TrainState.Failed, ended: 10);
        // Nothing of the run's own failure, input or output belongs in the timeline.
        run.Input = marker;
        run.Output = marker;
        run.StackTrace = marker;
        var step = Junction(0, "Charge", JunctionRunState.Failed, from: 1, to: 3);
        step.FailureClass = FailureClass.Transient;
        step.FailureException = "HttpRequestException";

        var timeline = Render(run, [step]);

        timeline
            .Find(".cs-jt-failure")
            .TextContent.Trim()
            .Should()
            .Be("Transient · HttpRequestException");
        timeline.Find(".cs-jt-bar").ClassList.Should().Contain("cs-jt--failed");
        timeline.Markup.Should().NotContain(marker);
    }

    [Test]
    public void Question_and_answer_text_is_encoded_not_rendered_as_markup()
    {
        var step = Decision(1, JunctionRunKind.Route, "<img src=x onerror=alert(1)>");
        step.Answer = "<script>alert('answer')</script>";

        var timeline = Render(Run(TrainState.Completed, ended: 10), [step]);

        timeline.FindAll("script").Should().BeEmpty();
        timeline.FindAll("img").Should().BeEmpty();
        timeline.Find(".cs-jt-answer").TextContent.Should().Be("<script>alert('answer')</script>");
    }

    [Test]
    public void The_runs_attempt_is_shown_when_set()
    {
        var step = Junction(0, "Charge", JunctionRunState.Completed, from: 0, to: 1);
        step.Attempt = 3;

        var timeline = Render(Run(TrainState.Completed, ended: 10), [step]);

        timeline.Find(".cs-jt-attempt").TextContent.Should().Contain("attempt 3");
    }

    [Test]
    public void An_ad_hoc_run_shows_no_attempt()
    {
        var timeline = Render(
            Run(TrainState.Completed, ended: 10),
            [Junction(0, "Charge", JunctionRunState.Completed, from: 0, to: 1)]
        );

        timeline.FindAll(".cs-jt-attempt").Should().BeEmpty();
    }

    [Test]
    public async Task The_run_page_shows_that_runs_steps_in_position_order()
    {
        var runId = await SeedRunAsync(r => r.TrainState = TrainState.Completed);
        var otherId = await SeedRunAsync(r => r.TrainState = TrainState.Completed);
        await using (var db = await _data.CreateDbContextAsync(default))
        {
            foreach (
                var (metadataId, position, name) in new[]
                {
                    (runId, 2, "Ship"),
                    (otherId, 0, "SomeoneElsesStep"),
                    (runId, 0, "Load"),
                    (runId, 1, "Charge"),
                }
            )
            {
                var step = Junction(position, name, JunctionRunState.Completed, 0, 1);
                step.MetadataId = metadataId;
                db.JunctionRuns.Add(step);
            }
            await db.SaveChanges(default);
        }

        var page = _ctx.RenderComponent<MetadataDetailPage>(p => p.Add(x => x.MetadataId, runId));

        page.WaitForAssertion(
            () =>
                page.FindAll(".cs-jt-row .cs-jt-title")
                    .Select(n => n.TextContent.Trim())
                    .Should()
                    .Equal("Load", "Charge", "Ship"),
            WaitTimeout
        );
        page.Markup.Should().NotContain("SomeoneElsesStep");
    }

    [TestCase(500, false, TestName = "A_run_with_500_steps_shows_them_all_without_a_note")]
    [TestCase(501, true, TestName = "A_run_with_more_than_500_steps_says_it_shows_the_first_500")]
    public async Task The_run_page_never_truncates_the_timeline_silently(int count, bool truncated)
    {
        var runId = await SeedRunAsync(r => r.TrainState = TrainState.Completed);
        await using (var db = await _data.CreateDbContextAsync(default))
        {
            for (var i = 0; i < count; i++)
            {
                var step = Junction(i, $"Step{i}", JunctionRunState.Completed, 0, 1);
                step.MetadataId = runId;
                db.JunctionRuns.Add(step);
            }
            await db.SaveChanges(default);
        }

        var page = _ctx.RenderComponent<MetadataDetailPage>(p => p.Add(x => x.MetadataId, runId));

        page.WaitForAssertion(
            () => page.FindAll(".cs-jt-row").Should().HaveCount(500),
            WaitTimeout
        );
        if (truncated)
            page.Find(".cs-jt-truncated")
                .TextContent.Should()
                .Contain("Showing the first 500 steps; this run recorded more.");
        else
            page.FindAll(".cs-jt-truncated").Should().BeEmpty();
    }

    [Test]
    public async Task The_run_page_hints_at_AddJunctionEvents_when_the_run_has_no_steps()
    {
        var runId = await SeedRunAsync(r => r.TrainState = TrainState.Completed);

        var page = _ctx.RenderComponent<MetadataDetailPage>(p => p.Add(x => x.MetadataId, runId));

        page.WaitForAssertion(
            () => page.Find(".cs-jt-hint").TextContent.Should().Contain("AddJunctionEvents()"),
            WaitTimeout
        );
    }

    [Test]
    public async Task A_retry_links_to_the_run_whose_decisions_it_replays()
    {
        // The scheduler's retry and the requeue both set ReplayDecisionsOf; the page shows either
        // the same way.
        var failedId = await SeedRunAsync(r => r.TrainState = TrainState.Failed);
        var retryId = await SeedRunAsync(r =>
        {
            r.TrainState = TrainState.InProgress;
            r.ReplayDecisionsOf = failedId;
        });

        var page = _ctx.RenderComponent<MetadataDetailPage>(p => p.Add(x => x.MetadataId, retryId));

        page.WaitForAssertion(
            () =>
                page.Find($"a[href='trax/data/metadata/{failedId}']")
                    .TextContent.Should()
                    .Be(failedId.ToString()),
            WaitTimeout
        );
        page.Markup.Should().Contain("Replays Decisions Of");
    }

    [Test]
    public async Task A_run_that_replays_nothing_shows_no_replay_link()
    {
        var runId = await SeedRunAsync(r => r.TrainState = TrainState.Completed);

        var page = _ctx.RenderComponent<MetadataDetailPage>(p => p.Add(x => x.MetadataId, runId));

        page.WaitForAssertion(() => page.Markup.Should().Contain("Executor"), WaitTimeout);
        page.Markup.Should().NotContain("Replays Decisions Of");
    }

    [TestCase(true, "Yes")]
    [TestCase(false, "No (retries ask afresh)")]
    public async Task The_manifest_page_shows_whether_retries_replay_decisions(
        bool replay,
        string expected
    )
    {
        long manifestId;
        await using (var db = await _data.CreateDbContextAsync(default))
        {
            var group = new ManifestGroup { Name = $"replay-{replay}" };
            await db.Track(group);
            await db.SaveChanges(default);
            var manifest = Manifest.Create(new CreateManifest { Name = typeof(IReplayingTrain) });
            manifest.ManifestGroupId = group.Id;
            manifest.ReplayDecisionsOnRetry = replay;
            await db.Track(manifest);
            await db.SaveChanges(default);
            manifestId = manifest.Id;
        }

        var page = _ctx.RenderComponent<ManifestDetailPage>(p =>
            p.Add(x => x.ManifestId, manifestId)
        );

        page.WaitForAssertion(
            () => page.Find(".cs-replay-value").TextContent.Should().Be(expected),
            WaitTimeout
        );
    }

    private IRenderedComponent<JunctionTimeline> Render(
        Metadata run,
        IReadOnlyList<JunctionRun> steps,
        DateTime? now = null
    ) =>
        _ctx.RenderComponent<JunctionTimeline>(p =>
            p.Add(x => x.Metadata, run)
                // Mapped as the page maps them, through the API's JunctionStep.
                .Add(x => x.Steps, steps.Select(JunctionStep.From).ToList())
                .Add(x => x.Now, now ?? Start.AddMinutes(5))
        );

    private static Metadata Run(TrainState state, int? ended)
    {
        var run = Metadata.Create(
            new CreateMetadata
            {
                Name = "Acme.IOrderTrain",
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = null,
            }
        );
        run.TrainState = state;
        run.StartTime = Start;
        run.EndTime = ended is { } s ? Start.AddSeconds(s) : null;
        return run;
    }

    private static JunctionRun Junction(
        int position,
        string name,
        JunctionRunState state,
        int from,
        int? to
    ) =>
        new()
        {
            Position = position,
            Kind = JunctionRunKind.Junction,
            Name = name,
            State = state,
            StartedAt = Start.AddSeconds(from),
            EndedAt = to is { } t ? Start.AddSeconds(t) : null,
        };

    private static JunctionRun Decision(int position, JunctionRunKind kind, string key) =>
        new()
        {
            Position = position,
            Kind = kind,
            Name = key,
            QuestionKey = key,
            State = JunctionRunState.Completed,
            StartedAt = Start.AddSeconds(4),
        };

    private async Task<long> SeedRunAsync(Action<Metadata> configure)
    {
        await using var db = await _data.CreateDbContextAsync(default);
        var run = Metadata.Create(
            new CreateMetadata
            {
                Name = typeof(IReplayingTrain).FullName!,
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = null,
            }
        );
        configure(run);
        await db.Track(run);
        await db.SaveChanges(default);
        return run.Id;
    }

    private interface IReplayingTrain;
}
