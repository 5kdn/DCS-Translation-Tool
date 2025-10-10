using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;

namespace DcsTranslationTool.Presentation.Wpf.Converters;

/// <summary>
/// ファイル種別と変更種別から表示色を取得するコンバーター。
/// </summary>
public class FileChangeTypeToBrushConverter : IMultiValueConverter {
    /// <summary>
    /// ファイル種別と変更種別からブラシを返す。
    /// </summary>
    /// <param name="values">[0] が変更種別、[1] がPage判別フラグ</param>
    /// <param name="targetType">変換後の型</param>
    /// <param name="parameter">未使用</param>
    /// <param name="culture">カルチャ</param>
    /// <returns>変換結果</returns>
    public object Convert( object[] values, Type targetType, object? parameter, CultureInfo culture ) {
        if(
            values.Length == 2 &&
            values[0] is FileChangeType changeType &&
            values[1] is ChangeTypeMode mode
            ) {
            var key = (changeType, mode) switch
            {
                // DL済みで変更なし
                (FileChangeType.Unchanged, ChangeTypeMode.Download ) => "Brush.Download.Unchanged",
                (FileChangeType.Unchanged, ChangeTypeMode.Upload ) => "Brush.Upload.Unchanged",
                // リポジトリに存在し、ローカルに無い
                (FileChangeType.RepoOnly, ChangeTypeMode.Download ) => "Brush.Download.RepoOnly",
                (FileChangeType.RepoOnly, ChangeTypeMode.Upload ) => "Brush.Upload.RepoOnly",
                // リポジトリに存在せず、ローカルに有る
                (FileChangeType.LocalOnly, ChangeTypeMode.Download ) => "Brush.Download.LocalOnly",
                (FileChangeType.LocalOnly, ChangeTypeMode.Upload ) => "Brush.Upload.LocalOnly",
                // 変更差分有り
                (FileChangeType.Modified, ChangeTypeMode.Download ) => "Brush.Download.Modified",
                (FileChangeType.Modified, ChangeTypeMode.Upload ) => "Brush.Upload.Modified",
                // デフォルト・読み込み失敗
                (_, ChangeTypeMode.Download ) => "Brush.Download.Default",
                (_, ChangeTypeMode.Upload ) => "Brush.Upload.Default",
                (_,_) => "Black",
            };

            if(System.Windows.Application.Current?.TryFindResource( key ) is Brush b) return b;
            return SystemColors.ControlTextBrush;
        }
        return SystemColors.ControlTextBrush;
    }

    /// <summary>
    /// 変換結果から元の値を取得する。
    /// </summary>
    /// <param name="value">変換後の値</param>
    /// <param name="targetTypes">変換前の型配列</param>
    /// <param name="parameter">未使用</param>
    /// <param name="culture">カルチャ</param>
    /// <returns>変換結果</returns>
    public object[] ConvertBack( object value, Type[] targetTypes, object? parameter, CultureInfo culture ) {
        throw new NotImplementedException();
    }
}