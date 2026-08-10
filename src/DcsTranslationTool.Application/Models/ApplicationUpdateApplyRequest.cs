namespace DcsTranslationTool.Application.Models;

/// <summary>
/// 更新専用プロセスへ引き渡す適用要求を表す。
/// </summary>
/// <param name="ApplicationDirectory">更新対象の配布ルートを受け取る。</param>
/// <param name="StagingDirectory">検証済みステージングルートを受け取る。</param>
/// <param name="BackupDirectory">ロールバック用バックアップルートを受け取る。</param>
/// <param name="ParentProcessId">終了を待機する親プロセス ID を受け取る。</param>
/// <param name="RuntimeIdentifier">更新対象 RID を受け取る。</param>
/// <param name="TargetVersion">更新先バージョンを受け取る。</param>
public sealed record ApplicationUpdateApplyRequest(
    string ApplicationDirectory,
    string StagingDirectory,
    string BackupDirectory,
    int ParentProcessId,
    string RuntimeIdentifier,
    string TargetVersion
);