# DCS Translation Tool

DCS: World の日本語化を支援する **非公式** Windows用デスクトップアプリケーションです。

翻訳ファイルの取得・適用・投稿が1つのアプリで実行できます。

## 主な機能

- [翻訳ファイルのリポジトリ](https://github.com/5kdn/DCS-Translation-Japanese) から翻訳ファイルを取得する。
- 翻訳ファイルを `.miz` / `.trk` / `*.lua` に適用する。
- ローカルで変更した翻訳ファイルをリポジトリへアップロードする。

## 動作環境

- OS: Windows 7以上（win-x64 / win-x86）
- Runtime: 不要（.NET ランタイム同梱）

## 使い方

### 1. 初期設定

設定ページで以下のフォルダーを指定してください。

- `Aircraft` フォルダー（`<DCS World>/Mods/aircraft` 相当）
- `Campaigns` フォルダー（`<DCS World>/Mods/campaigns` 相当）
- `ユーザーフォルダー`（`Saved Games/DCS/Missions` 相当）
- `翻訳ファイルダウンロードフォルダー`

### 2. 翻訳ファイルを取得/適用する（Download）

- Download ページで `Fetch` を実行して最新状態を取得する。
- 必要なファイルを選択する。
- ダウンロードのみの場合は `DL` を押す。
- `.miz/.trk` へ適用する場合は `Apply` を押す。

### 3. 翻訳ファイルを投稿する（Upload）

- Upload ページで `Fetch` を実行し、差分データを取得する。
- 投稿対象のファイルを選択して `PRを作成` を押す。
- 必要な情報を入力して作成する。

## インストールとアンインストール

### インストール

インストールは不要です。

> [!NOTE]
> 「**Windows によって PC が保護されました**」と表示される場合<br/>
> 本アプリは未署名の実行ファイルであるため、初回起動時に Windows SmartScreen によってブロックされる場合があります。<br/>
> これはウイルスではなく、Microsoft にコード署名証明書が登録されていない新しいアプリに対して Windows が安全確認を行う仕組みです。<br/>
> → 「詳細情報」 をクリック<br/>
> → 「実行」 ボタンを押してください。

### アンインストール

以下を削除してください。

- `DCS-Translation-Tool.exe`
- `%APPDATA%\DcsTranslationTool.Presentation.Wpf`（設定ファイル）
- `%TEMP%\.net\DCS-Translation-Tool`（自己展開領域）

## 開発者向け

リポジトリルートで実行する。

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --no-restore
dotnet format --no-restore --verify-no-changes
```

アプリ起動:

```powershell
dotnet run --project src/DcsTranslationTool.Presentation.Wpf/DcsTranslationTool.Presentation.Wpf.csproj
```

## ドキュメント

- [CHANGELOG](./CHANGELOG.md)
- [PRIVACY](./PRIVACY.md)
- [License (MIT)](./LICENSE)
- [Contributing](.github/CONTRIBUTING.md)
- [Code of Conduct](.github/CODE_OF_CONDUCT.md)
- [Security Policy](.github/SECURITY.md)

## 関連リポジトリ

- [DCS-Translation-Japanese](https://github.com/5kdn/DCS-Translation-Japanese)
- [DCS-Translation-Japanese-Downloader](https://github.com/5kdn/DCS-Translation-Japanese-Downloader)
- [DCS-Translation-Japanese-Cloudflare-Worker](https://github.com/5kdn/DCS-Translation-Japanese-Cloudflare-Worker)

## 注意事項

- 本ツールは **非公式** です。DCS: World の利用規約 / EULA の範囲でご利用ください。
- `.miz` / `.trk` `.lua` への適用前にバックアップを作成してから実行してください。
- 本ソフトウェアの使用または使用不能に起因する損害についても、開発者および著作権者は一切の責任を負いません。
