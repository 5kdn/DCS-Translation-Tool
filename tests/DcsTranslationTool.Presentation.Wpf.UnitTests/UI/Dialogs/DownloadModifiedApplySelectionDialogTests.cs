using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.UI.Dialogs;

/// <summary>
/// 差分個別選択ダイアログ関連型を検証する。
/// </summary>
public sealed class DownloadModifiedApplySelectionDialogTests {
    /// <summary>
    /// 選択項目の初期値がローカル版であることを検証する。
    /// </summary>
    [Fact]
    public void 選択項目の初期値はローカル版である() {
        var item = new DownloadModifiedApplySelectionItem( "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua" );

        Assert.Equal( DownloadModifiedApplySource.Local, item.ApplySource );
        Assert.True( item.IsLocalSelected );
        Assert.False( item.IsRepositorySelected );
    }

    /// <summary>
    /// 結果生成時に選択状態を保持することを検証する。
    /// </summary>
    [Fact]
    public void 結果生成時に選択状態を保持する() {
        var repositoryItem = new DownloadModifiedApplySelectionItem( "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua" )
        {
            ApplySource = DownloadModifiedApplySource.Repository
        };
        var localItem = new DownloadModifiedApplySelectionItem( "DCSWorld/Mods/aircraft/A10C/L10N/Alt.lua" );
        var parameters = new DownloadModifiedApplySelectionDialogParameters
        {
            Items = [repositoryItem, localItem]
        };

        var result = parameters.CreateResult();

        Assert.True( result.IsConfirmed );
        Assert.Equal( DownloadModifiedApplySource.Repository, result.SelectedSources[repositoryItem.Path] );
        Assert.Equal( DownloadModifiedApplySource.Local, result.SelectedSources[localItem.Path] );
    }
}