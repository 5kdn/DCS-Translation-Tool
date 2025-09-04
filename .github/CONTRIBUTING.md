# CONTRIBUTING

このリポジトリへの貢献に関心をお寄せいただきありがとうございます。以下のガイドラインに従って、開発・レビュー・リリースに参加してください。

---

## 方針の要約（重要）

- **リポジトリフロー:** GitHub Flow
- **リポジトリ構成:** 単一リポジトリ（モノレポ）
- **デプロイ対象:** `master` ブランチのみ（常にデプロイ可能状態）
- **マージ方式:** **Squash and Merge** をデフォルト
- **コミット/PRタイトル:** [Conventional Commits](https://www.conventionalcommits.org/ja/v1.0.0/) 準拠
- **`master` への取り込み手順:**
  1. 作業ブランチを作成
  2. 作業ブランチにコミット
  3. Pull Request (PR) を作成
  4. テスト・CI・レビューをクリア
  5. `master` へ **Squash Merge**

---

## 1. 開発環境

| 項目                     | 推奨バージョン / 内容       |
|--------------------------|-----------------------------|
| .NET SDK                 | 8.0.x                       |
| IDE                      | Visual Studio 2022 (17.14+) |
| ターゲットフレームワーク | `net8.0-windows`            |
| C# 言語バージョン        | 12                          |
| 動作OS                   | Windows 11                  |

## 2. Issue と機能要望

- バグ報告や機能提案は [Issues](../../../issues) に登録してください。
- 再現手順、環境、期待動作を明確に記載してください。

## 3. ブランチ運用 (GitHub Flow)

- `master`: 常にデプロイ可能な状態。
- 新機能・修正は `master` から短命ブランチを作成 (`feature/短い説明` や `fix/短い説明`)。
- 作業完了後、Pull Request を作成しレビューを受ける。
- CI が通過しレビュー承認後、`master` にマージ。
- マージ後、必要に応じて即時デプロイ。

## 4. コーディング規約

- **.editorconfig** に従ってください。
- 命名規則: [Microsoft C# ガイドライン](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names) に準拠。
- 可能な限り **nullable reference types** を有効化。
- 非同期コードは `async/await` を使用。
- 公開APIには XML ドキュメントコメントを付与。

## 5. コミットメッセージ

- [Conventional Commits](https://www.conventionalcommits.org/ja/v1.0.0/) に従ってください。
- 形式: `<type>[optional scope]: <description>`
  - type: `feat`, `fix`, `release`, `docs`, `chore`, `refactor`, `perf`, `test`, `build`, `ci`, `style`, `revert`
  - 例: `fix: NullReferenceException in MainWindow`

## 6. ビルドとテスト

```powershell
dotnet clean
dotnet build --configuration Release
dotnet test --configuration Release
```

新機能またはバグ修正には 単体テスト を必ず追加。

UI変更を含む場合はスクリーンショットを PR に添付。

## 7. Pull Request

Pull Request は release-please に従って作成します。

squash-commit として main に取り込まれます。

PR本文に「目的」「変更内容」「テスト方法」を明記してください。

> [!NOTE]
> **PR タイトル** は Squash 時にコミットメッセージとして採用されるため、Conventional Commits に準拠してください。

## 8. ライセンス

提供コードは、このプロジェクトの [LICENSE](../LICENSE) に従います。
