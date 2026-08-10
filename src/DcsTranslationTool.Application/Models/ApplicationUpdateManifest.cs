namespace DcsTranslationTool.Application.Models;

/// <summary>
/// 配布物の管理対象ファイル一覧を表す。
/// </summary>
/// <param name="SchemaVersion">マニフェストスキーマのバージョンを受け取る。</param>
/// <param name="ApplicationId">アプリケーション識別子を受け取る。</param>
/// <param name="Version">配布バージョンを受け取る。</param>
/// <param name="RuntimeIdentifier">配布 RID を受け取る。</param>
/// <param name="EntryPoint">起動実行ファイルの相対パスを受け取る。</param>
/// <param name="Files">管理対象ファイルを受け取る。</param>
public sealed record ApplicationUpdateManifest(
    int SchemaVersion,
    string ApplicationId,
    string Version,
    string RuntimeIdentifier,
    string EntryPoint,
    IReadOnlyList<ApplicationUpdateManifestFile> Files
);

/// <summary>
/// 配布マニフェスト内の管理対象ファイルを表す。
/// </summary>
/// <param name="Path">配布ルートからの相対パスを受け取る。</param>
/// <param name="Size">ファイルサイズを受け取る。</param>
/// <param name="Sha256">小文字16進数の SHA-256 を受け取る。</param>
public sealed record ApplicationUpdateManifestFile(
    string Path,
    long Size,
    string Sha256
);

/// <summary>
/// 直前版から更新先を再構築する差分マニフェストを表す。
/// </summary>
/// <param name="SchemaVersion">差分スキーマのバージョンを受け取る。</param>
/// <param name="ApplicationId">アプリケーション識別子を受け取る。</param>
/// <param name="BaseVersion">差分の基準バージョンを受け取る。</param>
/// <param name="TargetVersion">差分の更新先バージョンを受け取る。</param>
/// <param name="RuntimeIdentifier">配布 RID を受け取る。</param>
/// <param name="AddedOrChangedFiles">追加または変更する相対パスを受け取る。</param>
/// <param name="RemovedFiles">削除する管理対象相対パスを受け取る。</param>
/// <param name="TargetManifest">更新先の完全マニフェストを受け取る。</param>
public sealed record ApplicationUpdateDeltaManifest(
    int SchemaVersion,
    string ApplicationId,
    string BaseVersion,
    string TargetVersion,
    string RuntimeIdentifier,
    IReadOnlyList<string> AddedOrChangedFiles,
    IReadOnlyList<string> RemovedFiles,
    ApplicationUpdateManifest TargetManifest
);