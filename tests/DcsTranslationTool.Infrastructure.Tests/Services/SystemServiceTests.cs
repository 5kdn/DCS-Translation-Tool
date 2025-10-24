using System.ComponentModel;
using System.Diagnostics;

using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

public sealed class SystemServiceTests() {
    private readonly Mock<ILoggingService> logger = new();
    private readonly Mock<IProcessLauncher> processLauncher = new();

    private SystemService CreateSut() => new( logger.Object, processLauncher.Object );

    [Theory]
    [InlineData( null )]
    [InlineData( "" )]
    [InlineData( "   " )]
    public void OpenInWebBrowserはUrlが空白のときArgumentExceptionを投げる( string? url ) {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>( () => sut.OpenInWebBrowser( url! ) );

        logger.Verify( l => l.Error( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
        logger.Verify( l => l.Info( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
        processLauncher.Verify( p => p.Start( It.IsAny<ProcessStartInfo>() ), Times.Never );
    }

    [Fact]
    public void OpenInWebBrowserは起動に成功すると情報ログを出力する() {
        // Arrange
        var sut = CreateSut();
        const string url = "https://example.com/";
        processLauncher
            .Setup( p => p.Start( It.Is<ProcessStartInfo>( psi =>
                psi.FileName == url &&
                psi.UseShellExecute &&
                string.IsNullOrEmpty( psi.Arguments ) ) ) )
            .Returns( (Process?)null );

        // Act
        sut.OpenInWebBrowser( url );

        // Assert
        processLauncher.VerifyAll();
        logger.Verify(
            l => l.Info(
                It.Is<string>( m => m.Contains( url, StringComparison.Ordinal ) ),
                It.IsAny<Exception?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.Once );
        logger.Verify( l => l.Error( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
    }

    [Fact]
    public void OpenInWebBrowserは起動失敗時にエラーログを出力して例外を再スローする() {
        // Arrange
        var sut = CreateSut();
        var url = Guid.NewGuid().ToString( "N" );
        var expected = new Win32Exception( "failure" );
        processLauncher
            .Setup( p => p.Start( It.Is<ProcessStartInfo>( psi => psi.FileName == url ) ) )
            .Throws( expected );

        // Act
        var thrown = Assert.Throws<Win32Exception>( () => sut.OpenInWebBrowser( url ) );

        // Assert
        Assert.Same( expected, thrown );
        logger.Verify(
            l => l.Error(
                It.Is<string>( m => m.Contains( url, StringComparison.Ordinal ) ),
                It.Is<Exception>( ex => ReferenceEquals( ex, expected ) ),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.Once );
        logger.Verify( l => l.Info( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
    }

    [Fact]
    public void OpenDirectoryは存在しないパスでDirectoryNotFoundExceptionを投げる() {
        // Arrange
        var sut = CreateSut();
        var path = Path.Combine( Path.GetTempPath(), $"missing-{Guid.NewGuid():N}" );

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>( () => sut.OpenDirectory( path ) );

        processLauncher.Verify( p => p.Start( It.IsAny<ProcessStartInfo>() ), Times.Never );
        logger.Verify( l => l.Error( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
        logger.Verify( l => l.Info( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
    }

    [Fact]
    public void OpenDirectoryはディレクトリ指定で起動に成功すると情報ログを出力する() {
        // Arrange
        var sut = CreateSut();
        var directory = Directory.CreateDirectory( Path.Combine( Path.GetTempPath(), $"dir-{Guid.NewGuid():N}" ) );
        processLauncher
            .Setup( p => p.Start( It.Is<ProcessStartInfo>( psi =>
                psi.FileName == "explorer.exe" &&
                psi.Arguments == directory.FullName &&
                psi.UseShellExecute ) ) )
            .Returns( (Process?)null );

        try {
            // Act
            sut.OpenDirectory( directory.FullName );
        }
        finally {
            directory.Delete( true );
        }

        // Assert
        processLauncher.VerifyAll();
        logger.Verify(
            l => l.Info(
                It.Is<string>( m => m.Contains( directory.FullName, StringComparison.OrdinalIgnoreCase ) ),
                It.IsAny<Exception?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.Once );
        logger.Verify( l => l.Error( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
    }

    [Fact]
    public void OpenDirectoryはファイルパスを渡すと親ディレクトリを開く() {
        // Arrange
        var sut = CreateSut();
        var directory = Directory.CreateDirectory( Path.Combine( Path.GetTempPath(), $"dir-{Guid.NewGuid():N}" ) );
        var filePath = Path.Combine( directory.FullName, "sample.txt" );
        File.WriteAllText( filePath, "content" );
        processLauncher
            .Setup( p => p.Start( It.Is<ProcessStartInfo>( psi =>
                psi.FileName == "explorer.exe" &&
                psi.Arguments == directory.FullName &&
                psi.UseShellExecute ) ) )
            .Returns( (Process?)null );

        try {
            // Act
            sut.OpenDirectory( filePath );
        }
        finally {
            directory.Delete( true );
        }

        // Assert
        processLauncher.VerifyAll();
        logger.Verify(
            l => l.Info(
                It.Is<string>( m => m.Contains( directory.FullName, StringComparison.OrdinalIgnoreCase ) ),
                It.IsAny<Exception?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.Once );
        logger.Verify( l => l.Error( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
    }

    [Fact]
    public void OpenDirectoryは起動失敗時にエラーログを出力して例外を再スローする() {
        // Arrange
        var sut = CreateSut();
        var directory = Directory.CreateDirectory( Path.Combine( Path.GetTempPath(), $"dir-{Guid.NewGuid():N}" ) );
        var expected = new Win32Exception( "launch failed" );
        processLauncher
            .Setup( p => p.Start( It.Is<ProcessStartInfo>( psi => psi.Arguments == directory.FullName ) ) )
            .Throws( expected );

        try {
            // Act
            var thrown = Assert.Throws<Win32Exception>( () => sut.OpenDirectory( directory.FullName ) );

            // Assert
            Assert.Same( expected, thrown );
            logger.Verify(
                l => l.Error(
                    It.Is<string>( m => m.Contains( directory.FullName, StringComparison.OrdinalIgnoreCase ) ),
                    It.Is<Exception>( ex => ReferenceEquals( ex, expected ) ),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int>() ),
                Times.Once );
            logger.Verify( l => l.Info( It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>() ), Times.Never );
        }
        finally {
            directory.Delete( true );
        }
    }
}