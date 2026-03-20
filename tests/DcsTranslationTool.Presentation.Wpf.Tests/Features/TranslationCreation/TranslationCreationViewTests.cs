using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using MaterialDesignColors;

using MaterialDesignThemes.Wpf;

using Moq;

using TextBox = System.Windows.Controls.TextBox;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView の生成可否を検証する。
/// </summary>
public sealed class TranslationCreationViewTests {
    [StaFact]
    public async Task DataContextを設定して表示しても例外が発生しない() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "line1\nline2" ) { Translated = "translated1\ntranslated2" }
                ] ) );

        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        view.Show();
        view.UpdateLayout();
        view.Close();

        Assert.NotNull( view );
    }

    [StaFact]
    public void TranslationCreationViewは専用DialogHost識別子を持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();

        var dialogHost = Assert.IsType<DialogHost>( view.Content );

        Assert.NotNull( dialogHost );
        Assert.Equal( TranslationCreationDialogHostIdentifiers.Confirmation, dialogHost.Identifier );
    }

    [StaFact]
    public void TranslationCreationViewはWindow内Snackbarを持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();

        var snackbar = (System.Windows.Controls.Control?)view.FindName( "ExportSnackbar" );

        Assert.NotNull( snackbar );
    }

    [StaFact]
    public async Task TranslationCreationViewはDictionaryDataGridを持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var dataGrid = view.FindName( "DictionaryDataGrid" );

        view.Close();

        Assert.NotNull( dataGrid );
    }

    [StaFact]
    public async Task TranslationCreationViewのDictionaryDataGridは仮想化設定を有効化する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var dataGrid = Assert.IsType<DataGrid>( view.FindName( "DictionaryDataGrid" ) );

        view.Close();

        Assert.True( dataGrid.EnableRowVirtualization );
        Assert.True( dataGrid.EnableColumnVirtualization );
        Assert.True( VirtualizingPanel.GetIsVirtualizing( dataGrid ) );
        Assert.Equal( VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode( dataGrid ) );
    }

    [StaFact]
    public async Task TranslationCreationViewはDictionaryPaneGridSplitterを持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var gridSplitter = view.FindName( "DictionaryPaneGridSplitter" );

        view.Close();

        Assert.NotNull( gridSplitter );
    }

    [StaFact]
    public async Task TranslationCreationViewのDataGridヘッダーは日本語リソースを参照する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var dataGrid = Assert.IsType<System.Windows.Controls.DataGrid>( view.FindName( "DictionaryDataGrid" ) );

        view.Close();

        Assert.Equal( Strings_Translation.CreateTranslationDataGridEnabledHeaderText, dataGrid.Columns[0].Header );
        Assert.Equal( Strings_Translation.CreateTranslationDataGridKeyHeaderText, dataGrid.Columns[1].Header );
        Assert.Equal( Strings_Translation.CreateTranslationDataGridOriginalHeaderText, dataGrid.Columns[2].Header );
        Assert.Equal( Strings_Translation.CreateTranslationDataGridTranslatedHeaderText, dataGrid.Columns[3].Header );
        Assert.IsType<System.Windows.Controls.DataGridTemplateColumn>( dataGrid.Columns[2] );
        Assert.IsType<System.Windows.Controls.DataGridTextColumn>( dataGrid.Columns[3] );
    }

    [StaFact]
    public async Task TranslationCreationViewの原文列セルテンプレートはコピーボタンを持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" ) { Translated = "translated" }
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var dataGrid = Assert.IsType<DataGrid>( view.FindName( "DictionaryDataGrid" ) );
        var copyButton = FindOriginalColumnCopyButton( dataGrid );

        view.Close();

        Assert.NotNull( copyButton );
        Assert.Equal( Strings_Translation.CreateTranslationCopyButtonContent, copyButton.Content );
    }

    [StaFact]
    public async Task TranslationCreationViewの詳細ラベルはDataGridヘッダーと同じ日本語リソースを参照する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        view.Show();
        view.UpdateLayout();

        var originalLabel = Assert.IsType<System.Windows.Controls.TextBlock>( view.FindName( "SelectedOriginalLabel" ) );
        var translatedLabel = Assert.IsType<System.Windows.Controls.TextBlock>( view.FindName( "SelectedTranslatedLabel" ) );

        view.Close();

        Assert.Equal( Strings_Translation.CreateTranslationDataGridOriginalHeaderText, originalLabel.Text );
        Assert.Equal( Strings_Translation.CreateTranslationDataGridTranslatedHeaderText, translatedLabel.Text );
    }

    [StaFact]
    public async Task TranslationCreationViewは詳細折り返しチェックボックスを持ち日本語リソースを参照する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        view.Show();
        view.UpdateLayout();

        var wrapCheckBox = Assert.IsType<CheckBox>( view.FindName( "DictionaryDetailsWrapCheckBox" ) );

        view.Close();

        Assert.Equal( Strings_Translation.CreateTranslationDictionaryDetailsWrapCheckBoxContent, wrapCheckBox.Content );
        Assert.NotNull( BindingOperations.GetBindingExpressionBase( wrapCheckBox, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty ) );
    }

    [StaFact]
    public async Task TranslationCreationViewは保存済み折り返し設定を詳細TextBoxへ反映する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationWrapDictionaryDetailsText = false
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" ) { Translated = "translated" }
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        view.Show();
        view.UpdateLayout();

        var originalTextBox = Assert.IsType<TextBox>( view.FindName( "SelectedOriginalTextBox" ) );
        var translatedTextBox = Assert.IsType<TextBox>( view.FindName( "SelectedTranslatedTextBox" ) );

        view.Close();

        Assert.Equal( TextWrapping.NoWrap, originalTextBox.TextWrapping );
        Assert.Equal( TextWrapping.NoWrap, translatedTextBox.TextWrapping );
        Assert.Equal( ScrollBarVisibility.Auto, originalTextBox.HorizontalScrollBarVisibility );
        Assert.Equal( ScrollBarVisibility.Auto, translatedTextBox.HorizontalScrollBarVisibility );
    }

    [StaFact]
    public async Task TranslationCreationViewの詳細折り返し設定変更は両TextBoxの折り返しを切り替える() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationWrapDictionaryDetailsText = true
        };
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" ) { Translated = "translated" }
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        view.Show();
        view.UpdateLayout();

        var wrapCheckBox = Assert.IsType<CheckBox>( view.FindName( "DictionaryDetailsWrapCheckBox" ) );
        var originalTextBox = Assert.IsType<TextBox>( view.FindName( "SelectedOriginalTextBox" ) );
        var translatedTextBox = Assert.IsType<TextBox>( view.FindName( "SelectedTranslatedTextBox" ) );
        Assert.Equal( TextWrapping.Wrap, originalTextBox.TextWrapping );
        Assert.Equal( TextWrapping.Wrap, translatedTextBox.TextWrapping );

        viewModel.SetDictionaryDetailsWrapEnabled( false );
        view.UpdateLayout();

        view.Close();

        Assert.False( wrapCheckBox.IsChecked );
        Assert.Equal( TextWrapping.NoWrap, originalTextBox.TextWrapping );
        Assert.Equal( TextWrapping.NoWrap, translatedTextBox.TextWrapping );
        Assert.Equal( ScrollBarVisibility.Auto, originalTextBox.HorizontalScrollBarVisibility );
        Assert.Equal( ScrollBarVisibility.Auto, translatedTextBox.HorizontalScrollBarVisibility );
        Assert.False( settings.TranslationCreationWrapDictionaryDetailsText );
    }

    [StaFact]
    public async Task TranslationCreationViewは保存済み比率を初期レイアウトへ反映する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationDictionaryPaneRatio = 3.5
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var dataGridRow = Assert.IsType<RowDefinition>( view.FindName( "DictionaryDataGridRowDefinition" ) );
        var detailsRow = Assert.IsType<RowDefinition>( view.FindName( "DictionaryDetailsRowDefinition" ) );

        view.Close();

        Assert.Equal( 3.5, viewModel.DictionaryPaneRatio );
        Assert.Equal( GridUnitType.Star, dataGridRow.Height.GridUnitType );
        Assert.Equal( 3.5, dataGridRow.Height.Value );
        Assert.Equal( GridUnitType.Star, detailsRow.Height.GridUnitType );
        Assert.Equal( 1, detailsRow.Height.Value );
    }

    [StaFact]
    public async Task TranslationCreationViewは保存済みウィンドウサイズを初期表示へ反映する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationWindowWidth = 1400,
            TranslationCreationWindowHeight = 920
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var actualWidth = view.Width;
        var actualHeight = view.Height;

        view.Close();

        Assert.Equal( 1400, viewModel.WindowWidth );
        Assert.Equal( 920, viewModel.WindowHeight );
        Assert.Equal( 1400, actualWidth );
        Assert.Equal( 920, actualHeight );
    }

    [StaFact]
    public async Task TranslationCreationViewは不正な保存ウィンドウサイズを既定値へ補正する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationWindowWidth = double.NaN,
            TranslationCreationWindowHeight = 0
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var actualWidth = view.Width;
        var actualHeight = view.Height;

        view.Close();

        Assert.Equal( TranslationCreationViewModel.DefaultWindowWidth, viewModel.WindowWidth );
        Assert.Equal( 760, viewModel.WindowHeight );
        Assert.Equal( 900, actualWidth );
        Assert.Equal( 760, actualHeight );
    }

    [StaFact]
    public async Task TranslationCreationViewは不正な保存比率を既定値へ補正する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationDictionaryPaneRatio = double.NaN
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var dataGridRow = Assert.IsType<RowDefinition>( view.FindName( "DictionaryDataGridRowDefinition" ) );

        view.Close();

        Assert.Equal( TranslationCreationViewModel.DefaultDictionaryPaneRatio, viewModel.DictionaryPaneRatio );
        Assert.Equal( 2, dataGridRow.Height.Value );
    }

    [StaFact]
    public async Task TranslationCreationViewはClose時に現在の比率を設定へ保存する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld"
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var dataGridRow = Assert.IsType<RowDefinition>( view.FindName( "DictionaryDataGridRowDefinition" ) );
        var detailsRow = Assert.IsType<RowDefinition>( view.FindName( "DictionaryDetailsRowDefinition" ) );
        dataGridRow.Height = new GridLength( 4, GridUnitType.Star );
        detailsRow.Height = new GridLength( 1, GridUnitType.Star );
        view.UpdateLayout();

        view.Close();

        Assert.Equal( 4, viewModel.DictionaryPaneRatio );
        Assert.Equal( 4, settings.TranslationCreationDictionaryPaneRatio );
    }

    [StaFact]
    public async Task TranslationCreationViewはClose時に現在のウィンドウサイズを設定へ保存する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld"
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( settings );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.Width = 1500;
        view.Height = 980;
        view.UpdateLayout();

        view.Close();

        Assert.Equal( 1500, viewModel.WindowWidth );
        Assert.Equal( 980, viewModel.WindowHeight );
        Assert.Equal( 1500, settings.TranslationCreationWindowWidth );
        Assert.Equal( 980, settings.TranslationCreationWindowHeight );
    }

    [StaFact]
    public async Task TranslationCreationViewはImportSplitButtonとExportSplitButtonを持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var importSplitButton = view.FindName( "ImportSplitButton" );
        var exportSplitButton = view.FindName( "ExportSplitButton" );

        view.Close();

        Assert.NotNull( importSplitButton );
        Assert.NotNull( exportSplitButton );
    }

    [StaFact]
    public async Task TranslationCreationViewのSplitButton初期表示は期待どおりである() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var importSplitButton = Assert.IsType<SplitButton>( view.FindName( "ImportSplitButton" ) );
        var exportSplitButton = Assert.IsType<SplitButton>( view.FindName( "ExportSplitButton" ) );

        view.Close();

        Assert.Equal( Strings_Translation.CreateTranslationImportDictionaryButtonContent, viewModel.ImportSplitButtonContent );
        Assert.Equal( Strings_Translation.CreateTranslationExportButtonContent, viewModel.ExportSplitButtonContent );
        Assert.NotNull( BindingOperations.GetBindingExpressionBase( importSplitButton, System.Windows.Controls.ContentControl.ContentProperty ) );
        Assert.NotNull( BindingOperations.GetBindingExpressionBase( exportSplitButton, System.Windows.Controls.ContentControl.ContentProperty ) );
    }

    [StaFact]
    public async Task TranslationCreationViewのImportSplitButtonPopupはdictionary読み込み項目を持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var importSplitButton = Assert.IsType<SplitButton>( view.FindName( "ImportSplitButton" ) );
        var popupContent = Assert.IsType<StackPanel>( importSplitButton.PopupContent );
        var dictionaryImportButton = popupContent.Children
            .OfType<Button>()
            .Single( button => Equals( button.Content, Strings_Translation.CreateTranslationImportDictionaryButtonContent ) );

        view.Close();

        Assert.NotNull( dictionaryImportButton );
    }

    [StaFact]
    public async Task TranslationCreationViewは無効行選択時にTranslatedTextBoxを無効化する() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" ) {
                        IsEnabled = false
                    }
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        view.Show();
        view.UpdateLayout();

        var translatedTextBox = Assert.IsType<TextBox>( view.FindName( "SelectedTranslatedTextBox" ) );
        var dataGrid = Assert.IsType<System.Windows.Controls.DataGrid>( view.FindName( "DictionaryDataGrid" ) );

        view.Close();

        Assert.False( viewModel.CanEditSelectedTranslated );
        Assert.NotNull( BindingOperations.GetBindingExpressionBase( translatedTextBox, UIElement.IsEnabledProperty ) );
        Assert.Equal( 4, dataGrid.Columns.Count );
    }

    [StaFact]
    public async Task TranslationCreationViewは有効無効フィルターのCheckBoxを持つ() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "original" )
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();

        var showEnabledCheckBox = Assert.IsType<System.Windows.Controls.CheckBox>( view.FindName( "ShowEnabledItemsCheckBox" ) );
        var showDisabledCheckBox = Assert.IsType<System.Windows.Controls.CheckBox>( view.FindName( "ShowDisabledItemsCheckBox" ) );

        view.Close();

        Assert.NotNull( BindingOperations.GetBindingExpressionBase( showEnabledCheckBox, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty ) );
        Assert.NotNull( BindingOperations.GetBindingExpressionBase( showDisabledCheckBox, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty ) );
    }

    [StaFact]
    public async Task TranslationCreationViewは狭いTextBoxでも改行マーカー描画で例外にならない() {
        EnsureApplicationResources();

        var view = new TranslationCreationView
        {
            Width = 320,
            Height = 240
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var dialogProviderMock = new Mock<IDialogProvider>();
        var systemServiceMock = new Mock<ISystemService>();
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld"
            } );
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem(
                        "DictKey_descriptionFoo_1",
                        "line1\nline2\nline3\nline4\nline5\nline6" ) {
                        Translated = "translated1\ntranslated2\ntranslated3\ntranslated4\ntranslated5\ntranslated6"
                    }
                ] ) );
        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            appSettingsServiceMock.Object,
            applicationInfoServiceMock.Object,
            dialogServiceMock.Object,
            dialogProviderMock.Object,
            systemServiceMock.Object,
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        view.Show();
        view.UpdateLayout();

        var originalTextBox = Assert.IsType<TextBox>( view.GetLogicalChildCollection<TextBox>().First( textBox => textBox.IsReadOnly ) );
        var translatedTextBox = Assert.IsType<TextBox>( view.FindName( "SelectedTranslatedTextBox" ) );
        originalTextBox.Width = 80;
        originalTextBox.Height = 48;
        translatedTextBox.Width = 80;
        translatedTextBox.Height = 48;
        originalTextBox.ScrollToEnd();
        translatedTextBox.ScrollToEnd();
        view.UpdateLayout();

        view.Close();

        Assert.True( true );
    }

    [StaFact]
    public async Task ExecuteWindowLoadedAsyncはHandleWindowLoadedAsyncの完了まで待機する() {
        EnsureApplicationResources();

        var dispatcherReadySource = new TaskCompletionSource<Dispatcher>();
        var actionCompletionSource = new TaskCompletionSource();
        var invocationStartedSource = new TaskCompletionSource();
        var invocationCount = 0;
        var dispatcherThread = new Thread( () => {
            dispatcherReadySource.SetResult( Dispatcher.CurrentDispatcher );
            Dispatcher.Run();
        } );

        dispatcherThread.SetApartmentState( ApartmentState.STA );
        dispatcherThread.Start();

        var dispatcher = await dispatcherReadySource.Task.WaitAsync( TestContext.Current.CancellationToken );

        try {
            var executeTask = TranslationCreationView.ExecuteWindowLoadedAsync(
                dispatcher,
                async () => {
                    Interlocked.Increment( ref invocationCount );
                    invocationStartedSource.TrySetResult();
                    await actionCompletionSource.Task;
                },
                DispatcherPriority.Background,
                TestContext.Current.CancellationToken );

            await invocationStartedSource.Task.WaitAsync( TestContext.Current.CancellationToken );

            Assert.False( executeTask.IsCompleted );
            Assert.Equal( 1, Volatile.Read( ref invocationCount ) );

            actionCompletionSource.SetResult();
            await executeTask.WaitAsync( TestContext.Current.CancellationToken );
            Assert.Equal( 1, Volatile.Read( ref invocationCount ) );
        }
        finally {
            dispatcher.InvokeShutdown();
            dispatcherThread.Join();
        }
    }

    public static TheoryData<string, int[]> GetNewlineMarkerIndicesTestData => new()
    {
        { string.Empty, [] },
        { "single line", [] },
        { "line1\nline2", [5] },
        { "line1\r\nline2", [5] },
        { "\nline2\n", [0, 6] },
        { "line1\rline2", [5] }
    };

    [Theory]
    [MemberData( nameof( GetNewlineMarkerIndicesTestData ) )]
    public void 改行マーカー位置抽出はCRLFとLFを正しく扱う( string text, int[] expectedIndices ) {
        var actual = TextBoxNewlineMarkerAdorner.GetNewlineMarkerIndices( text );

        Assert.Equal( expectedIndices, actual );
    }

    private static void EnsureApplicationResources() {
        var app = System.Windows.Application.Current ?? new System.Windows.Application();
        var mergedDictionaries = app.Resources.MergedDictionaries;

        if(mergedDictionaries.Count > 0) {
            return;
        }

        mergedDictionaries.Add( new BundledTheme
        {
            BaseTheme = BaseTheme.Light,
            PrimaryColor = PrimaryColor.DeepPurple,
            SecondaryColor = SecondaryColor.Lime
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml", UriKind.Absolute )
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Button.xaml", UriKind.Absolute )
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.PopupBox.xaml", UriKind.Absolute )
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.SplitButton.xaml", UriKind.Absolute )
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/CustomBrushes.xaml", UriKind.Absolute )
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/_Thickness.xaml", UriKind.Absolute )
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/WindowStyle.xaml", UriKind.Absolute )
        } );
        mergedDictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/ButtonStyle.xaml", UriKind.Absolute )
        } );
    }

    private static Button FindOriginalColumnCopyButton( DataGrid dataGrid ) {
        var column = Assert.IsType<System.Windows.Controls.DataGridTemplateColumn>( dataGrid.Columns[2] );
        var templateRoot = Assert.IsType<FrameworkElement>( column.CellTemplate.LoadContent(), exactMatch: false );
        return Assert.IsType<Button>( templateRoot.FindName( "PART_CopyOriginalButton" ) );
    }

}

file static class DependencyObjectExtensions {
    internal static IReadOnlyList<T> GetLogicalChildCollection<T>( this DependencyObject parent ) where T : DependencyObject {
        List<T> children = [];
        GetLogicalChildCollection( parent, children );
        return children;
    }

    private static void GetLogicalChildCollection<T>( DependencyObject parent, ICollection<T> result ) where T : DependencyObject {
        foreach(var child in LogicalTreeHelper.GetChildren( parent ).OfType<DependencyObject>()) {
            if(child is T typedChild) {
                result.Add( typedChild );
            }

            GetLogicalChildCollection( child, result );
        }
    }
}