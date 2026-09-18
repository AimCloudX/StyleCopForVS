# StyleCop Classic for Visual Studio

Visual Studio 2022 (17.x) / Visual Studio 2026 (18.x) 用の VSIX 拡張です。C# Editor の現在のバッファを StyleCop Classic 4.7 系で解析し、違反を `Warning` の `ErrorTag` として表示します。

## ビルド

Visual Studio の「拡張機能の開発」ワークロード、または対応する .NET SDK が必要です。

```powershell
dotnet restore
dotnet build -c Release -p:Platform=x64
```

生成物は `bin\Release\net472\StyleCopForVS.vsix` です。VSIX をインストールして C# ファイルを開くと、編集後 350ms のデバウンスを経て波線が更新されます。

プロジェクトまたは親フォルダーの `Settings.StyleCop` を読み込みます。設定ファイルが見つからない場合は、VSIX に含まれる既定設定を使用します。

## 動作確認用ソリューション

`StyleCopErrorSample.sln` は、空行ルール違反を意図的に含む確認用プロジェクトです。

1. VSIX をインストールします。
2. `StyleCopErrorSample.sln` を Visual Studio 2022 または Visual Studio 2026 で開きます。
3. `samples\StyleCopErrorSample\ErrorSample.cs` を開きます。
4. `SA1505`、`SA1508`、`SA1509`、`SA1513`、`SA1516` などの波線を確認します。

同じフォルダーの `Settings.StyleCop` により、レイアウトルールが有効化され、StyleCop 上は違反がエラー扱いになります。

## 制約

StyleCop Classic 4.7 は独自パーサーのため、C# の最新構文をすべて解釈できるとは限りません。解析できない編集中の状態では、Visual Studio を止めないよう診断を一時的にクリアします。

この拡張はビルド時に StyleCop の設定やプロジェクトを変更しません。Editor の未保存テキストも解析対象です。
