using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel テストの共通コンテキストを提供する。
/// </summary>
internal sealed class TranslationCreationViewModelTestContext {
    /// <summary>
    /// コンテキストを初期化する。
    /// </summary>
    public TranslationCreationViewModelTestContext( AppSettings? settings = null ) {
        Settings = settings ?? new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            SourceUserMissionDir = @"C:\UserMissions",
        };

        AppSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( Settings );
        LayoutStateServiceMock
            .Setup( service => service.Load() )
            .Returns( () => new TranslationCreationLayoutState(
                Settings.TranslationCreationWindowWidth,
                Settings.TranslationCreationWindowHeight,
                Settings.TranslationCreationDictionaryPaneRatio,
                Settings.TranslationCreationWrapDictionaryDetailsText ) );
        WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( CreateLoadResult() );
        WorkflowServiceMock
            .Setup( service => service.CreateInitialPromptPlan(
                It.IsAny<TranslationCreationImportContext>(),
                It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<TranslationDictionaryItem>>() ) )
            .Returns( TranslationCreationInitialPromptPlan.None );
        FilterServiceMock
            .Setup( service => service.ShouldInclude(
                It.IsAny<TranslationDictionaryItemRowViewModel>(),
                It.IsAny<TranslationCreationFilterOptions>() ) )
            .Returns<TranslationDictionaryItemRowViewModel, TranslationCreationFilterOptions>( static ( row, options ) =>
                ShouldInclude( row, options ) );
    }

    /// <summary>
    /// アプリ設定を取得する。
    /// </summary>
    internal AppSettings Settings { get; }

    /// <summary>
    /// 設定サービスのモックを取得する。
    /// </summary>
    internal Mock<IAppSettingsService> AppSettingsServiceMock { get; } = new();

    /// <summary>
    /// システムサービスのモックを取得する。
    /// </summary>
    internal Mock<ISystemService> SystemServiceMock { get; } = new();

    /// <summary>
    /// ログサービスのモックを取得する。
    /// </summary>
    internal Mock<ILoggingService> LoggerMock { get; } = new();

    /// <summary>
    /// ワークフローサービスのモックを取得する。
    /// </summary>
    internal Mock<ITranslationCreationWorkflowService> WorkflowServiceMock { get; } = new();

    /// <summary>
    /// レイアウト状態サービスのモックを取得する。
    /// </summary>
    internal Mock<ITranslationCreationLayoutStateService> LayoutStateServiceMock { get; } = new();

    /// <summary>
    /// ダイアログサービスのモックを取得する。
    /// </summary>
    internal Mock<ITranslationCreationDialogService> DialogServiceMock { get; } = new();

    /// <summary>
    /// フィルターサービスのモックを取得する。
    /// </summary>
    internal Mock<ITranslationCreationFilterService> FilterServiceMock { get; } = new();

    /// <summary>
    /// 通知サービスのモックを取得する。
    /// </summary>
    internal Mock<ITranslationCreationNotificationService> NotificationServiceMock { get; } = new();

    /// <summary>
    /// セッションを取得する。
    /// </summary>
    internal TranslationCreationSession Session { get; } = new();

    /// <summary>
    /// 読込結果を構成する。
    /// </summary>
    /// <param name="items">読込項目一覧。</param>
    /// <param name="statusMessage">状態メッセージ。</param>
    /// <param name="hasJapaneseDictionary">JP dictionary 有無。</param>
    /// <param name="japaneseItems">JP dictionary 項目一覧。</param>
    /// <returns>構成した結果を返す。</returns>
    internal static TranslationCreationLoadResult CreateLoadResult(
        IReadOnlyList<TranslationDictionaryItem>? items = null,
        string statusMessage = "",
        bool hasJapaneseDictionary = false,
        IReadOnlyList<TranslationDictionaryItem>? japaneseItems = null ) {
        var sourceItems = items ?? CreateDefaultItems();
        var loadedItems = sourceItems
            .Select( static item => new TranslationDictionaryItem( item.Key, item.Original )
            {
                Translated = item.Translated,
                IsEnabled = item.IsEnabled,
            } )
            .ToArray();
        var rowStates = sourceItems
            .Select( static item => new TranslationCreationRowState(
                new TranslationDictionaryItem( item.Key, item.Original )
                {
                    Translated = item.Translated,
                    IsEnabled = item.IsEnabled,
                } ) )
            .ToArray();

        return TranslationCreationLoadResult.Succeeded(
            new TranslationCreationDictionaryLoadState( loadedItems, rowStates ),
            hasJapaneseDictionary,
            japaneseItems ?? [],
            statusMessage );
    }

    /// <summary>
    /// テスト対象を生成する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>生成した ViewModel を返す。</returns>
    internal TranslationCreationViewModel CreateViewModel( string archiveFullPath = @"C:\DCSWorld\Mission1.miz" ) =>
        new(
            archiveFullPath,
            AppSettingsServiceMock.Object,
            SystemServiceMock.Object,
            LoggerMock.Object,
            Session,
            WorkflowServiceMock.Object,
            LayoutStateServiceMock.Object,
            DialogServiceMock.Object,
            FilterServiceMock.Object,
            NotificationServiceMock.Object );

    /// <summary>
    /// ViewModel をアクティブ化して初期読込を実行する。
    /// </summary>
    /// <param name="viewModel">対象 ViewModel。</param>
    /// <returns>非同期タスクを返す。</returns>
    internal static async Task ActivateAndInitializeAfterShownAsync( TranslationCreationViewModel viewModel ) {
        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        await viewModel.InitializeAfterShownAsync( TestContext.Current.CancellationToken );
    }

    /// <summary>
    /// 現在表示中の項目一覧を取得する。
    /// </summary>
    /// <param name="viewModel">対象 ViewModel。</param>
    /// <returns>表示中の項目一覧を返す。</returns>
    internal static TranslationDictionaryItemRowViewModel[] GetVisibleDictionaryItems( TranslationCreationViewModel viewModel ) =>
        [.. viewModel.DictionaryItems.Where( viewModel.ShouldIncludeRow )];

    private static IReadOnlyList<TranslationDictionaryItem> CreateDefaultItems() =>
    [
        new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ),
    ];

    private static bool ShouldInclude( TranslationDictionaryItemRowViewModel row, TranslationCreationFilterOptions options ) {
        if(row.IsEnabled && !options.ShowEnabledItems) {
            return false;
        }

        if(!row.IsEnabled && !options.ShowDisabledItems) {
            return false;
        }

        if(options.HidePossibleNonTranslationTargets && row.IsPossibleNonTranslationTarget) {
            return false;
        }

        if(options.HideEmptyOriginal && string.IsNullOrWhiteSpace( row.Original )) {
            return false;
        }

        if(options.ShowOnlyUntranslated && !string.IsNullOrWhiteSpace( row.Translated )) {
            return false;
        }

        return true;
    }
}