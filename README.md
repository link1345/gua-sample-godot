# Gua UI Lab

Godot 4.7で動作する2画面のUIサンプルです。`Start`で2画面目へ移動し、6秒間のLoading中は`Back`が無効になります。Loading終了後は`Back`で1画面目へ戻れます。`End`では終了確認が表示され、`Cancel`で戻り、`OK`で終了します。

ウィンドウはリサイズ可能です。541×857のデザイン比率を維持して一様に拡大・縮小し、画面比率から余る領域は黒いレターボックスとして表示します。

UI自動化には[Gua](https://github.com/link1345/gua) v0.12.0のGodot GDScriptアドオンを使用しています。実行中は `ws://127.0.0.1:8765` でGua bridgeが待ち受け、標準Godot Controlツリーを自動的に公開します。

```powershell
C:\Users\testk\.local\bin\Godot_v4.7-stable_win64.exe --path .
```

## UIテスト

NUnitから`Gua.Testing.Godot`を使い、実際のGodotプロセスとSemantic UI Treeを操作します。

```powershell
$env:GODOT_EXECUTABLE = "C:\Users\testk\.local\bin\Godot_v4.7-stable_win64_console.exe"
dotnet test tests\GuaUiLab.Tests.csproj
```

失敗時のGua UI Tree、ログ、Godot標準出力・標準エラーなどは、テスト出力ディレクトリの`artifacts/gua`に保存されます。
