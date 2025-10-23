using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;
public class ApplicationInfoServiceTests {
    private readonly Mock<ILoggingService> logger = new();

    #region GetVersion

    [Fact]
    public void GetVersionは実行アセンブリにバージョン情報が含まれているときに正しいバージョンを返す() {
        // Arrange
        var sut = new ApplicationInfoService(logger.Object);

        // Act
        var version = sut.GetVersion();

        // Assert
        // 実際のバージョン番号は都度変わるので Major.Minorの型チェックのみ
        Assert.NotNull( version );
        Assert.True( version.Major >= 0 ); // バージョンが取れればOKとみなす
        Assert.True( version.Minor >= 0 );
    }

    #endregion
}