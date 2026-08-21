using Gua.Testing;
using Gua.Testing.Godot;
using Gua.Testing.Recording;
using Gua.Testing.Visual;
using NUnit.Framework;

namespace GuaUiLab.Tests;

[TestFixture]
[NonParallelizable]
public sealed class AdvancedValidationTests
{
    private static readonly string VisualVariant =
        Environment.GetEnvironmentVariable("GUA_VISUAL_VARIANT")
        ?? "windows-godot-4.7-gl-compatibility";
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly string ProjectRoot = FindProjectRoot();

    [Test]
    public async Task TitleScreenMatchesReviewedVisualBaseline()
    {
        using var host = StartHost(rendered: true);
        using var assertions = CreateAssertionScope(host);

        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "start",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        await host.WaitForScreenshotAsync(ShortTimeout);
        var screenshot = host.GetScreenshot();

        var result = await GuaVisualAssertions.ExpectScreenshotAsync(
            host.Context,
            "title-screen",
            new ScreenshotOptions
            {
                BaselineDirectory = Path.Combine(ProjectRoot, "tests", "baselines"),
                ArtifactDirectory = Path.Combine(
                    TestContext.CurrentContext.WorkDirectory,
                    "artifacts",
                    "gua"),
                BaselineVariant = VisualVariant,
                PixelThreshold = 0.02f,
                MaxDifferentPixelRatio = 0.001,
                WaitForStableSnapshot = true,
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.Matched, Is.True);
            Assert.That(screenshot.Width, Is.EqualTo(541));
            Assert.That(screenshot.Height, Is.EqualTo(857));
        });
    }

    [Test]
    public async Task RecordedEndCancelJourneyRoundTripsAndReplays()
    {
        using var host = StartHost();
        using var assertions = CreateAssertionScope(host);

        var recorder = new GuaRecorder(host.Context);
        await recorder.ClickAsync(
            new(Id: "end"),
            waitCondition: GuaWaitConditions.Visible("end"),
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        await recorder.ClickAsync(
            new(Id: "cancel_exit"),
            waitCondition: GuaWaitConditions.Visible("cancel_exit"),
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "end",
            timeout: ShortTimeout,
            pollInterval: PollInterval);

        var recordingDirectory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "artifacts",
            "gua",
            "recordings");
        var recordingPath = Path.Combine(recordingDirectory, "end-cancel.json");
        GuaRecordingFile.Save(recordingPath, recorder.Recording);
        TestContext.AddTestAttachment(recordingPath, "Recorded Gua end/cancel journey");

        var recording = GuaRecordingFile.Load(recordingPath);
        var replay = await GuaReplayer.ReplayAsync(
            host.Context,
            recording,
            new GuaReplayOptions
            {
                TimingMode = GuaReplayTimingMode.PreferConditions,
                ActionTimeout = ShortTimeout,
                PollInterval = PollInterval,
            });

        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "end",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        await GuaAssertions.WaitForHiddenAsync(
            host.Context,
            "exit_question",
            timeout: ShortTimeout,
            pollInterval: PollInterval);

        Assert.Multiple(() =>
        {
            Assert.That(recording.SchemaVersion, Is.EqualTo(1));
            Assert.That(recording.Steps, Has.Count.EqualTo(2));
            Assert.That(replay.Steps, Has.Count.EqualTo(2));
            Assert.That(
                replay.Steps.All(step => step.Completion is { Succeeded: true }),
                Is.True);
        });
    }

    private static GodotSceneTestHost StartHost(bool rendered = false)
    {
        var options = new GodotSceneTestHostOptions
        {
            ProjectPath = ProjectRoot,
            UseAvailableBridgePort = true,
            StartupResetPolicy = GuaResetPolicy.Strict,
            TeardownResetPolicy = GuaResetPolicy.Strict,
            CaptureDiagnosticsBeforeTeardown = true,
            CleanupAfterLeakReport = true,
        };

        return rendered
            ? GodotSceneTestHost.LoadRendered("res://main.tscn", options)
            : GodotSceneTestHost.Load("res://main.tscn", options);
    }

    private static IDisposable CreateAssertionScope(GodotSceneTestHost host)
    {
        var diagnostics = host.CreateDiagnosticsSession(
            TestContext.CurrentContext.Test.FullName,
            outputDirectory: Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "artifacts",
                "gua"));
        return GuaAssertionScope.Use(new GuaAssertionOptions
        {
            DiagnosticsSession = diagnostics,
        });
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find project.godot above the NUnit test output directory.");
    }
}
