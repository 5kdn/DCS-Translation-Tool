# テストレビュー（2025-10-24）

## 概要

- 依然として PR 作成フローやアップロード／ダウンロードの中核ロジックは自動化されておらず、回帰検知には追加投資が必要である。

## 重要な不足箇所

1. **Medium**: `src/DcsTranslationTool.Presentation.Wpf/Features/CreatePullRequest/CreatePullRequestViewModel.cs:1`  
  - 同意・キャンセル・API 失敗はテスト化したが、成功シナリオ・ファイル読み込み失敗・バイナリ判定といったハッピーパス／ガード条件が未検証。GitHub API 正常応答時のダイアログ完了挙動を抑えて事故リリースを防ぎたい。
2. **High**: `src/DcsTranslationTool.Presentation.Wpf/Features/Download/DownloadViewModel.cs:1`  
  - マニフェスト欠落時のガードはテスト化したが、正常系の ZIP 展開・`ProcessApplyAsync`・差分マージ・再ダウンロード時の ETag／`CanDownload` 判定が依然未検証。ファイル破損や部分適用の退行を防ぐため分岐網羅が必要。
3. **High**: `src/DcsTranslationTool.Presentation.Wpf/Features/Upload/UploadViewModel.cs:ActivateAsync`  
  - Fetch 正常／失敗・監視開始まではカバーできたものの、`ShowCreatePullRequestDialog` の確認ダイアログや `CommitFiles` 実行分岐、Snackbar 通知の失敗経路は未テスト。複合操作の順序を検証する統合ユニットテストが求められる。
4. **Low**: `src/DcsTranslationTool.Presentation.Wpf/Features/Settings/SettingsViewModel.cs:1`  
  - ディレクトリ選択の分岐は主要パスをカバーしたが、ライセンス／プライバシーリンク操作やキャンセル時のログ確認は未検証。副作用を伴うナビゲーション呼び出しをモックで監視するテストを追加したい。

## 推奨アクション

1. CreatePullRequest フローの成功経路とファイル読み込み例外を再現するユニットテストを追加し、GitHub API 正常応答時のダイアログ結果とバイナリ拒否ガードを検証する。
2. DownloadViewModel の正常系／部分適用／失敗復旧を対象に ZIP 展開・ファイル出力・ETag 管理を検証するテストケースを増補する。
