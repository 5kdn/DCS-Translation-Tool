using System.Globalization;
using System.Windows.Data;

using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Converters;

/// <summary>
/// ファイルの種類と変更種別からアイコンを取得するコンバーター。
/// </summary>
public class FileEntryToIconKindConverter : IMultiValueConverter {
    /// <summary>
    /// ファイル種別と変更種別からアイコンを返す。
    /// </summary>
    /// <param name="values">[0] がディレクトリかどうか、[1] が変更種別、[2] がページ判定フラグ</param>
    /// <param name="targetType">変換後の型</param>
    /// <param name="parameter">未使用</param>
    /// <param name="culture">カルチャ</param>
    /// <returns>アイコン種別</returns>
    public object Convert( object[] values, Type targetType, object? parameter, CultureInfo culture ) {
        if(
            values.Length == 3 &&
            values[0] is bool isDirectory &&
            values[1] is FileChangeType changeType &&
            values[2] is ChangeTypeMode mode
            ) {
            return (isDirectory, changeType, mode) switch
            {
                // DL済みで変更なし
                (true, FileChangeType.Unchanged, _ ) => PackIconKind.FolderCheckOutline,
                (false, FileChangeType.Unchanged, _ ) => PackIconKind.FileCheckOutline,
                // 未DL
                (true, FileChangeType.RepoOnly, ChangeTypeMode.Download ) => PackIconKind.FolderDownloadOutline,
                (false, FileChangeType.RepoOnly, ChangeTypeMode.Download ) => PackIconKind.FileDownloadOutline,
                (true, FileChangeType.RepoOnly, ChangeTypeMode.Upload ) => PackIconKind.FolderRemoveOutline,
                (false, FileChangeType.RepoOnly, ChangeTypeMode.Upload ) => PackIconKind.FileRemoveOutline,
                // リポジトリに存在せず、ローカルに有る
                (true, FileChangeType.LocalOnly, ChangeTypeMode.Download ) => PackIconKind.FolderRemoveOutline,
                (false, FileChangeType.LocalOnly, ChangeTypeMode.Download ) => PackIconKind.FileRemoveOutline,
                (true, FileChangeType.LocalOnly, ChangeTypeMode.Upload ) => PackIconKind.FolderUploadOutline,
                (false, FileChangeType.LocalOnly, ChangeTypeMode.Upload ) => PackIconKind.FileUploadOutline,
                // 変更差分有り
                (true, FileChangeType.Modified, _ ) => PackIconKind.FolderAlertOutline,
                (false, FileChangeType.Modified, _ ) => PackIconKind.FileAlertOutline,
                // デフォルト・読み込み失敗
                (true, _, _ ) => PackIconKind.FolderQuestion,
                (false, _, _ ) => PackIconKind.FileQuestion,
            };
        }
        return PackIconKind.FileQuestion;
    }

    /// <summary>
    /// 変換結果から元の値を取得する。
    /// </summary>
    /// <param name="value">変換後の値</param>
    /// <param name="targetTypes">変換前の型配列</param>
    /// <param name="parameter">未使用</param>
    /// <param name="culture">カルチャ</param>
    /// <returns>変換結果</returns>
    public object[] ConvertBack( object value, Type[] targetTypes, object? parameter, CultureInfo culture ) =>
        throw new NotImplementedException();
}