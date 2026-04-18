using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// Download Page の差分個別選択項目を表す。
/// </summary>
/// <param name="path">対象ファイルパス。</param>
public sealed class DownloadModifiedApplySelectionItem( string path ) : PropertyChangedBase {
    private DownloadModifiedApplySource _applySource = DownloadModifiedApplySource.Local;

    /// <summary>
    /// 対象ファイルパスを取得する。
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// ラジオボタンのグループ名を取得する。
    /// </summary>
    public string GroupName { get; } = $"DownloadModifiedApplySelection_{Guid.NewGuid():N}";

    /// <summary>
    /// 適用元を取得または設定する。
    /// </summary>
    public DownloadModifiedApplySource ApplySource {
        get => _applySource;
        set {
            if(!Set( ref _applySource, value )) {
                return;
            }

            NotifyOfPropertyChange( nameof( IsRepositorySelected ) );
            NotifyOfPropertyChange( nameof( IsLocalSelected ) );
        }
    }

    /// <summary>
    /// サーバー版が選択されているかどうかを取得または設定する。
    /// </summary>
    public bool IsRepositorySelected {
        get => ApplySource == DownloadModifiedApplySource.Repository;
        set {
            if(value) {
                ApplySource = DownloadModifiedApplySource.Repository;
            }
        }
    }

    /// <summary>
    /// ローカル版が選択されているかどうかを取得または設定する。
    /// </summary>
    public bool IsLocalSelected {
        get => ApplySource == DownloadModifiedApplySource.Local;
        set {
            if(value) {
                ApplySource = DownloadModifiedApplySource.Local;
            }
        }
    }
}