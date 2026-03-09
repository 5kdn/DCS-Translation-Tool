using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.Settings;

/// <summary>SettingsViewModel の設定永続化挙動を検証するテストを提供する。</summary>
public sealed class SettingsViewModelTests {
    /// <summary>ActivateAsync を呼び出した際に状態イベントを発火することを検証する。</summary>
    [StaFact]
    public async Task ActivateAsyncを呼び出すと状態イベントを発火する() {
        var version = new Version( 1, 2, 3, 4 );
        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        applicationInfoServiceMock
            .Setup( service => service.GetVersion() )
            .Returns( version );

        var appSettings = new AppSettings();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var dialogProviderMock = new Mock<IDialogProvider>();
        List<object> publishedMessages = [];
        var eventAggregatorMock = new Mock<IEventAggregator>();
        eventAggregatorMock
            .Setup( aggregator => aggregator.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<Func<Func<Task>, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .Returns<object, Func<Func<Task>, Task>, CancellationToken>( ( message, _, _ ) => {
                publishedMessages.Add( message );
                return Task.CompletedTask;
            } );

        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = new SettingsViewModel(
            applicationInfoServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogProviderMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            systemServiceMock.Object );

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Collection(
            publishedMessages,
            message => Assert.Equal( "Loading", message ),
            message => Assert.Equal( string.Empty, message ) );
    }

    /// <summary>Browse を呼び出した際に DcsWorldInstallDir を更新することを検証する。</summary>
    [StaFact]
    public void Browseを呼び出すとDcsWorldInstallDirを更新する() {
        var appSettings = new AppSettings();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        applicationInfoServiceMock
            .Setup( service => service.GetVersion() )
            .Returns( new Version( 1, 0 ) );

        var selectedPath = @"C:\Temp\Translations";
        var dialogProviderMock = new Mock<IDialogProvider>();
        dialogProviderMock
            .Setup( provider => provider.ShowFolderPicker(
                It.IsAny<string>(),
                out selectedPath ) )
            .Returns( true );

        var eventAggregatorMock = new Mock<IEventAggregator>();
        eventAggregatorMock
            .Setup( aggregator => aggregator.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<Func<Func<Task>, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .Returns( Task.CompletedTask );

        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = new SettingsViewModel(
            applicationInfoServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogProviderMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            systemServiceMock.Object );

        viewModel.Browse( @"C:\Start", "DcsWorldInstall" );

        Assert.Equal( selectedPath, viewModel.DcsWorldInstallDir );
        Assert.Equal( selectedPath, appSettings.DcsWorldInstallDir );
    }

    /// <summary>Browse をキャンセルした際に設定値が変化しないことを検証する。</summary>
    [StaFact]
    public void Browseをキャンセルすると設定値を保持する() {
        var appSettings = new AppSettings
        {
            ExternalAircraftInjectionDir = @"D:\Existing"
        };
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        applicationInfoServiceMock
            .Setup( service => service.GetVersion() )
            .Returns( new Version( 1, 0 ) );

        var dialogProviderMock = new Mock<IDialogProvider>();
        string dummy = string.Empty;
        dialogProviderMock
            .Setup( provider => provider.ShowFolderPicker(
                It.IsAny<string>(),
                out dummy ) )
            .Returns( false );

        var eventAggregatorMock = new Mock<IEventAggregator>();
        eventAggregatorMock
            .Setup( aggregator => aggregator.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<Func<Func<Task>, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .Returns( Task.CompletedTask );

        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = new SettingsViewModel(
            applicationInfoServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogProviderMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            systemServiceMock.Object );

        viewModel.Browse( @"C:\Start", "ExternalAircraftInjection" );

        Assert.Equal( @"D:\Existing", viewModel.ExternalAircraftInjectionDir );
        Assert.Equal( @"D:\Existing", appSettings.ExternalAircraftInjectionDir );
    }

    /// <summary>外部保存設定を更新した際に AppSettings へ反映することを検証する。</summary>
    [StaFact]
    public void 外部保存設定を更新するとAppSettingsへ反映する() {
        var appSettings = new AppSettings();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var applicationInfoServiceMock = new Mock<IApplicationInfoService>();
        applicationInfoServiceMock
            .Setup( service => service.GetVersion() )
            .Returns( new Version( 1, 0 ) );

        var dialogProviderMock = new Mock<IDialogProvider>();
        var eventAggregatorMock = new Mock<IEventAggregator>();
        eventAggregatorMock
            .Setup( aggregator => aggregator.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<Func<Func<Task>, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .Returns( Task.CompletedTask );

        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = new SettingsViewModel(
            applicationInfoServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogProviderMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            systemServiceMock.Object )
        {
            UseExternalAircraftInjectionDir = true,
            ExternalAircraftInjectionDir = @"D:\Mods\Aircraft",
            UseExternalCampaignInjectionDir = true,
            ExternalCampaignInjectionDir = @"D:\Mods\Campaigns"
        };

        Assert.True( appSettings.UseExternalAircraftInjectionDir );
        Assert.Equal( @"D:\Mods\Aircraft", appSettings.ExternalAircraftInjectionDir );
        Assert.True( appSettings.UseExternalCampaignInjectionDir );
        Assert.Equal( @"D:\Mods\Campaigns", appSettings.ExternalCampaignInjectionDir );
    }

}