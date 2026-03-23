using System.Diagnostics;
using System.Text;
using System.Text.Json;

using DcsTranslationTool.Infrastructure.Services;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services;

[Collection( nameof( LoggingServiceNonParallelCollection ) )]
public sealed class LoggingServiceTests : IDisposable {
    private readonly string _tempDir = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString() );

    public LoggingServiceTests() {
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        try { LogManager.Shutdown(); }
        catch { }

        if(Directory.Exists( _tempDir )) {
            try { Directory.Delete( _tempDir, true ); }
            catch { }
        }

        GC.SuppressFinalize( this );
    }

    [Fact]
    public void BuildConfigurationはディレクトリが存在しないとき作成する() {
        var targetDir = Path.Combine( _tempDir, "sub" );

        _ = LoggingService.BuildConfiguration( new LoggingOptions { LogDirectory = targetDir } );

        Assert.True( Directory.Exists( targetDir ) );
    }

    [Theory]
    [InlineData( false, false )]
    [InlineData( true, true )]
    public void BuildConfigurationはデバッグ出力設定に応じてデバッグターゲット有無が変わる( bool enableDebug, bool expectedHasDebug ) {
        var options = new LoggingOptions
        {
            LogDirectory = Path.Combine( _tempDir, enableDebug ? "cfg-enable" : "cfg-disable" ),
            EnableDebugOutput = enableDebug
        };

        var config = LoggingService.BuildConfiguration( options );

        var hasDebug = config.LoggingRules.Any( rule => rule.Targets.Any( target => target is AsyncTargetWrapper wrapper && wrapper.WrappedTarget is DebuggerTarget ) );
        Assert.Equal( expectedHasDebug, hasDebug );
    }

    [Theory]
    [InlineData( "Trace", "Trace", 1 )]
    [InlineData( "Info", "Trace", 0 )]
    [InlineData( "Debug", "Debug", 1 )]
    [InlineData( "Info", "Debug", 0 )]
    public void TraceとDebugは最小レベル設定に応じて書き出し件数が変化する( string minLevel, string method, int expectedDelta ) {
        var logPath = Path.Combine( _tempDir, "app.log" );
        Configure( _tempDir, LogLevel.FromString( minLevel ), enableDebug: false );
        var sut = new LoggingService();
        var before = ReadLines( logPath ).Length;

        if(method == "Trace") {
            sut.Trace( "log message" );
        }
        else {
            sut.Debug( "log message" );
        }

        var after = ReadLines( logPath ).Length;
        Assert.Equal( expectedDelta, after - before );
    }

    [Fact]
    public void WarnとErrorは主要メタデータをJSONへ出力する() {
        var logPath = Path.Combine( _tempDir, "app.log" );
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        sut.Warn( "warn message", null, member: "DoSomething" );
        using var warnJson = LastJson( logPath );
        Assert.Equal( "warn message", warnJson.RootElement.GetProperty( "message" ).GetString() );
        Assert.Equal( "DoSomething", warnJson.RootElement.GetProperty( "member" ).GetString() );

        var exception = new InvalidOperationException( "Boom" );
        sut.Error( "error message", exception, file: Path.Combine( _tempDir, "nested", "FooBar.cs" ), line: 123 );
        using var errorJson = LastJson( logPath );
        Assert.Equal( "error message", errorJson.RootElement.GetProperty( "message" ).GetString() );
        Assert.Equal( "FooBar.cs", errorJson.RootElement.GetProperty( "file" ).GetString() );
        Assert.Equal( 123, errorJson.RootElement.GetProperty( "line" ).GetInt32() );
        Assert.Contains( "InvalidOperationException", errorJson.RootElement.GetProperty( "exception" ).GetString() );
    }

    [Fact]
    public void Fatalはloggerが呼び出し元の型名になる() {
        var logPath = Path.Combine( _tempDir, "app.log" );
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();

        sut.Fatal( "log message" );

        using var json = LastJson( logPath );
        Assert.Equal( typeof( LoggingServiceTests ).FullName, json.RootElement.GetProperty( "logger" ).GetString() );
    }

    [Fact]
    public void 多数連続書き込みを行うとき件数は欠落しない() {
        var logPath = Path.Combine( _tempDir, "app.log" );
        Configure( _tempDir, LogLevel.Trace );
        var sut = new LoggingService();
        const int count = 50;

        for(var index = 0; index < count; index++) {
            sut.Info( $"b{index}" );
        }

        Assert.Equal( count, ReadLines( logPath ).Length );
    }

    [Fact]
    public void 無効レベルの呼び出しはファイルを生成しない() {
        var logPath = Path.Combine( _tempDir, "app.log" );
        Configure( _tempDir, LogLevel.Error );
        var sut = new LoggingService();

        sut.Warn( "nope" );

        Assert.False( File.Exists( logPath ) );
    }

    private static LoggingConfiguration Configure( string dir, LogLevel min, bool enableDebug = true ) {
        var options = new LoggingOptions
        {
            LogDirectory = dir,
            FileName = "app.log",
            MinLevel = min,
            EnableDebugOutput = enableDebug
        };
        var configuration = LoggingService.BuildConfiguration( options );
        LogManager.Configuration = configuration;
        return configuration;
    }

    private static void Flush() => LogManager.Flush( TimeSpan.FromSeconds( 5 ) );

    private static string[] ReadLines( string logPath ) {
        Flush();
        if(!File.Exists( logPath )) {
            return [];
        }

        using var stream = new FileStream( logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
        using var reader = new StreamReader( stream, Encoding.UTF8 );
        var content = reader.ReadToEnd();
        return content.Length == 0 ? [] : content.Split( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries );
    }

    private static JsonDocument LastJson( string logPath ) {
        var stopwatch = Stopwatch.StartNew();
        while(stopwatch.Elapsed < TimeSpan.FromSeconds( 5 )) {
            var lines = ReadLines( logPath );
            if(lines.Length > 0) {
                return JsonDocument.Parse( lines[^1] );
            }

            Thread.Sleep( 50 );
        }

        throw new InvalidOperationException( "ログ行が出力されていません。" );
    }
}