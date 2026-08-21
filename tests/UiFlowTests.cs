using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using Gua.Testing;
using Gua.Testing.Godot;
using NUnit.Framework;

namespace GuaUiLab.Tests;

[TestFixture]
[NonParallelizable]
public sealed class UiFlowTests
{
    private const int WideRenderedWidth = 1000;
    private const int WideRenderedHeight = 700;
    private const double DesignWidth = 541.0;
    private const double DesignHeight = 857.0;
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LoadingTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly string ProjectRoot = FindProjectRoot();

    [Test]
    public async Task StartLocksBackUntilLoadingFinishesThenReturnsToPageOne()
    {
        using var host = StartHost();
        var diagnostics = CreateDiagnostics(host);
        using var assertions = GuaAssertionScope.Use(new GuaAssertionOptions
        {
            DiagnosticsSession = diagnostics,
        });

        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "start",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        GuaAssertions.GetById(host.Context, "end").ToBeVisible();

        await GuaAssertions.GetById(host.Context, "start").ClickAsync();
        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "loading",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        await GuaAssertions.WaitForDisabledAsync(
            host.Context,
            "back",
            timeout: ShortTimeout,
            pollInterval: PollInterval);

        var rejection = Assert.ThrowsAsync<GuaActionException>(async () =>
            await GuaAssertions.GetById(host.Context, "back").ClickAsync());
        Assert.That(rejection!.Message, Does.Contain("disabled").IgnoreCase);

        await GuaAssertions.WaitForHiddenAsync(
            host.Context,
            "loading",
            timeout: LoadingTimeout,
            pollInterval: PollInterval);
        await GuaAssertions.WaitForEnabledAsync(
            host.Context,
            "back",
            timeout: ShortTimeout,
            pollInterval: PollInterval);

        await GuaAssertions.GetById(host.Context, "back").ClickAsync();
        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "start",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
    }

    [Test]
    public async Task EndCanBeCanceledOrConfirmed()
    {
        using var host = StartHost(strictTeardown: false);
        var diagnostics = CreateDiagnostics(host);
        using var process = Process.GetProcessById(host.ProcessId);
        using var assertions = GuaAssertionScope.Use(new GuaAssertionOptions
        {
            DiagnosticsSession = diagnostics,
        });

        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "end",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        await GuaAssertions.GetById(host.Context, "end").ClickAsync();
        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "exit_question",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        GuaAssertions.GetById(host.Context, "cancel_exit").ToBeVisible();
        GuaAssertions.GetById(host.Context, "confirm_exit").ToBeVisible();

        await GuaAssertions.GetById(host.Context, "cancel_exit").ClickAsync();
        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "end",
            timeout: ShortTimeout,
            pollInterval: PollInterval);

        await GuaAssertions.GetById(host.Context, "end").ClickAsync();
        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "confirm_exit",
            timeout: ShortTimeout,
            pollInterval: PollInterval);
        try
        {
            await GuaAssertions.GetById(host.Context, "confirm_exit").ClickAsync();
        }
        catch (WebSocketException)
        {
            // The expected application exit can close the bridge before the
            // correlated click result reaches the test process. The process
            // exit assertion below distinguishes this from a lost connection.
        }

        await WaitForProcessExitAsync(process, ShortTimeout);
        Assert.That(process.HasExited, Is.True);
    }

    [Test]
    public async Task WideWindowKeepsTheDesignCenteredAtItsOriginalAspectRatio()
    {
        using var host = StartHost(
            rendered: true,
            additionalArguments:
            [
                "--resolution",
                $"{WideRenderedWidth}x{WideRenderedHeight}",
                "--position",
                "0,0",
            ]);
        var diagnostics = CreateDiagnostics(host);
        using var assertions = GuaAssertionScope.Use(new GuaAssertionOptions
        {
            DiagnosticsSession = diagnostics,
        });

        await GuaAssertions.WaitForVisibleAsync(
            host.Context,
            "start",
            timeout: ShortTimeout,
            pollInterval: PollInterval);

        await host.WaitForScreenshotAsync(TimeSpan.FromSeconds(5));
        var screenshot = host.GetScreenshot();
        Assert.Multiple(() =>
        {
            Assert.That(screenshot.Width, Is.EqualTo(WideRenderedWidth));
            Assert.That(screenshot.Height, Is.EqualTo(WideRenderedHeight));
        });

        using var tree = JsonDocument.Parse(host.Context.GetUiTreeJson());
        var root = tree.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("id").GetString() == "root");
        var bounds = root.GetProperty("bounds");
        var expectedScale = Math.Min(
            WideRenderedWidth / DesignWidth,
            WideRenderedHeight / DesignHeight);
        var expectedRenderedWidth = DesignWidth * expectedScale;
        var expectedRenderedHeight = DesignHeight * expectedScale;
        var expectedX = (WideRenderedWidth - expectedRenderedWidth) * 0.5;
        var expectedY = (WideRenderedHeight - expectedRenderedHeight) * 0.5;

        Assert.Multiple(() =>
        {
            Assert.That(bounds.GetProperty("x").GetDouble(), Is.EqualTo(expectedX).Within(1.0));
            Assert.That(bounds.GetProperty("y").GetDouble(), Is.EqualTo(expectedY).Within(1.0));
            Assert.That(bounds.GetProperty("w").GetDouble(), Is.EqualTo(DesignWidth).Within(1.0));
            Assert.That(bounds.GetProperty("h").GetDouble(), Is.EqualTo(DesignHeight).Within(1.0));
        });
    }

    private static GodotSceneTestHost StartHost(
        bool strictTeardown = true,
        bool rendered = false,
        IReadOnlyList<string>? additionalArguments = null)
    {
        var options = new GodotSceneTestHostOptions
        {
            ProjectPath = ProjectRoot,
            UseAvailableBridgePort = true,
            StartupResetPolicy = GuaResetPolicy.Strict,
            TeardownResetPolicy = strictTeardown
                ? GuaResetPolicy.Strict
                : GuaResetPolicy.Disabled,
            CaptureDiagnosticsBeforeTeardown = true,
            CleanupAfterLeakReport = true,
            AdditionalArguments = additionalArguments ?? [],
        };

        return rendered
            ? GodotSceneTestHost.LoadRendered("res://main.tscn", options)
            : GodotSceneTestHost.Load("res://main.tscn", options);
    }

    private static GuaDiagnosticsSession CreateDiagnostics(GodotSceneTestHost host)
    {
        return host.CreateDiagnosticsSession(
            TestContext.CurrentContext.Test.FullName,
            outputDirectory: Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "artifacts",
                "gua"));
    }

    private static async Task WaitForProcessExitAsync(Process process, TimeSpan timeout)
    {
        using var timeoutCancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (!process.HasExited)
        {
            throw new TimeoutException(
                $"Godot process {process.Id} did not exit within {timeout}.");
        }
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
