using System.Windows;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

using FluentResults;

using MaterialDesignColors;

using MaterialDesignThemes.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView の生成可否を検証する。
/// </summary>
public sealed class TranslationCreationViewTests {
    [StaFact]
    public async Task DataContextを設定して表示しても例外が発生しない() {
        EnsureApplicationResources();

        var view = new TranslationCreationView();
        var loggerMock = new Mock<ILoggingService>();
        var dictionaryServiceMock = new Mock<ITranslationDictionaryService>();
        dictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "original" )
                ] ) );

        var viewModel = new TranslationCreationViewModel(
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
            loggerMock.Object,
            dictionaryServiceMock.Object );
        view.DataContext = viewModel;
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        view.Show();
        view.UpdateLayout();
        view.Close();

        Assert.NotNull( view );
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
}