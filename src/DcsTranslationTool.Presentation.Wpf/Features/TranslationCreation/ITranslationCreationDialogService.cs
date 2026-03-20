namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 機能で利用する対話操作を提供する。
/// </summary>
public interface ITranslationCreationDialogService {
    /// <summary>
    /// 閉じる確認ダイアログを表示する。
    /// </summary>
    /// <returns>閉じてもよい場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmCloseAsync();

    /// <summary>
    /// PO 取り込み時の上書き確認を行う。
    /// </summary>
    /// <returns>上書きする場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmPoOverwriteAsync();

    /// <summary>
    /// dictionary 取り込み時の上書き確認を行う。
    /// </summary>
    /// <returns>上書きする場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmDictionaryOverwriteAsync();

    /// <summary>
    /// CSV 取り込み時の上書き確認を行う。
    /// </summary>
    /// <returns>上書きする場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmCsvOverwriteAsync();

    /// <summary>
    /// dictionary の部分一致取り込み確認を行う。
    /// </summary>
    /// <param name="matchedCount">一致件数。</param>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmDictionaryPartialImportAsync( int matchedCount );

    /// <summary>
    /// PO の部分一致取り込み確認を行う。
    /// </summary>
    /// <param name="matchedCount">一致件数。</param>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmPoPartialImportAsync( int matchedCount );

    /// <summary>
    /// CSV の部分一致取り込み確認を行う。
    /// </summary>
    /// <param name="matchedCount">一致件数。</param>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmCsvPartialImportAsync( int matchedCount );

    /// <summary>
    /// アーカイブ内の JP dictionary 存在警告を表示する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>継続する場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmArchiveContainsJapaneseDictionaryAsync( string archiveFullPath );

    /// <summary>
    /// 埋め込み JP dictionary の初期取り込み確認を行う。
    /// </summary>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmJapaneseDictionaryImportAsync();

    /// <summary>
    /// 書き出し先パスを確認し、必要に応じて上書きまたは別名保存を選択させる。
    /// </summary>
    /// <param name="exportPath">既定の書き出し先パス。</param>
    /// <param name="saveFileFilter">保存ダイアログフィルタ。</param>
    /// <param name="logTargetName">ログ出力用対象名。</param>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>確定した保存先パス。キャンセル時は <see langword="null"/> を返す。</returns>
    Task<string?> ConfirmExportPathAsync( string exportPath, string saveFileFilter, string logTargetName, string archiveFullPath );

    /// <summary>
    /// 取り込みファイルを選択する。
    /// </summary>
    /// <param name="initialPath">初期パス。</param>
    /// <param name="openFileFilter">ファイル選択フィルタ。</param>
    /// <param name="selectedPath">選択結果パス。</param>
    /// <returns>選択した場合は <see langword="true"/> を返す。</returns>
    bool TrySelectImportFile( string initialPath, string openFileFilter, out string selectedPath );
}