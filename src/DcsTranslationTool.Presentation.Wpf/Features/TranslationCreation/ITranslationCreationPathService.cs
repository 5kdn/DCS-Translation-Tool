using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の import/export パス解決を提供する。
/// </summary>
public interface ITranslationCreationPathService {
    /// <summary>
    /// dictionary の既定書き出し先を取得する。
    /// </summary>
    /// <param name="settings">アプリケーション設定。</param>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>書き出し先パスを返す。</returns>
    /// <exception cref="InvalidOperationException">翻訳ファイル出力先ディレクトリが未設定、またはアーカイブが既知のルート配下に存在しない場合に送出する。</exception>
    string GetDictionaryExportPath( AppSettings settings, string archiveFullPath );

    /// <summary>
    /// PO の既定書き出し先を取得する。
    /// </summary>
    /// <param name="settings">アプリケーション設定。</param>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>書き出し先パスを返す。</returns>
    /// <exception cref="InvalidOperationException">翻訳ファイル出力先ディレクトリが未設定、またはアーカイブが既知のルート配下に存在しない場合に送出する。</exception>
    string GetPoExportPath( AppSettings settings, string archiveFullPath );

    /// <summary>
    /// CSV の既定書き出し先を取得する。
    /// </summary>
    /// <param name="settings">アプリケーション設定。</param>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>書き出し先パスを返す。</returns>
    /// <exception cref="InvalidOperationException">翻訳ファイル出力先ディレクトリが未設定、またはアーカイブが既知のルート配下に存在しない場合に送出する。</exception>
    string GetCsvExportPath( AppSettings settings, string archiveFullPath );

    /// <summary>
    /// PO 読み込みダイアログの初期パスを取得する。
    /// </summary>
    /// <param name="settings">アプリケーション設定。</param>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>初期パスを返す。</returns>
    string GetPoImportInitialPath( AppSettings settings, string archiveFullPath );

    /// <summary>
    /// dictionary 読み込みダイアログの初期パスを取得する。
    /// </summary>
    /// <param name="settings">アプリケーション設定。</param>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>初期パスを返す。</returns>
    string GetDictionaryImportInitialPath( AppSettings settings, string archiveFullPath );

    /// <summary>
    /// CSV 読み込みダイアログの初期パスを取得する。
    /// </summary>
    /// <param name="settings">アプリケーション設定。</param>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>初期パスを返す。</returns>
    string GetCsvImportInitialPath( AppSettings settings, string archiveFullPath );
}