using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Features.Upload;

namespace DcsTranslationTool.Presentation.Wpf.Features.Main;

public class MainViewModel(
    ILoggingService logger,
    INavigationService navigationService
) : Screen, IActivate {

    #region Action Messages

    public void NavToDownload() {
        logger.Info( "DownloadViewModel へ遷移する。" );
        navigationService.For<DownloadViewModel>().Navigate();
    }
    public void NavToUpload() {
        logger.Info( "UploadViewModel へ遷移する。" );
        navigationService.For<UploadViewModel>().Navigate();
    }

    #endregion

}