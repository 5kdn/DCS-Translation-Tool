# エージェント向けガイド

## 技術スタック
- .NET 8.0 (win-x64) と WPF / XAML で構築されたデスクトップ アプリケーションです。
- MVVM フレームワークとして Caliburn.Micro を採用し、`Bootstrapper` で DI コンテナー (SimpleContainer) を初期化します。
- GitHub 連携は Octokit + GitHub App 認証 (`GitHubJwt`) を用いて実装しています。
- ロギングは NLog を `LoggingService` でラップして利用し、`Logs/` にローテーション付きで出力します。

## ソリューション構成
- `DcsTranslationTool` : UI 層。Caliburn.Micro の ViewModel / View、アプリ固有サービス、プロバイダーを配置します。
  - `Assets/Styles` : Material Design 3 ベースのリソース辞書。
  - `Contracts` : UI/インフラ層のインターフェース。
  - `Constants`・`Enums`・`Extensions` など : UI 専用の定数・列挙・拡張メソッド。
  - `Providers` : フォルダー選択や環境情報など OS 連携機能。
  - `Securities` : GitHub App 用の AES-GCM 復号器などセキュリティ関連。
  - `Services` : GitHub API クライアント、ロギング、スナックバーなど UI から直接利用するサービス。
  - `ViewModels` / `Views` : ダウンロード・アップロード・設定・PR 作成ページの MVVM 実装。
- `DcsTranslationTool.Core` : ドメイン/ユースケース層。ファイル差分管理、Zip 展開、GitHub リポジトリ操作の純ロジックを保持します。
- `BuildTasks` : GitHub App の秘密鍵などを暗号化/復号する MSBuild タスクを格納します。アプリ本体は `IDecryptService` を通じて利用します。

## プロダクト機能の押さえどころ
- 対象リポジトリは `5kdn/DCS-Translation-Japanese` (`TargetRepository`) です。ダウンロード/アップロード/PR 作成はこのリポジトリ前提で実装されています。
- `DownloadViewModel` と `UploadViewModel` はローカル翻訳ディレクトリ (`AppSettings.TranslateFileDir`) を監視し、GitHub 上のファイル一覧との差分を `FileEntryService` で算出します。
- フィルター (`FilterViewModel`) やタブ (`TabItemViewModel`) で変更種別ごとの表示を切り替えられるようになっています。
- `CreatePullRequestViewModel` はブランチ作成 → コミット → PR 作成をまとめて実行するフローを持ちます。GitHub App の資格情報が正しく暗号化されているかを事前に確認してください。
- アプリ設定 (`AppSettingsService`) は `%AppData%/<AssemblyTitle>/appsettings.json` に保存され、起動時にロード・終了時に永続化されます。

## 開発ワークフロー
- 基本コマンド
  - `dotnet restore DcsTranslationTool.sln`
  - `dotnet build DcsTranslationTool.sln -c Debug`
  - `dotnet run --project DcsTranslationTool/DcsTranslationTool.csproj`
- リリースビルドは `dotnet publish DcsTranslationTool/DcsTranslationTool.csproj -c Release -r win-x64 --self-contained` を想定しています。
- GitHub App の秘密鍵などは `Directory.Build.props.user` でローカル上書きし、実ファイルは Git にコミットしません。
- `BuildTasks` プロジェクトは MSBuild 実行時に自動で読み込まれるため、通常は個別でビルドする必要はありません。

## テストと検証
- 現在自動テスト プロジェクトは未整備です。主な動作確認は次の観点で手動検証してください。
  - 翻訳ディレクトリ監視: ファイルの追加/更新/削除が UI に反映されるか。
  - GitHub API 呼び出し: ブランチ作成・コミット・PR 作成が想定どおり成功するか (サンドボックス環境推奨)。
  - Zip 展開/暗号化復号: `IZipService` と `IDecryptService` の境界で例外が発生しないか。
- テスト追加時は xUnit + FluentAssertions の採用を想定し、Caliburn.Micro 依存コードは STA 実行でカバーしてください。

## コーディング規約
- `.editorconfig` に従い **CRLF** / 4 スペース / 末尾空白なしを維持します。`dotnet format --verify-no-changes` で静的解析を通してください。
- 名前空間はファイル スコープ、明示的な型名を基本とし、型が明白な場合のみ `var` を使用します。
- ドキュメントコメントは「〜する」調で統一します。
- プライマリ コンストラクタを積極的に利用し、依存関係の注入はコンストラクタ引数で完結させます。
- ロギングは `ILoggingService` を経由し、例外ハンドリングでは `Result` (FluentResults) を活用して呼び出し側でメッセージ制御できるようにしてください。

## CI / リリース
- GitHub Actions
  - `.github/workflows/publish-to-GitHub-Release.yml` : リリースビルドと GitHub Release 連携。
  - `.github/workflows/release-please.yml` : バージョニングとリリース PR の自動生成。
- `release-please` の設定は `release-please-config.json` と `.release-please-manifest.json` で管理します。
- リポジトリ方針は GitHub Flow + Squash merge。`master` ブランチは常にデプロイ可能な状態を維持してください。
