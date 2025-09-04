# DCS Translation Tool

DCS: World の日本語化をサポートする**非公式**ツールです。

## 主な機能

- [翻訳ファイルのリポジトリ](https://github.com/5kdn/DCS-Translation-Japanese) から翻訳ファイルをダウンロード
- 翻訳ファイルを `.miz` ファイルへ注入
- ユーザーが作成した翻訳ファイルをリポジトリへアップロード

### 翻訳ファイルをダウンロード・mizファイルへ注入する

> [!NOTE]
> ダウンロードを行う前に設定ページからフォルダの設定を行ってください

- Download ページを開く
- 必要なファイルにチェックを付ける
- mizファイルに翻訳を注入する場合
   - 「適用」 ボタンを押す
- 翻訳ファイルをダウンロードだけする場合
   - 「DL」 ボタンを押す

### 自作・修正した翻訳ファイルをアップロードする

> [!NOTE]
> アップロードを行う前に設定ページからフォルダの設定を行ってください

- Upload ページを開く
- アップロードまたは削除したいファイルにチェックを付ける
- 「PRを作成」 ボタンを押す
- 変更する理由を記述
- 「アップロード」 ボタンを押す

## インストールとアンインストール

### インストール

インストールは不要です

> [!NOTE]
> 「**Windows によって PC が保護されました**」と表示される場合<br/>
> 本アプリは未署名の実行ファイルであるため、初回起動時に Windows SmartScreen によってブロックされる場合があります。<br/>
> これはウイルスではなく、Microsoft にコード署名証明書が登録されていない新しいアプリに対して Windows が安全確認を行う仕組みです。<br/>
> → 「詳細情報」 をクリック<br/>
> → 「実行」 ボタンを押す

### アンインストール

以下を削除してください

- `DCS-Translation-Tool.exe` (本ソフトウェア)
- `%APPDATA%\DcsTranslationTool.Presentation.Wpf` (設定ファイル保存領域)
- `%TEMP%\.net\DCS-Translation-Tool` (ソフトウェア展開領域)

---

## ライセンス / 行動規範 他

- **License**: [MIT](./LICENSE)
- **Contributing**: [CONTRIBUTING.md](.github/CONTRIBUTING.md)
- **Code of Conduct**: [CODE_OF_CONDUCT.md](.github/CODE_OF_CONDUCT.md)
- **Security Policy**: [SECURITY.md](.github/SECURITY.md)

## 注意事項

- 本ツールは **非公式** です。DCS: World の利用規約/EULA の範囲でご利用ください。
- `.miz` への注入動作はバックアップを作成してから実行してください。
- 本ソフトウェアの使用または使用不能に起因して生じたいかなる損害についても、開発者および著作権者は一切の責任を負いません。
