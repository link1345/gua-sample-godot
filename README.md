# Gua UI Lab

This is a Godot GDScript sample project for [Gua](https://github.com/link1345/gua). Its UI and tests were created through conversations with AI such as the following:

https://youtube.com/live/25UtypMUlcg?feature=share

> “Use the provided assets to create a two-screen UI that can be operated through Gua.”
>
> “Disable Back while loading, and add an exit confirmation dialog.”
>
> “Preserve the design when the window size changes.”
>
> “Create UI tests using Gua and NUnit.”

This two-screen UI sample runs on Godot 4.7. `Start` opens the second screen, where `Back` remains disabled during a six-second Loading state. After loading finishes, `Back` returns to the first screen. `End` opens an exit confirmation dialog; `Cancel` closes it, and `OK` exits the application.

The window is resizable. The UI scales uniformly while preserving its 541×857 design aspect ratio, and any space outside that ratio is rendered as black letterboxing.

UI automation uses the [Gua](https://github.com/link1345/gua) v0.15.0 Godot GDScript add-on. At runtime, the Gua bridge listens on `ws://127.0.0.1:8765` and automatically exposes the standard Godot Control tree.

```powershell
Godot_v4.7-stable_win64.exe --path .
```

## UI tests

The NUnit tests use `Gua.Testing.Godot` to operate a real Godot process through its Semantic UI Tree.

```powershell
$env:GODOT_EXECUTABLE = "Godot_v4.7-stable_win64_console.exe"
dotnet test tests\GuaUiLab.Tests.csproj
```

On failure, the Gua UI Tree, logs, and Godot standard output and standard error are saved under `artifacts/gua` in the test output directory.

### Visual and recording validation

`Gua.Testing.Visual` compares the initial screen with a reviewed PNG baseline to detect missing images, layout shifts, and unintended overlays. Only update the baseline intentionally after reviewing the difference:

```powershell
$env:GUA_UPDATE_BASELINES = "1"
dotnet test tests\GuaUiLab.Tests.csproj --filter TitleScreenMatchesReviewedVisualBaseline
Remove-Item Env:GUA_UPDATE_BASELINES
```

`Gua.Testing.Recording` records real semantic interactions for `End` → `Cancel`, saves them to JSON, reloads them, and replays them in the same Godot session. Successful recording files are also saved under `artifacts/gua/recordings`.

## GitHub Actions

`.github/workflows/gua-tests.yml` uses the Godot Action from [`link1345/gua-tester`](https://github.com/link1345/gua-tester) v2 (`link1345/gua-tester/godot@v2`) to run UI tests against a real Godot process on pushes to `master` and on pull requests. CI downloads the same published Gua v0.15.0 add-on used by the NuGet packages, so the repository does not need to include its DLLs.

### Visual difference viewer

When a visual comparison fails in a pull request, `visual-report@v2` turns `comparison.json` and its PNGs into an Astro-based static viewer and stores it as the normal `gua-visual-report` Actions artifact. The viewer provides three-column Expected, Diff, and Actual images as well as an Expected/Actual comparison slider.

On pushes to `master` and manual runs, the latest report is uploaded as a GitHub Pages artifact and deployed by a dedicated job. Configure the repository's Pages source as **GitHub Actions** first.

Enable `visual-report-demo` in a manual run to have `VisualReportViewerDemoProducesPixelDifferenceArtifact` compare same-size title and loading screens and generate an intentional pixel difference. The test succeeds after verifying the expected comparison failure, while the workflow marks only the outcome passed to the viewer as `failure`, so all three images and the slider can be inspected.

To inspect a downloaded artifact locally, serve the extracted directory over HTTP because the viewer loads `report.json` through a relative URL instead of opening `index.html` directly:

```powershell
python -m http.server 8000
```

> [!WARNING]
> Screenshots may contain secrets or personal information rendered inside the game. Review what will be published before enabling Pages, especially in a public repository.
