# Gua UI Lab for Godot

日本語 | [English](README.md)

これは[Gua](https://github.com/link1345/gua)のGodot GDScript版サンプルプログラムです。AIとの次のようなやり取りを通じて、UIの実装からテストまで作られています。

https://youtube.com/live/25UtypMUlcg?feature=share

> 「素材を使って2画面のUIを作り、Guaで操作できるようにしてください」
>
> 「Loading中はBackを無効にし、終了確認ダイアログも追加してください」
>
> 「画面サイズが変わってもデザインを維持してください」
>
> 「GuaとNUnitを使ったUIテストを作ってください」

Godot 4.7で動作する2画面のUIサンプルです。`Start`で2画面目へ移動し、6秒間のLoading中は`Back`が無効になります。Loading終了後は`Back`で1画面目へ戻れます。`End`では終了確認が表示され、`Cancel`で戻り、`OK`で終了します。

ウィンドウはリサイズ可能です。541×857のデザイン比率を維持して一様に拡大・縮小し、画面比率から余る領域は黒いレターボックスとして表示します。

UI自動化には[Gua](https://github.com/link1345/gua) v0.15.0のGodot GDScriptアドオンを使用しています。実行中は `ws://127.0.0.1:8765` でGua bridgeが待ち受け、標準Godot Controlツリーを自動的に公開します。

```powershell
Godot_v4.7-stable_win64.exe --path .
```

## UIテスト

NUnitから`Gua.Testing.Godot`を使い、実際のGodotプロセスとSemantic UI Treeを操作します。

```powershell
$env:GODOT_EXECUTABLE = "Godot_v4.7-stable_win64_console.exe"
dotnet test tests\GuaUiLab.Tests.csproj
```

失敗時のGua UI Tree、ログ、Godot標準出力・標準エラーなどは、テスト出力ディレクトリの`artifacts/gua`に保存されます。

### Visual / Recording検証

`Gua.Testing.Visual` で初期画面をレビュー済みPNGと比較し、画像欠落、配置ずれ、意図しないオーバーレイを検出します。基準画像を意図的に更新する場合だけ、差分を確認した上で次を実行します。

```powershell
$env:GUA_UPDATE_BASELINES = "1"
dotnet test tests\GuaUiLab.Tests.csproj --filter TitleScreenMatchesReviewedVisualBaseline
Remove-Item Env:GUA_UPDATE_BASELINES
```

`Gua.Testing.Recording` では `End` → `Cancel` のセマンティック操作を実際に記録し、JSONへの保存・再読込・同一Godotセッションへの再生まで検証します。成功時の記録JSONも `artifacts/gua/recordings` に保存されます。

## GitHub Actions

`.github/workflows/gua-tests.yml` で [`link1345/gua-tester`](https://github.com/link1345/gua-tester) v2.1のGodot Action（`link1345/gua-tester/godot@v2.1`）を使用し、`master` へのpushとpull requestで実際のGodotプロセスを操作するUIテストを実行します。CIではNuGetパッケージと同じGua v0.15.0の公開アドオンをダウンロードするため、DLLをリポジトリに含める必要はありません。

### Visual差分Viewer

pull requestでVisual比較が失敗すると、`visual-report@v2.1`が`comparison.json`とPNGをAstro製の静的Viewerへ変換し、`gua-visual-report`という通常のActions artifactとして保存します。ViewerにはExpected／Diff／Actualの3列表示とExpected／Actual比較スライダーがあります。

`master`へのpushと手動実行では、最新結果をGitHub Pages artifactとしてアップロードし、専用jobからPagesへdeployします。repositoryのPages sourceを事前に **GitHub Actions** へ設定してください。

手動実行で`visual-report-demo`を有効にすると、`VisualReportViewerDemoProducesPixelDifferenceArtifact`が同じ解像度のtitle画面とloading画面を比較し、意図的なpixel差分を生成します。テスト自体は期待した比較失敗を検証して成功し、workflowはViewerへ渡すoutcomeだけを`failure`にするため、3画像とスライダーを実際に確認できます。

通常artifactをダウンロードしてローカル確認する場合、Viewerは相対URLの`report.json`を読み込むため、`index.html`を直接開かず展開先をHTTP配信します。

```powershell
python -m http.server 8000
```

> [!WARNING]
> screenshotにはゲーム画面へ描画された秘密情報や個人情報が含まれる可能性があります。特にpublic repositoryでPagesを有効にする前に、公開内容を確認してください。
