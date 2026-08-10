using System.Windows.Controls;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.UI.Dialogs.Parameters;

/// <summary>
/// <see cref="ApplicationUpdateProgressDialogParameters"/> の更新実行結果を検証する。
/// </summary>
public sealed class ApplicationUpdateProgressDialogParametersTests {
    /// <summary>
    /// 更新処理で予期しない例外が発生してもダイアログ処理から例外を送出しないことを検証する。
    /// </summary>
    [StaFact]
    public async Task RunAsyncは更新処理の例外を送出しない() {
        var logger = new Mock<ILoggingService>();
        var expectedException = new InvalidOperationException( "test failure" );
        using var parameters = new ApplicationUpdateProgressDialogParameters(
            "更新",
            (_, _) => Task.FromException<Result<ApplicationUpdateStageResult>>( expectedException ),
            logger.Object );

        var exception = await Record.ExceptionAsync( () => parameters.RunAsync( new UserControl() ) );

        Assert.Null( exception );
        Assert.False( parameters.IsRunning );
        logger.Verify(
            value => value.Error(
                It.IsAny<string>(),
                expectedException,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.Once );
    }
}