using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Features.Settings;

public class SettingsViewModel(
    IApplicationInfoService applicationInfoService,
    IAppSettingsService appSettingsService,
    IDialogProvider dialogProvider,
    IEventAggregator eventAggregator,
    ILoggingService logger,
    ISystemService systemService
) : Screen, IActivate {

    #region Fields

    private string _sourceAircraftDir = appSettingsService.Settings.SourceAircraftDir;
    private string _sourceDlcCampaignDir = appSettingsService.Settings.SourceDlcCampaignDir;
    private string _sourceUserMissionDir = appSettingsService.Settings.SourceUserMissionDir;
    private string _translateFileDir = appSettingsService.Settings.TranslateFileDir;

    #endregion

    #region Properties
    public string SourceAircraftDir {
        get => _sourceAircraftDir;
        set {
            if(!Set( ref _sourceAircraftDir, value )) return;
            appSettingsService.Settings.SourceAircraftDir = value;
            logger.Info( $"SourceAircraftDir を更新する。Value={value}" );
        }
    }

    public string SourceDlcCampaignDir {
        get => _sourceDlcCampaignDir;
        set {
            if(!Set( ref _sourceDlcCampaignDir, value )) return;
            appSettingsService.Settings.SourceDlcCampaignDir = value;
            logger.Info( $"SourceDlcCampaignDir を更新する。Value={value}" );
        }
    }

    public string SourceUserMissionDir {
        get => _sourceUserMissionDir;
        set {
            if(!Set( ref _sourceUserMissionDir, value )) return;
            appSettingsService.Settings.SourceUserMissionDir = value;
            logger.Info( $"SourceUserMissionDir を更新する。Value={value}" );
        }
    }

    public string TranslateFileDir {
        get => _translateFileDir;
        set {
            if(!Set( ref _translateFileDir, value )) return;
            appSettingsService.Settings.TranslateFileDir = value;
            logger.Info( $"TranslateFileDir を更新する。Value={value}" );
        }
    }

    /// <summary>
    /// 画面に表示するバージョン情報
    /// </summary>
    public string VersionDescription { get; init; } = $"{Resources.Strings_Shared.AppDisplayName} - {applicationInfoService.GetVersion()}";

    public string LicenseType { get; init; } = appSettingsService.Settings.LicenseType;

    #endregion

    #region Lifecycle

    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( "SettingsViewModel をアクティブ化する。" );
        await eventAggregator.PublishOnUIThreadAsync( "Loading", cancellationToken: cancellationToken );
        await eventAggregator.PublishOnUIThreadAsync( string.Empty, cancellationToken: cancellationToken );
    }

    protected override async Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"SettingsViewModel を非アクティブ化する。Close={close}" );
        await eventAggregator.PublishOnUIThreadAsync( "Closing", cancellationToken: cancellationToken );
    }

    #endregion

    #region Action Messages

    /// <summary>
    /// プライバシーステートメントを開くコマンド。
    /// </summary>
    public void BrowseToPrivacyStatement() {
        logger.Info( $"プライバシーステートメントを開く。Url={appSettingsService.Settings.PrivacyStatement}" );
        systemService.OpenInWebBrowser( appSettingsService.Settings.PrivacyStatement );
    }

    /// <summary>
    /// フォルダ選択を実行し、key に応じて該当プロパティを更新する。
    /// </summary>
    public void Browse( string path, string key ) {
        logger.Info( $"フォルダー選択を開始する。Path={path}, Key={key}" );
        if(dialogProvider.ShowFolderPicker( path, out var selected )) {
            switch(key) {
                case "Aircraft":
                    SourceAircraftDir = selected;
                    logger.Info( $"SourceAircraftDir ディレクトリを設定した。Selected={selected}" );
                    break;
                case "DlcCampaign":
                    SourceDlcCampaignDir = selected;
                    logger.Info( $"SourceDlcCampaignDir ディレクトリを設定した。Selected={selected}" );
                    break;
                case "UserMission":
                    SourceUserMissionDir = selected;
                    logger.Info( $"SourceUserMissionDir ディレクトリを設定した。Selected={selected}" );
                    break;
                case "TranslateFile":
                    TranslateFileDir = selected;
                    logger.Info( $"TranslateFileDir ディレクトリを設定した。Selected={selected}" );
                    break;
                default:
                    logger.Warn( $"未対応のキーを受信した。Key={key}" );
                    break;
            }
        }
        else {
            logger.Info( "フォルダー選択がキャンセルされた。" );
        }
    }

    public void BrowseToReleaseLatestPage() {
        logger.Info( $"最新リリースページを開く。Url={appSettingsService.Settings.ReleaseLatestUrl}" );
        systemService.OpenInWebBrowser( appSettingsService.Settings.ReleaseLatestUrl );
    }

    public void BrowseToLicensePage() {
        logger.Info( $"ライセンスページを開く。Url={appSettingsService.Settings.LicenseUrl}" );
        systemService.OpenInWebBrowser( appSettingsService.Settings.LicenseUrl );
    }

    #endregion

}