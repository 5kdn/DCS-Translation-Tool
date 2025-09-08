using System.Text;
using System.Text.Json;

using DcsTranslationTool.Infrastructure.Services;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

public class LoggingServiceTests : IDisposable {
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public LoggingServiceTests() {
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        try { LogManager.Shutdown(); } catch { }
        if(Directory.Exists( _tempDir )) try { Directory.Delete( _tempDir, true ); } catch { }
        GC.SuppressFinalize( this );
    }

    #region Helper

    private static LoggingConfiguration Configure( string dir, LogLevel min, bool enableDebug = true ) {
        var options = new LoggingOptions
        {
            LogDirectory = dir,
            FileName = "app.log",
            MinLevel = min,
            EnableDebugOutput = enableDebug
        };
        var cfg = LoggingService.BuildConfiguration(options);
        LogManager.Configuration = cfg;
        return cfg;
    }

    private static void Flush() => LogManager.Flush( TimeSpan.FromSeconds( 5 ) );

    private static string[] ReadLines( string logPath ) {
        Flush();
        if(!File.Exists( logPath )) return [];
        using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        var content = sr.ReadToEnd();
        return content.Length == 0 ? [] : content.Split( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries );
    }

    private static JsonDocument LastJson( string logPath ) {
        var last = ReadLines(logPath).Last();
        return JsonDocument.Parse( last );
    }

    #endregion

    #region BuildConfiguration

    [Fact]
    public void BuildConfigurationはディレクトリが存在しないとき作成する() {
        // Arrange & Act
        var targetDir = Path.Combine(_tempDir, "sub");
        _ = LoggingService.BuildConfiguration( new LoggingOptions { LogDirectory = targetDir } );

        // Assert
        Assert.True( Directory.Exists( targetDir ), "ログディレクトリが作成されていません" );
    }

    [Theory]
    [InlineData( false, false )]
    [InlineData( true, true )]
    public void BuildConfigurationはデバッグ出力設定に応じてデバッグターゲット有無が変わる( bool enableDebug, bool expectedHasDebug ) {
        // Arrange
        var options = new LoggingOptions
        {
            LogDirectory = Path.Combine( _tempDir, enableDebug ? "cfg-enable" : "cfg-disable" ),
            EnableDebugOutput = enableDebug
        };

        // Act
        var config = LoggingService.BuildConfiguration( options );

        // Assert
        var hasDebug = config.LoggingRules.Any( r => r.Targets.Any( t => t is AsyncTargetWrapper aw && aw.WrappedTarget is DebuggerTarget ) );
        Assert.Equal( expectedHasDebug, hasDebug );
    }

    #endregion

    #region Trace

    [Theory]
    [InlineData( "Trace", 1 )]
    [InlineData( "Info", 0 )]
    public void Traceは最小レベル設定に応じて書き出し件数が変化する( string minLevel, int expectedDelta ) {
        // Arrange
        var logPath = Path.Combine( _tempDir, "app.log" );
        Configure( _tempDir, LogLevel.FromString( minLevel ) );
        var sut = new LoggingService();
        var before = ReadLines( logPath ).Length;

        // Act
        sut.Trace( "log message" );

        // Assert
        var after = ReadLines( logPath ).Length;
        Assert.Equal( expectedDelta, after - before );
    }

    #endregion

    #region Debug

    [Theory]
    [InlineData( "Debug", 1 )]
    [InlineData( "Info", 0 )]
    public void Debugは最小レベル設定に応じて書き出し件数が変化する( string minLevel, int expectedDelta ) {
        // Arrange
        var logPath = Path.Combine( _tempDir, "app.log" );
        Configure( _tempDir, LogLevel.FromString( minLevel ), enableDebug: false );
        var sut = new LoggingService();
        var before = ReadLines( logPath ).Length;

        // Act
        sut.Debug( "log message" );

        // Assert
        var after = ReadLines( logPath ).Length;
        Assert.Equal( expectedDelta, after - before );
    }

    #endregion

    #region Info

    [Fact]
    public void Infoはメッセージが空文字でも出力する() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        // Act
        sut.Info( string.Empty );

        // Assert
        using var jd = LastJson(logPath);
        var root = jd.RootElement;
        var exists = root.TryGetProperty( "message", out var messageProp );
        Assert.True( exists, "message プロパティが存在しません" );
        Assert.Equal( string.Empty, messageProp.GetString() );
    }

    #endregion

    #region Warn

    [Fact]
    public void Warnはmemberを指定するとmemberが出力される() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        // Act
        sut.Warn( "log message", null, member: "DoSomething" );

        // Assert
        using var jd = LastJson(logPath);
        var root = jd.RootElement;
        var ok = root.TryGetProperty( "member", out var memberProp );
        Assert.True( ok, "member が欠落しています" );
        Assert.Equal( "DoSomething", memberProp.GetString() );
    }

    [Fact]
    public void Warnはmemberを指定しないときmemberは出力されない() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        // Act
        sut.Warn( "log message" );

        // Assert
        using var jd = LastJson(logPath);
        var exists = jd.RootElement.TryGetProperty("member", out _);
        Assert.False( exists );
    }

    #endregion

    #region Error

    [Fact]
    public void Errorは例外を指定するとexceptionが例外文字列を含む() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();
        var ex = new InvalidOperationException("Boom");

        // Act
        sut.Error( "log message", ex );

        // Assert
        using var jd = LastJson(logPath);
        var root = jd.RootElement;
        var ok = root.TryGetProperty( "exception", out var exProp );
        Assert.True( ok, "exception が欠落しています" );
        Assert.Contains( "InvalidOperationException", exProp.GetString() );
    }

    [Fact]
    public void Errorはfileを指定するとファイル名のみが出力される() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();
        var full = Path.Combine("C:", "dir", "FooBar.cs");

        // Act
        sut.Error( "log message", null, file: full );

        // Assert
        using var jd = LastJson(logPath);
        var root = jd.RootElement;
        var ok = root.TryGetProperty( "file", out var fileProp );
        Assert.True( ok, "file が欠落しています" );
        Assert.Equal( "FooBar.cs", fileProp.GetString() );
    }

    [Fact]
    public void Errorはlineを指定しないときlineが数値型の0で出力される() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        // Act
        sut.Error( "log message" );

        // Assert
        using var jd = LastJson(logPath);
        var root = jd.RootElement;
        var ok = root.TryGetProperty( "line", out var lineProp );
        Assert.True( ok, "line が欠落しています" );
        Assert.Equal( 0, lineProp.GetInt32() );
    }

    [Fact]
    public void Errorはlineを指定すると数値型でその値で出力される() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        // Act
        sut.Error( "log message", null, line: 123 );

        // Assert
        using var jd = LastJson(logPath);
        var root = jd.RootElement;
        var ok = root.TryGetProperty( "line", out var lineProp );
        Assert.True( ok, "line が欠落しています" );
        Assert.Equal( 123, lineProp.GetInt32() );
    }

    #endregion

    #region Fatal

    [Fact]
    public void Fatalはloggerが呼び出し元の型名になる() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        // Act
        sut.Fatal( "log message" );

        // Assert
        using var jd = LastJson(logPath);
        var root = jd.RootElement;
        var ok = root.TryGetProperty( "logger", out var loggerProp );
        Assert.True( ok, "logger が欠落しています" );
        Assert.Equal( typeof( LoggingServiceTests ).FullName, loggerProp.GetString() );
    }

    #endregion

    #region 耐性

    [Fact]
    public void 多数連続書き込みを行うとき件数は欠落しない() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();
        const int n = 50;

        // Act
        for(var i = 0; i < n; i++) sut.Info( $"b{i}" );

        // Assert
        var count = ReadLines(logPath).Length;
        Assert.Equal( n, count );
    }

    [Fact]
    public void 無効レベルの呼び出しはファイルを生成しない() {
        // Arrange
        var logPath = Path.Combine(_tempDir, "app.log");
        Configure( _tempDir, LogLevel.Error );
        var sut = new LoggingService();

        // Act
        sut.Warn( "nope" );

        // Assert
        Assert.False( File.Exists( logPath ) );
    }

    #endregion
}