using Caliburn.Micro;

using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.ViewModels;

/// <summary>
/// ファイルの変更種別によるフィルタ状態を保持する ViewModel
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public sealed class FilterViewModel( ILoggingService logger ) : PropertyChangedBase, IFilterViewModel {
    #region Fields

    private bool _all = true;
    private bool _unchanged = true;
    private bool _repoOnly = true;
    private bool _localOnly = true;
    private bool _modified = true;

    #endregion

    #region Properties

    public bool All {
        get => _all;
        set {
            if(_all == value) return;
            _all = value;
            NotifyOfPropertyChange( () => All );
            logger.Info( $"All フィルターを更新した。IsChecked={value}" );

            Unchanged = value;
            RepoOnly = value;
            LocalOnly = value;
            Modified = value;

            FiltersChanged?.Invoke( this, EventArgs.Empty );
            logger.Info( "全フィルターを一括更新した。" );
        }
    }

    public bool Unchanged {
        get => _unchanged;
        set {
            if(_unchanged == value) return;
            _unchanged = value;
            NotifyOfPropertyChange( () => Unchanged );
            UpdateAll();
            logger.Info( $"Unchanged フィルターを更新した。IsChecked={value}" );
        }
    }

    public bool RepoOnly {
        get => _repoOnly;
        set {
            if(_repoOnly == value) return;
            _repoOnly = value;
            NotifyOfPropertyChange( () => RepoOnly );
            UpdateAll();
            logger.Info( $"RepoOnly フィルターを更新した。IsChecked={value}" );
        }
    }

    public bool LocalOnly {
        get => _localOnly;
        set {
            if(_localOnly == value) return;
            _localOnly = value;
            NotifyOfPropertyChange( () => LocalOnly );
            UpdateAll();
            logger.Info( $"LocalOnly フィルターを更新した。IsChecked={value}" );
        }
    }

    public bool Modified {
        get => _modified;
        set {
            if(_modified == value) return;
            _modified = value;
            NotifyOfPropertyChange( () => Modified );
            UpdateAll();
            logger.Info( $"Modified フィルターを更新した。IsChecked={value}" );
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public IEnumerable<FileChangeType?> GetActiveTypes() {
        if(Unchanged) yield return FileChangeType.Unchanged;
        if(RepoOnly) yield return FileChangeType.RepoOnly;
        if(LocalOnly) yield return FileChangeType.LocalOnly;
        if(Modified) yield return FileChangeType.Modified;
    }

    /// <summary>
    /// All の状態を更新する
    /// </summary>
    private void UpdateAll() {
        var allChecked = Unchanged && RepoOnly && LocalOnly && Modified;
        if(_all != allChecked) {
            _all = allChecked;
            NotifyOfPropertyChange( () => All );
            logger.Info( $"All フィルターを再計算した。IsChecked={_all}" );
        }

        FiltersChanged?.Invoke( this, EventArgs.Empty );
        logger.Info( "FiltersChanged イベントを発行した。" );
    }

    #endregion

    #region Events

    /// <inheritdoc/>
    public event EventHandler? FiltersChanged;

    #endregion
}