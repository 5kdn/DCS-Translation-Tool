using DcsTranslationTool.Presentation.Wpf.Services;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class ApplicationInfoServiceTests {
    private readonly Mock<ILoggingService> logger = new();

    [Fact]
    public void GetVersionは実行アセンブリにバージョン情報が含まれているときに正しいバージョンを返す() {
        // Arrange
        var sut = new ApplicationInfoService( logger.Object );

        // Act
        var version = sut.GetVersion();

        // Assert
        Assert.NotNull( version );
        Assert.True( version.Major >= 0 );
        Assert.True( version.Minor >= 0 );
    }
}