using System.IO;

using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の import/export パス解決を担う。
/// </summary>
/// <param name="settings">アプリケーション設定。</param>
/// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
public sealed class TranslationCreationPathResolver(
    AppSettings settings,
    string archiveFullPath ) {
    #region PublicMethods

    /// <summary>
    /// dictionary の既定書き出し先を取得する。
    /// </summary>
    /// <returns>書き出し先パスを返す。</returns>
    /// <exception cref="InvalidOperationException">翻訳ファイル出力先ディレクトリが未設定、またはアーカイブが既知のルート配下に存在しない場合に送出する。</exception>
    public string GetDictionaryExportPath() => Path.Combine( GetExportDirectoryPath(), "dictionary" );

    /// <summary>
    /// PO の既定書き出し先を取得する。
    /// </summary>
    /// <returns>書き出し先パスを返す。</returns>
    /// <exception cref="InvalidOperationException">翻訳ファイル出力先ディレクトリが未設定、またはアーカイブが既知のルート配下に存在しない場合に送出する。</exception>
    public string GetPoExportPath() => Path.Combine( GetExportDirectoryPath(), $"{Path.GetFileNameWithoutExtension( archiveFullPath )}.po" );

    /// <summary>
    /// CSV の既定書き出し先を取得する。
    /// </summary>
    /// <returns>書き出し先パスを返す。</returns>
    /// <exception cref="InvalidOperationException">翻訳ファイル出力先ディレクトリが未設定、またはアーカイブが既知のルート配下に存在しない場合に送出する。</exception>
    public string GetCsvExportPath() => Path.Combine( GetExportDirectoryPath(), $"{Path.GetFileNameWithoutExtension( archiveFullPath )}.csv" );

    /// <summary>
    /// PO 読み込みダイアログの初期パスを取得する。
    /// </summary>
    /// <returns>初期パスを返す。</returns>
    public string GetPoImportInitialPath() => TryResolve( GetPoExportPath, $"{Path.GetFileNameWithoutExtension( archiveFullPath )}.po" );

    /// <summary>
    /// dictionary 読み込みダイアログの初期パスを取得する。
    /// </summary>
    /// <returns>初期パスを返す。</returns>
    public string GetDictionaryImportInitialPath() => TryResolve( GetDictionaryExportPath, "dictionary" );

    /// <summary>
    /// CSV 読み込みダイアログの初期パスを取得する。
    /// </summary>
    /// <returns>初期パスを返す。</returns>
    public string GetCsvImportInitialPath() => TryResolve( GetCsvExportPath, $"{Path.GetFileNameWithoutExtension( archiveFullPath )}.csv" );

    /// <summary>
    /// 既定出力ディレクトリを取得する。
    /// </summary>
    /// <returns>出力ディレクトリパスを返す。</returns>
    /// <exception cref="InvalidOperationException">翻訳ファイル出力先ディレクトリが未設定、またはアーカイブが既知のルート配下に存在しない場合に送出する。</exception>
    public string GetExportDirectoryPath() {
        var translateFileDir = settings.TranslateFileDir;
        if(string.IsNullOrWhiteSpace( translateFileDir )) {
            throw new InvalidOperationException( "翻訳ファイル出力先ディレクトリが未設定である。" );
        }

        if(TryBuildExportPath( settings.DcsWorldInstallDir, "DCSWorld", out var dcsWorldPath )) {
            return dcsWorldPath;
        }

        if(TryBuildExportPath( settings.SourceUserMissionDir, "UserMissions", out var userMissionPath )) {
            return userMissionPath;
        }

        throw new InvalidOperationException( "アーカイブが既知のルート配下に存在しません。" );

        /// <summary>
        /// 指定基準ディレクトリ配下での書き出し先を構築できるかどうかを判定する。
        /// </summary>
        /// <param name="baseDirectory">判定対象の基準ディレクトリ。</param>
        /// <param name="relativeRoot">書き出し先ルート名。</param>
        /// <param name="exportDirectoryPath">構築できた場合の書き出し先ディレクトリ。</param>
        /// <returns>構築できた場合は <see langword="true"/> を返す。</returns>
        bool TryBuildExportPath( string baseDirectory, string relativeRoot, out string exportDirectoryPath ) {
            exportDirectoryPath = string.Empty;
            if(string.IsNullOrWhiteSpace( baseDirectory )) {
                return false;
            }

            var normalizedBasePath = Path.GetFullPath( baseDirectory );
            var normalizedArchivePath = Path.GetFullPath( archiveFullPath );
            if(!IsPathWithinBaseDirectory( normalizedBasePath, normalizedArchivePath )) {
                return false;
            }

            var relativePath = Path.GetRelativePath( normalizedBasePath, normalizedArchivePath );
            exportDirectoryPath = Path.Combine( translateFileDir, relativeRoot, relativePath, "l10n", "JP" );
            return true;
        }
    }
    #endregion

    #region PrivateHelpers

    /// <summary>
    /// 優先パス解決を試み、失敗時はアーカイブ隣接パスへフォールバックする。
    /// </summary>
    /// <param name="preferredPathFactory">優先パス生成処理。</param>
    /// <param name="fallbackFileName">フォールバック時に利用するファイル名。</param>
    /// <returns>解決したパスを返す。</returns>
    private string TryResolve( Func<string> preferredPathFactory, string fallbackFileName ) {
        try {
            return preferredPathFactory();
        }
        catch {
            var archiveDirectory = Path.GetDirectoryName( archiveFullPath );
            return string.IsNullOrWhiteSpace( archiveDirectory )
                ? fallbackFileName
                : Path.Combine( archiveDirectory, fallbackFileName );
        }
    }

    /// <summary>
    /// 対象パスが基準ディレクトリ配下に存在するかどうかを判定する。
    /// </summary>
    /// <param name="baseDirectory">基準ディレクトリ。</param>
    /// <param name="targetPath">判定対象パス。</param>
    /// <returns>基準ディレクトリ配下に存在する場合は <see langword="true"/> を返す。</returns>
    private static bool IsPathWithinBaseDirectory( string baseDirectory, string targetPath ) {
        var relativePath = Path.GetRelativePath( baseDirectory, targetPath );
        return !relativePath.StartsWith( "..", StringComparison.Ordinal )
            && !Path.IsPathRooted( relativePath );
    }
    #endregion
}