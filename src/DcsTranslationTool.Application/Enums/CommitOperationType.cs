namespace DcsTranslationTool.Application.Enums;

/// <summary>
/// コミットするファイルの操作種別。
/// GitHubへコミットする際、ファイルの追加・更新、または削除を指定する。
/// </summary>
public enum CommitOperationType {
    AddOrUpdate,  // TODO: rename to Upsert
    Delete,
}