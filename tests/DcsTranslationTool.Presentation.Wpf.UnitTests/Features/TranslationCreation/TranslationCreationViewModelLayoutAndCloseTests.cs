using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel のレイアウトと終了状態の動作を検証する。
/// </summary>
public sealed class TranslationCreationViewModelLayoutAndCloseTests {
    [Fact]
    public void コンストラクタは保存済みウィンドウ状態を補正して初期化する() {
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            SourceUserMissionDir = @"C:\UserMissions",
            TranslationCreationWindowWidth = 100,
            TranslationCreationWindowHeight = 0,
            TranslationCreationDictionaryPaneRatio = 99,
            TranslationCreationWrapDictionaryDetailsText = true,
        };
        var context = new TranslationCreationViewModelTestContext( settings );
        var viewModel = CreateViewModel( context );

        Assert.Equal( TranslationCreationViewModel.MinWindowWidth, viewModel.WindowWidth );
        Assert.Equal( TranslationCreationViewModel.DefaultWindowHeight, viewModel.WindowHeight );
        Assert.Equal( TranslationCreationViewModel.MaxDictionaryPaneRatio, viewModel.DictionaryPaneRatio );
        Assert.True( viewModel.IsDictionaryDetailsWrapEnabled );
    }

    [Fact]
    public void WindowWidth設定は補正してレイアウト状態を保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = CreateViewModel( context );

        viewModel.WindowWidth = 100;

        Assert.Equal( TranslationCreationViewModel.MinWindowWidth, context.Settings.TranslationCreationWindowWidth );
    }

    [Fact]
    public void DictionaryPaneRatio設定は補正してレイアウト状態を保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = CreateViewModel( context );

        viewModel.DictionaryPaneRatio = 99;

        Assert.Equal( TranslationCreationViewModel.MaxDictionaryPaneRatio, context.Settings.TranslationCreationDictionaryPaneRatio );
    }

    [Fact]
    public void IsDictionaryDetailsWrapEnabled設定はレイアウト状態を保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = CreateViewModel( context );

        viewModel.IsDictionaryDetailsWrapEnabled = true;

        Assert.True( context.Settings.TranslationCreationWrapDictionaryDetailsText );
    }

    /// <summary>
    /// レイアウト状態サービス実体を利用する ViewModel を生成する。
    /// </summary>
    /// <param name="context">テストコンテキスト。</param>
    /// <returns>生成した ViewModel を返す。</returns>
    private static TranslationCreationViewModel CreateViewModel( TranslationCreationViewModelTestContext context ) =>
        new(
            @"C:\DCSWorld\Mission1.miz",
            context.AppSettingsServiceMock.Object,
            context.SystemServiceMock.Object,
            context.LoggerMock.Object,
            context.Session,
            context.WorkflowServiceMock.Object,
            new TranslationCreationLayoutStateService( context.AppSettingsServiceMock.Object ),
            context.DialogServiceMock.Object,
            context.FilterServiceMock.Object,
            context.NotificationServiceMock.Object );
}