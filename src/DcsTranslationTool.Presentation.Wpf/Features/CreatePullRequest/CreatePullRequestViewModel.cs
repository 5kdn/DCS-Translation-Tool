using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;

using Caliburn.Micro;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Items;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Extensions;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Resources;

using FluentResults;

namespace DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;

/// <summary>
/// PR作成ダイアログの ViewModel。
/// 変更種別の選択と同意項目の状態を集約し、PR作成可否を制御する。
/// </summary>
public class CreatePullRequestViewModel(
    CreatePullRequestDialogParameters dialogParameters,
    IApiService apiService,
    IFileContentInspector fileContentInspector,
    ILoggingService logger
) : Conductor<IScreen>.Collection.OneActive, IActivate {

    #region Fields

    /// <summary>ダイアログ完了を通知するTCS。</summary>
    private readonly TaskCompletionSource<CreatePullRequestResult> _tcs = new();

    /// <summary>Commitで追加または更新されるファイル。</summary>
    private IEnumerable<CommitFile> _upsertFiles = [];

    /// <summary>Commitで削除されるファイル。</summary>
    private IEnumerable<CommitFile> _deleteFiles = [];

    /// <summary>PR概要。</summary>
    private string _prSummary = string.Empty;

    /// <summary>PR詳細</summary>
    private string _prDetail = Strings_CreatePullRequest.DialogPullRequestDetailPlaceholder;

    /// <summary>PRの留意点</summary>
    private string _prNotes = Strings_CreatePullRequest.DialogPullRequestNotePlaceholder;

    /// <summary><see cref="PullRequestDialogAgreementCheckItem"/> が全て同意されているか。</summary>
    private bool _isAgreeToAllAgreementItems;

    /// <summary><see cref="PullRequestChangeKindViewModel"/> のうち1件以上がチェック済みか。</summary>
    private bool _isAnyPullRequestChangeKindChecked;

    private bool _isCreatingPullRequest = false;

    #endregion

    #region Properties

    /// <summary>大分類。</summary>
    public string Category { get; init; } = dialogParameters.Category;

    /// <summary>小分類。</summary>
    public string SubCategory { get; init; } = dialogParameters.SubCategory;

    /// <summary>コミット対象ファイル一覧。</summary>
    public IEnumerable<CommitFile> CommitFiles { get; init; } = dialogParameters.CommitFiles;

    /// <summary>追加または更新されるファイル一覧。</summary>
    public IEnumerable<CommitFile> UpsertFiles => _upsertFiles;

    /// <summary>削除されるファイル一覧。</summary>
    public IEnumerable<CommitFile> DeleteFiles => _deleteFiles;

    /// <summary>変更種別コレクション。</summary>
    public BindableCollection<PullRequestChangeKindViewModel> PullRequestChangeKinds { get; } =
        new( Enum.GetValues<PullRequestChangeKind>().Select( kind => new PullRequestChangeKindViewModel( logger, kind ) ) );

    /// <summary>PRタイトル。選択中の変更種別を含める。</summary>
    public string PRTitle {
        get {
            var checkedKinds = PullRequestChangeKinds
                .Where(k => k.IsChecked)
                .Select(k => k.DisplayName)
                .ToArray();

            var kindsText = checkedKinds.Length > 0 ? string.Join("/", checkedKinds) : string.Empty;
            return $"[{Category}][{SubCategory}] {kindsText}";
        }
    }

    ///<summary>PRタイトル。</summary>
    public string PRSummary {
        get => _prSummary;
        set {
            if(!Set( ref _prSummary, value )) return;
            NotifyOfPropertyChange( nameof( CanCreatePullRequest ) );
        }
    }

    ///<summary>PR詳細。</summary>
    public string PRDetail {
        get => _prDetail;
        set => Set( ref _prDetail, value );
    }

    //<summary>PRの留意点。</summary>
    public string PRNotes {
        get => _prNotes;
        set => Set( ref _prNotes, value );
    }

    /// <summary>PR本文。</summary>
    public string PRComment {
        get {
            return
                ":pushpin: "
                + Strings_CreatePullRequest.PullRequestSummaryTitle
                + "\n\n"
                + PRSummary
                + "\n\n"
                + ":hammer_and_wrench: "
                + Strings_CreatePullRequest.PullRequestDetailTitle
                + "\n\n"
                + PRDetail
                + "\n\n"
                + ":warning: "
                + Strings_CreatePullRequest.PullRequestNoteTitle
                + "\n\n"
                + PRNotes;
        }
    }

    /// <summary>同意チェック項目コレクション。</summary>
    public ObservableCollection<PullRequestDialogAgreementCheckItem> AgreementItems { get; } =
    [
        new("アップロードするファイルに個人情報は含まれていません"),
    ];

    /// <summary>全同意済みか。0件時は <see langword="false"/>。</summary>
    public bool AllAgreed => _isAgreeToAllAgreementItems;

    /// <summary>変更種別が1件以上選択済みか。</summary>
    public bool AnyKindSelected => _isAnyPullRequestChangeKindChecked;

    /// <summary>PR作成中か。</summary>
    public bool IsCreatingPullRequest {
        get => _isCreatingPullRequest;
        set => Set( ref _isCreatingPullRequest, value );
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// モーダル表示ヘルパー。
    /// </summary>
    public static async Task<CreatePullRequestResult> ShowDialogAsync(
        CreatePullRequestDialogParameters parameters,
        IApiService apiService,
        IFileContentInspector fileContentInspector,
        ILoggingService logger,
        IWindowManager windowManager,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull( windowManager );
        ArgumentNullException.ThrowIfNull( parameters );
        ArgumentNullException.ThrowIfNull( logger );

        logger.Info( "Pull Request ダイアログを表示する準備を開始する。" );

        var vm = new CreatePullRequestViewModel(parameters, apiService, fileContentInspector, logger);
        _ = windowManager.ShowDialogAsync( vm );
        await using var reg = cancellationToken.Register(() => vm._tcs.TrySetCanceled(cancellationToken));
        var result = await vm._tcs.Task.ConfigureAwait( false );
        logger.Info( $"Pull Request ダイアログが完了した。IsOk={result.IsOk}" );
        return result;
    }

    /// <summary>
    /// 画面アクティブ時の初期化。
    /// </summary>
    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( $"CreatePullRequestViewModel をアクティブ化する。Category={Category}, SubCategory={SubCategory}, FileCount={CommitFiles.Count()}" );

        // ファイルの種別振り分け
        List<CommitFile> upsertFiles = [];
        List<CommitFile> deleteFiles = [];
        foreach(var file in CommitFiles) {
            switch(file.Operation) {
                case CommitOperationType.Upsert:
                    upsertFiles.Add( file );
                    break;
                case CommitOperationType.Delete:
                    deleteFiles.Add( file );
                    break;
            }
        }
        _upsertFiles = upsertFiles;
        NotifyOfPropertyChange( nameof( UpsertFiles ) );
        _deleteFiles = deleteFiles;
        NotifyOfPropertyChange( nameof( DeleteFiles ) );
        logger.Info( $"コミットファイルを分類した。Upsert={_upsertFiles.Count()}, Delete={_deleteFiles.Count()}" );

        // 変更種別と同意項目の購読設定
        HookCollection( PullRequestChangeKinds, OnKindItemPropertyChanged );
        HookCollection( AgreementItems, OnAgreementItemPropertyChanged );

        PullRequestChangeKinds.CollectionChanged += OnKindsCollectionChanged;
        AgreementItems.CollectionChanged += OnAgreementCollectionChanged;

        // 初期再計算
        RecomputeAllAgreed( notify: true );
        RecomputeAnyKindSelected( notify: true );
        NotifyOfPropertyChange( nameof( PRTitle ) );
        logger.Info( $"初期状態を計算した。AllAgreed={AllAgreed}, AnyKindSelected={AnyKindSelected}" );

        await base.OnActivatedAsync( cancellationToken );
        logger.Info( "CreatePullRequestViewModel のアクティブ化が完了した。" );
    }

    /// <summary>
    /// 非アクティブ時。購読解除とクリーンアップ。
    /// </summary>
    protected override async Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"CreatePullRequestViewModel を非アクティブ化する。Close={close}" );
        PullRequestChangeKinds.CollectionChanged -= OnKindsCollectionChanged;
        AgreementItems.CollectionChanged -= OnAgreementCollectionChanged;

        UnhookCollection( PullRequestChangeKinds, OnKindItemPropertyChanged );
        UnhookCollection( AgreementItems, OnAgreementItemPropertyChanged );

        await base.OnDeactivateAsync( close, cancellationToken );

        if(close && !_tcs.Task.IsCompleted) {
            logger.Warn( "ダイアログがキャンセルされたため結果をキャンセルとして返す。" );
            var cancelResult = new CreatePullRequestResult
            {
                IsOk = false,
                Errors = [new OperationCanceledException( "ユーザーによりダイアログがキャンセルされました" )],
            };
            _tcs.TrySetResult( cancelResult );
        }
        logger.Info( "CreatePullRequestViewModel の非アクティブ化が完了した。" );
    }

    #endregion

    #region Action Guards

    /// <summary>PR作成に必要な入力が満たされているか。</summary>
    public bool CanCreatePullRequest =>
        AllAgreed &&
        AnyKindSelected &&
        !string.IsNullOrWhiteSpace( _prSummary );

    #endregion

    #region Actions

    /// <summary>
    /// PRを作成する。
    /// </summary>
    public async Task CreatePullRequest() {
        logger.Info( "Pull Request 作成処理を開始する。" );
        if(!CanCreatePullRequest) {
            logger.Warn( "必要条件を満たしていないため PR 作成を中断する。" );
            return;
        }

        IsCreatingPullRequest = true;

        var msgFromKinds = string.Join("\n", PullRequestChangeKinds.Where(x => x.IsChecked).Select(x => x.DisplayName));
        var branchName = CreateBranchName();
        //var bodyCandidate = string.IsNullOrWhiteSpace( PRComment ) ? msgFromKinds : PRComment;
        var bodyCandidate = string.IsNullOrWhiteSpace( PRComment ) ? msgFromKinds : PRComment;
        var body = string.IsNullOrWhiteSpace( bodyCandidate ) ? "(no summary)" : bodyCandidate;
        var commitMessage = PRTitle;

        CreatePullRequestResult? result = null;
        try {
            var files = await BuildPullRequestFilesAsync();
            if(files.Count == 0) {
                logger.Warn( $"コミット対象ファイルが存在しないため PR 作成を中断する。Branch={branchName}" );
                result = new CreatePullRequestResult
                {
                    IsOk = false,
                    Errors = [new InvalidOperationException( "コミット対象ファイルが存在しません。" )],
                };
            }
            else {
                logger.Info( $"PR リクエストを組み立てる。Branch={branchName}, Upsert={UpsertFiles.Count()}, Delete={DeleteFiles.Count()}" );
                var request = new ApiCreatePullRequestRequest( branchName, commitMessage, PRTitle, body, files );

                var apiResult = await apiService.CreatePullRequestAsync( request );
                if(apiResult.IsFailed) {
                    var errors = apiResult.Errors.Select( ToException ).ToArray();
                    logger.Warn( $"Pull Request API 呼び出しに失敗した。Branch={branchName}, ErrorCount={errors.Length}" );
                    result = new CreatePullRequestResult
                    {
                        IsOk = false,
                        Errors = errors.Length > 0 ? errors : [new InvalidOperationException( "Pull Request API 呼び出しに失敗しました。" )],
                    };
                }
                else {
                    var outcome = apiResult.Value;
                    if(!outcome.Success) {
                        logger.Warn( $"Pull Request API が失敗を返した。Branch={branchName}, Message={outcome.Message}" );
                        var message = string.IsNullOrWhiteSpace( outcome.Message ) ? "Pull Request の作成に失敗しました。" : outcome.Message!;
                        result = new CreatePullRequestResult
                        {
                            IsOk = false,
                            Errors = [new InvalidOperationException( message )],
                        };
                    }
                    else {
                        ApiCreatePullRequestEntry? entry = outcome.Entries.Count > 0 ? outcome.Entries[0] : null;
                        var prUrl = entry?.PullRequestUrl?.ToString();
                        logger.Info( $"Pull Request を作成した。Branch={entry?.BranchName ?? branchName}, Url={prUrl}" );
                        if(outcome.Message is { Length: > 0 }) logger.Info( $"API メッセージ: {outcome.Message}" );

                        result = new CreatePullRequestResult
                        {
                            IsOk = true,
                            PrUrl = prUrl,
                        };
                    }
                }
            }
        }
        catch(OperationCanceledException) {
            logger.Warn( "Pull Request 作成処理がキャンセルされた。" );
            result = new CreatePullRequestResult
            {
                IsOk = false,
                Errors = [new OperationCanceledException( "Operation canceled." )],
            };
        }
        catch(Exception ex) {
            logger.Error( ex.Message, ex );
            logger.Warn( "Pull Request 作成処理で例外が発生した。" );
            result = new CreatePullRequestResult
            {
                IsOk = false,
                Errors = [ex],
            };
        }
        finally {
            IsCreatingPullRequest = false;
            logger.Info( $"Pull Request 作成処理を終了した。IsOk={result?.IsOk}" );

            try {
                await TryCloseAsync( result?.IsOk );
            }
            finally {
                if(result is not null) {
                    logger.Info( $"ダイアログ結果を完了として返す。IsOk={result.IsOk}" );
                    _tcs.TrySetResult( result );
                }
                else {
                    logger.Warn( "結果が null のため失敗として返す。" );
                    _tcs.TrySetResult( new CreatePullRequestResult
                    {
                        IsOk = false,
                        Errors = [new Exception( "Unexpected null result." )],
                    } );
                }
            }
        }
    }

    #endregion

    #region Helper

    /// <summary>Pull Request 用のファイル一覧を構築する。</summary>
    private async Task<List<ApiPullRequestFile>> BuildPullRequestFilesAsync() {
        List<ApiPullRequestFile> files = [];

        foreach(var commitFile in UpsertFiles) {
            if(string.IsNullOrWhiteSpace( commitFile.LocalPath )) throw new InvalidOperationException( $"ローカルパスが未指定のためファイルを読み込めない。RepoPath={commitFile.RepoPath}" );

            if(!File.Exists( commitFile.LocalPath )) throw new FileNotFoundException( $"コミット対象ファイルが存在しない。Path={commitFile.LocalPath}", commitFile.LocalPath );

            var bytes = await File.ReadAllBytesAsync( commitFile.LocalPath );
            var inspection = fileContentInspector.Inspect( bytes );
            if(inspection.IsBinary) throw new InvalidOperationException( $"バイナリファイルはアップロードできない。Path={commitFile.LocalPath}" );

            var text = inspection.Text ?? string.Empty;
            logger.Debug( $"PR へ追加するファイルを読み込んだ。RepoPath={commitFile.RepoPath}, Encoding={inspection.Encoding?.WebName ?? "unknown"}, Length={text.Length}" );

            files.Add( new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, commitFile.RepoPath, text ) );
        }

        foreach(var commitFile in DeleteFiles) {
            files.Add( new ApiPullRequestFile( ApiPullRequestFileOperation.Delete, commitFile.RepoPath, null ) );
        }

        return files;
    }

    /// <summary>FluentResults のエラーを例外に変換する。</summary>
    private static Exception ToException( IError error ) =>
        error switch
        {
            ExceptionalError exceptional when exceptional.Exception is not null => exceptional.Exception,
            _ when !string.IsNullOrWhiteSpace( error.Message ) => new InvalidOperationException( error.Message ),
            _ => new InvalidOperationException( error.ToString() ),
        };

    /// <summary>PRタイトル用のブランチ名を生成する。</summary>
    private string CreateBranchName() {
        var jst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"));
        var dateStr = jst.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "JST";
        var changes = string.Join("_", PullRequestChangeKinds.Where(x => x.IsChecked).Select(x => x.Kind.GetBranchString() ));
        var safeCategory = SanitizeForBranchSegment(Category);
        var safeSubCategory = SanitizeForBranchSegment(SubCategory);
        var branch = $"feature/{safeCategory}/{safeSubCategory}/{changes}--{dateStr}";
        logger.Info( $"ブランチ名を生成した。Branch={branch}" );
        return branch;
    }

    /// <summary>ブランチ名セグメントの無効文字を安全化する。</summary>
    private static string SanitizeForBranchSegment( string text ) {
        var invalids = Path.GetInvalidFileNameChars();
        var sanitized = new string([.. text.Select(c => invalids.Contains(c) ? '-' : c)]);
        sanitized = sanitized.Trim().Trim( '.', '/', ' ' );
        sanitized = sanitized.Replace( ' ', '-' );
        sanitized = sanitized
            .Replace( "..", "." )
            .Replace( "@{", "" )
            .Replace( "~", "-" )
            .Replace( "^", "-" )
            .Replace( "?", "-" )
            .Replace( "*", "-" )
            .Replace( "[", "-" );
        return string.IsNullOrWhiteSpace( sanitized ) ? "unknown" : sanitized;
    }

    // 購読ユーティリティ
    private static void HookCollection<T>( IEnumerable<T> items, PropertyChangedEventHandler handler )
        where T : INotifyPropertyChanged {
        foreach(var i in items) i.PropertyChanged += handler;
    }

    private static void UnhookCollection<T>( IEnumerable<T> items, PropertyChangedEventHandler handler )
        where T : INotifyPropertyChanged {
        foreach(var i in items) i.PropertyChanged -= handler;
    }

    // 集約再計算
    private void RecomputeAllAgreed( bool notify ) {
        var newValue = AgreementItems.Count > 0 && AgreementItems.All(x => x.IsAgreed);
        if(newValue == _isAgreeToAllAgreementItems && !notify) return;

        _isAgreeToAllAgreementItems = newValue;
        NotifyOfPropertyChange( nameof( AllAgreed ) );
        NotifyOfPropertyChange( nameof( CanCreatePullRequest ) );
        logger.Info( $"同意項目の集約を再計算した。AllAgreed={_isAgreeToAllAgreementItems}" );
    }

    private void RecomputeAnyKindSelected( bool notify ) {
        var newValue = PullRequestChangeKinds.Any(x => x.IsChecked);
        if(newValue == _isAnyPullRequestChangeKindChecked && !notify) return;

        _isAnyPullRequestChangeKindChecked = newValue;
        NotifyOfPropertyChange( nameof( AnyKindSelected ) );
        NotifyOfPropertyChange( nameof( CanCreatePullRequest ) );
        logger.Info( $"変更種別の集約を再計算した。AnyKindSelected={_isAnyPullRequestChangeKindChecked}" );
    }

    // イベント
    private void OnAgreementCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        try {
            if(e.Action is NotifyCollectionChangedAction.Add && e.NewItems is not null) {
                foreach(var item in e.NewItems.OfType<PullRequestDialogAgreementCheckItem>())
                    item.PropertyChanged += OnAgreementItemPropertyChanged;
                logger.Info( $"同意項目が追加された。Count={AgreementItems.Count}" );
            }
            else if(e.Action is NotifyCollectionChangedAction.Remove && e.OldItems is not null) {
                foreach(var item in e.OldItems.OfType<PullRequestDialogAgreementCheckItem>())
                    item.PropertyChanged -= OnAgreementItemPropertyChanged;
                logger.Info( $"同意項目が削除された。Count={AgreementItems.Count}" );
            }
            else if(e.Action is NotifyCollectionChangedAction.Reset) {
                UnhookCollection( AgreementItems, OnAgreementItemPropertyChanged );
                HookCollection( AgreementItems, OnAgreementItemPropertyChanged );
                logger.Info( "同意項目コレクションをリセットした。" );
            }
        }
        finally {
            RecomputeAllAgreed( notify: true );
        }
    }

    private void OnKindsCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        try {
            if(e.Action is NotifyCollectionChangedAction.Add && e.NewItems is not null) {
                foreach(var vm in e.NewItems.OfType<PullRequestChangeKindViewModel>())
                    vm.PropertyChanged += OnKindItemPropertyChanged;
                logger.Info( $"変更種別が追加された。Count={PullRequestChangeKinds.Count}" );
            }
            else if(e.Action is NotifyCollectionChangedAction.Remove && e.OldItems is not null) {
                foreach(var vm in e.OldItems.OfType<PullRequestChangeKindViewModel>())
                    vm.PropertyChanged -= OnKindItemPropertyChanged;
                logger.Info( $"変更種別が削除された。Count={PullRequestChangeKinds.Count}" );
            }
            else if(e.Action is NotifyCollectionChangedAction.Reset) {
                UnhookCollection( PullRequestChangeKinds, OnKindItemPropertyChanged );
                HookCollection( PullRequestChangeKinds, OnKindItemPropertyChanged );
                logger.Info( "変更種別コレクションをリセットした。" );
            }
        }
        finally {
            RecomputeAnyKindSelected( notify: true );
            NotifyOfPropertyChange( nameof( PRTitle ) );
        }
    }

    private void OnAgreementItemPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName is not nameof( PullRequestDialogAgreementCheckItem.IsAgreed )) return;
        logger.Info( "同意項目の変更を検出した。" );
        RecomputeAllAgreed( notify: true );
    }

    private void OnKindItemPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName is not nameof( PullRequestChangeKindViewModel.IsChecked )) return;
        logger.Info( "変更種別のチェック状態変化を検出した。" );
        RecomputeAnyKindSelected( notify: true );
        NotifyOfPropertyChange( nameof( PRTitle ) );
    }

    #endregion

}