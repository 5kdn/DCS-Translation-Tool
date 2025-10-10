using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

using Caliburn.Micro;

using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.ViewModels;

/// <inheritdoc/>
public class FileEntryViewModel(
    FileEntry model,
    ChangeTypeMode changeTypeMode,
    ILoggingService logger
) : PropertyChangedBase, IFileEntryViewModel, IDisposable {

    #region Fields

    private bool? _checkState = false;
    private bool _isSelected = false;

    private ObservableCollection<IFileEntryViewModel> _children = [];
    private bool _childrenHandlersAttached;
    private bool _suppressCheckPropagation = false;
    private bool _suppressSelectPropagation = false;
    private readonly ChangeTypeMode _mode = changeTypeMode;
    private bool _isExpanded;
    private bool _isVisible = true;

    #endregion

    #region Events / Model

    /// <inheritdoc/>
    public event EventHandler<bool?>? CheckStateChanged;

    /// <inheritdoc/>
    public FileEntry Model { get; } = model;

    #endregion

    #region Basic Properties

    /// <inheritdoc/>
    public string Name => Model.Name;

    /// <inheritdoc/>
    public string Path => Model.Path;

    /// <inheritdoc/>
    public bool IsDirectory => Model.IsDirectory;

    #endregion

    #region UI State

    /// <inheritdoc/>
    public bool IsExpanded {
        get => _isExpanded;
        set => Set( ref _isExpanded, value );
    }

    /// <inheritdoc/>
    public bool IsVisible {
        get => _isVisible;
        set => Set( ref _isVisible, value );
    }

    #endregion

    #region Derived Properties

    /// <inheritdoc/>
    public FileChangeType? ChangeType {
        get {
            switch(_mode) {
                case ChangeTypeMode.Download:
                    if(IsDirectory) {
                        if(Children.Count == 0) return FileChangeType.Unchanged;
                        var set = Children.Select(c => c.ChangeType).ToHashSet();
                        if(set.Contains( FileChangeType.Modified )) return FileChangeType.Modified;
                        if(set.Contains( FileChangeType.RepoOnly )) return FileChangeType.RepoOnly;
                        if(set.Contains( FileChangeType.Unchanged )) return FileChangeType.Unchanged;
                        if(set.Contains( FileChangeType.LocalOnly )) return FileChangeType.LocalOnly;
                        return null;
                    }
                    return (Model.LocalSha, Model.RepoSha) switch
                    {
                        (string l, string r ) when l == r => FileChangeType.Unchanged,
                        (string l, string r ) when l != r => FileChangeType.Modified,
                        (string _, null ) => FileChangeType.LocalOnly,
                        (null, string _ ) => FileChangeType.RepoOnly,
                        (null, null ) => null,
                        _ => throw new NotImplementedException()
                    };

                case ChangeTypeMode.Upload:
                    if(IsDirectory) {
                        if(Children.Count == 0) return FileChangeType.Unchanged;
                        var set = Children.Select(c => c.ChangeType).ToHashSet();
                        if(set.Contains( FileChangeType.Modified )) return FileChangeType.Modified;
                        if(set.Contains( FileChangeType.LocalOnly )) return FileChangeType.LocalOnly;
                        if(set.Contains( FileChangeType.Unchanged )) return FileChangeType.Unchanged;
                        if(set.Contains( FileChangeType.RepoOnly )) return FileChangeType.RepoOnly;
                        return null;
                    }
                    return (Model.LocalSha, Model.RepoSha) switch
                    {
                        (string l, string r ) when l == r => FileChangeType.Unchanged,
                        (string l, string r ) when l != r => FileChangeType.Modified,
                        (string _, null ) => FileChangeType.LocalOnly,
                        (null, string _ ) => FileChangeType.RepoOnly,
                        (null, null ) => null,
                        _ => throw new NotImplementedException()
                    };

                default:
                    throw new InvalidEnumArgumentException();
            }
        }
    }

    /// <inheritdoc/>
    public bool CanCheck => (_mode, ChangeType) switch
    {
        (ChangeTypeMode.Upload, FileChangeType.Unchanged ) => false,
        (ChangeTypeMode.Download, FileChangeType.Unchanged ) => false,
        (ChangeTypeMode.Download, FileChangeType.LocalOnly ) => false,
        _ => true,
    };

    /// <inheritdoc/>
    public bool? CheckState {
        get => _checkState;
        set {
            var coercedValue = CanCheck ? value : false;
            if(!Set( ref _checkState, coercedValue )) return;

            // 親→子へ伝播
            if(!_suppressCheckPropagation && IsDirectory && coercedValue is not null) {
                try {
                    _suppressCheckPropagation = true;
                    foreach(var child in Children) {
                        var childTargetValue = child.CanCheck ? coercedValue : false;
                        if(child.CheckState != childTargetValue) child.CheckState = childTargetValue;
                    }
                }
                finally {
                    _suppressCheckPropagation = false;
                }
            }

            CheckStateChanged?.Invoke( this, _checkState );
        }
    }
    /// <inheritdoc/>
    public bool IsSelected {
        get => _isSelected;
        set {
            if(!CanCheck && !value) return;

            if(!Set( ref _isSelected, value )) return;
            logger.Info( $"選択状態を更新した。Path={Model.Path}, Value={value}" );

            if(!_suppressSelectPropagation && IsDirectory) {
                try {
                    _suppressSelectPropagation = true;
                    foreach(var child in Children) {
                        if(child.IsSelected != value) child.IsSelected = value;
                    }
                }
                finally {
                    _suppressSelectPropagation = false;
                }
            }
        }
    }

    /// <inheritdoc/>
    public ObservableCollection<IFileEntryViewModel> Children {
        get {
            EnsureChildrenHandlersAttached();
            return _children;
        }
        set {
            if(ReferenceEquals( _children, value )) return;
            DetachChildrenHandlers( _children );
            Set( ref _children, value ?? [] );
            AttachChildrenHandlers( _children );
            logger.Info( $"子ノードコレクションを更新した。Path={Model.Path}, ChildCount={_children.Count}" );

            // 参照入替に伴い集計系を更新
            NotifyOfPropertyChange( nameof( ChangeType ) );
            RecomputeCheckStateFromChildren();
            NotifyOfPropertyChange( nameof( CanCheck ) );
            if(!CanCheck && _checkState is not false) CheckState = false;
        }
    }

    #endregion

    #region Ctor / Dispose

    public void Dispose() {
        DetachChildrenHandlers( Children );
        GC.SuppressFinalize( this );
        logger.Info( $"FileEntryViewModel を破棄した。Path={Model.Path}" );
    }

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public void SetSelectRecursive( bool value ) {
        logger.Info( $"選択状態を再帰的に設定する。Path={Model.Path}, Value={value}" );
        IsSelected = value;
        foreach(var child in Children) child.SetSelectRecursive( value );
    }

    /// <inheritdoc/>
    public List<FileEntry> GetCheckedModelRecursive( bool fileOnly = false ) {
        var checkedChildrenModels = new List<FileEntry>();

        switch(CheckState, IsDirectory, fileOnly) {
            case (true, false, _ ):
            case (true, true, false ):
            case (null, false, _ ):
            case (null, true, false ):
                checkedChildrenModels.Add( Model );
                break;
        }

        foreach(var child in Children)
            checkedChildrenModels.AddRange( child.GetCheckedModelRecursive( fileOnly ) );

        return checkedChildrenModels;
    }

    /// <inheritdoc/>
    public List<IFileEntryViewModel> GetCheckedViewModelRecursive() {
        var checkedChildren = new List<IFileEntryViewModel>();

        if(CheckState is true &&
            !IsDirectory &&
            ChangeType != FileChangeType.Unchanged) {
            checkedChildren.Add( this );
        }

        foreach(var child in Children)
            checkedChildren.AddRange( child.GetCheckedViewModelRecursive() );

        return checkedChildren;
    }

    #endregion

    #region Private Methods

    private void EnsureChildrenHandlersAttached() {
        if(_childrenHandlersAttached) return;
        AttachChildrenHandlers( _children );
    }

    /// <summary>
    /// 子ノードの状態から自分のチェック状態を再計算する
    /// </summary>
    private void RecomputeCheckStateFromChildren() {
        if(!IsDirectory || Children.Count == 0) return;

        var allChecked = Children.All(c => c.CheckState == true);
        var allUnchecked = Children.All(c => c.CheckState == false);

        bool? newState = (allChecked, allUnchecked) switch
        {
            (true, _) => true,
            (_, true) => false,
            _ => null,
        };

        if(CheckState == newState) return;
        CheckState = newState;
        NotifyOfPropertyChange( nameof( CheckState ) );
        CheckStateChanged?.Invoke( this, CheckState );
        logger.Info( $"子ノードに基づきチェック状態を再計算した。Path={Model.Path}, Value={newState}" );
    }

    /// <summary>既存コレクションから購読を全解除</summary>
    private void DetachChildrenHandlers( ObservableCollection<IFileEntryViewModel> collection ) {
        if(collection is null) return;
        collection.CollectionChanged -= OnChildrenCollectionChanged;
        foreach(var ch in collection) {
            if(ch is INotifyPropertyChanged inpc) inpc.PropertyChanged -= OnChildPropertyChanged;
            ch.CheckStateChanged -= OnChildCheckStateChanged;
        }
        if(ReferenceEquals( collection, _children )) _childrenHandlersAttached = false;
    }

    /// <summary>コレクションへ購読を付与</summary>
    private void AttachChildrenHandlers( ObservableCollection<IFileEntryViewModel> collection ) {
        if(collection is null) return;
        collection.CollectionChanged -= OnChildrenCollectionChanged; // 二重防止
        collection.CollectionChanged += OnChildrenCollectionChanged;
        foreach(var ch in collection) {
            if(ch is INotifyPropertyChanged inpc) inpc.PropertyChanged += OnChildPropertyChanged;
            ch.CheckStateChanged += OnChildCheckStateChanged;
        }
        if(ReferenceEquals( collection, _children )) _childrenHandlersAttached = true;
    }

    #endregion

    #region Event Handlers

    private void OnChildrenCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        if(e.Action == NotifyCollectionChangedAction.Reset) {
            DetachChildrenHandlers( Children );
            AttachChildrenHandlers( Children );
            NotifyOfPropertyChange( nameof( ChangeType ) );
            RecomputeCheckStateFromChildren();
            NotifyOfPropertyChange( nameof( CanCheck ) );
            if(!CanCheck && CheckState != false) CheckState = false;
            logger.Info( $"子ノードコレクションがリセットされた。Path={Model.Path}" );
            return;
        }

        if(e.OldItems is not null) {
            foreach(var obj in e.OldItems) {
                if(obj is INotifyPropertyChanged inpc) inpc.PropertyChanged -= OnChildPropertyChanged;
                if(obj is IFileEntryViewModel vm) vm.CheckStateChanged -= OnChildCheckStateChanged;
            }
        }
        if(e.NewItems is not null) {
            foreach(var obj in e.NewItems) {
                if(obj is INotifyPropertyChanged inpc) inpc.PropertyChanged += OnChildPropertyChanged;
                if(obj is IFileEntryViewModel vm) vm.CheckStateChanged += OnChildCheckStateChanged;
            }
        }

        NotifyOfPropertyChange( nameof( ChangeType ) );
        RecomputeCheckStateFromChildren();
        NotifyOfPropertyChange( nameof( CanCheck ) );
        if(!CanCheck && CheckState is not false) CheckState = false;
    }

    private void OnChildPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName == nameof( ChangeType )) {
            NotifyOfPropertyChange( nameof( ChangeType ) );
            NotifyOfPropertyChange( nameof( CanCheck ) );
            if(!CanCheck && CheckState is not false) CheckState = false;
        }
    }

    /// <summary>子のチェック状態変化時に自分の状態を再計算する</summary>
    private void OnChildCheckStateChanged( object? sender, bool? e ) {
        if(_suppressCheckPropagation) return;
        RecomputeCheckStateFromChildren();
    }

    #endregion
}