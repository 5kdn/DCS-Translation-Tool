using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.TestCommon.IO;

using Moq;

namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services;

/// <summary>
/// <see cref="AppSettingsService"/> の永続化挙動を検証する。
/// </summary>
public sealed class AppSettingsServiceTests : IDisposable {
    private readonly Mock<ILoggingService> _logger = new();
    private readonly TemporaryDirectory _temporaryDirectory = new( nameof( AppSettingsServiceTests ) );
    private readonly string _settingsFileName = "appsettings.json";

    /// <summary>
    /// テストで使用したリソースを破棄する。
    /// </summary>
    public void Dispose() {
        _temporaryDirectory.Dispose();
        GC.SuppressFinalize( this );
    }

    /// <summary>
    /// 設定ファイルが存在しない場合に既定値で初期化することを検証する。
    /// </summary>
    [Fact]
    public void Constructorでファイルが存在しないと既定値で初期化する() {
        using var sut = CreateService();

        Assert.NotNull( sut.Settings );
        Assert.False( File.Exists( GetSettingsPath() ) );
    }

    /// <summary>
    /// 本体が破損していてもバックアップから復旧することを検証する。
    /// </summary>
    [Fact]
    public void Constructorで本体が破損しバックアップが有効ならバックアップを読み込む() {
        File.WriteAllText( GetSettingsPath(), "<<<invalid json>>>" );
        File.WriteAllText( GetBackupPath(), "{}" );

        using var sut = CreateService();

        Assert.NotNull( sut.Settings );
    }

    /// <summary>
    /// 本体もバックアップも破損している場合に既定値で初期化することを検証する。
    /// </summary>
    [Fact]
    public void Constructorで本体とバックアップが破損していると既定値で初期化する() {
        File.WriteAllText( GetSettingsPath(), "<<<invalid json>>>" );
        File.WriteAllText( GetBackupPath(), "<<<invalid json>>>" );

        using var sut = CreateService();

        Assert.NotNull( sut.Settings );
    }

    /// <summary>
    /// 初回保存時に設定ファイルとバックアップを作成することを検証する。
    /// </summary>
    [Fact]
    public async Task SaveAsyncを初回に実行するとファイルとバックアップを作成する() {
        await using var sut = CreateService();

        await sut.SaveAsync( TestContext.Current.CancellationToken );

        Assert.True( File.Exists( GetSettingsPath() ) );
        Assert.True( File.Exists( GetBackupPath() ) );
        Assert.True( new FileInfo( GetSettingsPath() ).Length > 0 );
    }

    /// <summary>
    /// 保存先ディレクトリが存在しなくても自動作成することを検証する。
    /// </summary>
    [Fact]
    public async Task SaveAsyncで保存先が存在しないとディレクトリを自動作成する() {
        var root = Path.Combine( _temporaryDirectory.Path, "NonExistent", "Directory" );
        var path = Path.Combine( root, _settingsFileName );
        await using var sut = CreateService( root );

        await sut.SaveAsync( TestContext.Current.CancellationToken );

        Assert.True( Directory.Exists( root ) );
        Assert.True( File.Exists( path ) );
    }

    /// <summary>
    /// 同時保存時も競合せず完了することを検証する。
    /// </summary>
    [Fact]
    public async Task SaveAsyncを同時に呼び出しても競合せずに完了する() {
        await using var sut = CreateService();

        var tasks = Enumerable.Range( 0, 8 ).Select( _ => sut.SaveAsync() ).ToArray();
        await Task.WhenAll( tasks );

        Assert.True( File.Exists( GetSettingsPath() ) );
        Assert.True( new FileInfo( GetSettingsPath() ).Length > 0 );
    }

    /// <summary>
    /// 代表的な設定値を保存して再読込できることを検証する。
    /// </summary>
    [Fact]
    public async Task SaveAsyncは代表的な設定値を保存して再読込できる() {
        await using(var sut = CreateService()) {
            sut.Settings.TranslationCreationDictionaryPaneRatio = 3.5;
            sut.Settings.TranslationCreationWindowWidth = 1440;
            sut.Settings.TranslationCreationWindowHeight = 960;
            sut.Settings.TranslationCreationWrapDictionaryDetailsText = false;

            await sut.SaveAsync( TestContext.Current.CancellationToken );
        }

        await using var reloaded = CreateService();
        Assert.Equal( 3.5, reloaded.Settings.TranslationCreationDictionaryPaneRatio );
        Assert.Equal( 1440, reloaded.Settings.TranslationCreationWindowWidth );
        Assert.Equal( 960, reloaded.Settings.TranslationCreationWindowHeight );
        Assert.False( reloaded.Settings.TranslationCreationWrapDictionaryDetailsText );
    }

    /// <summary>
    /// 公開設定変更により自動保存が走ることを検証する。
    /// </summary>
    [Fact]
    public async Task Settings変更時は自動保存される() {
        await using var sut = CreateService();

        sut.Settings.ShellWidth = 777;

        await WaitUntilAsync(
            () => File.Exists( GetSettingsPath() ) && new FileInfo( GetSettingsPath() ).Length > 0,
            TestContext.Current.CancellationToken );

        await using var reloaded = CreateService();
        Assert.Equal( 777, reloaded.Settings.ShellWidth );
    }

    /// <summary>
    /// 破棄時に保留中の変更をフラッシュすることを検証する。
    /// </summary>
    [Fact]
    public async Task DisposeAsyncを呼び出すと保留中の保存をフラッシュする() {
        await using var sut = CreateService();
        Assert.False( File.Exists( GetSettingsPath() ) );

        sut.Settings.ShellHeight = 888;
        await sut.DisposeAsync();

        Assert.True( File.Exists( GetSettingsPath() ) );
        await using var reloaded = CreateService();
        Assert.Equal( 888, reloaded.Settings.ShellHeight );
    }

    /// <summary>
    /// キャンセル済みトークン時に中断することを検証する。
    /// </summary>
    [Fact]
    public async Task SaveAsyncでキャンセル要求を受けると中断する() {
        await using var sut = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>( () => sut.SaveAsync( cts.Token ) );
    }

    /// <summary>
    /// テスト対象を生成する。
    /// </summary>
    /// <param name="directory">保存先ディレクトリ。</param>
    /// <returns>生成したサービスを返す。</returns>
    private AppSettingsService CreateService( string? directory = null ) {
        return new AppSettingsService( _logger.Object, directory ?? _temporaryDirectory.Path, _settingsFileName );
    }

    /// <summary>
    /// 設定ファイルパスを返す。
    /// </summary>
    /// <returns>設定ファイルパスを返す。</returns>
    private string GetSettingsPath() => Path.Combine( _temporaryDirectory.Path, _settingsFileName );

    /// <summary>
    /// バックアップファイルパスを返す。
    /// </summary>
    /// <returns>バックアップファイルパスを返す。</returns>
    private string GetBackupPath() => GetSettingsPath() + ".bak";

    /// <summary>
    /// 条件成立まで待機する。
    /// </summary>
    /// <param name="condition">待機条件。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスクを返す。</returns>
    private static async Task WaitUntilAsync( Func<bool> condition, CancellationToken cancellationToken ) {
        var timeoutAt = DateTime.UtcNow.AddSeconds( 5 );
        while(DateTime.UtcNow < timeoutAt) {
            cancellationToken.ThrowIfCancellationRequested();
            if(condition()) {
                return;
            }

            await Task.Delay( 50, cancellationToken );
        }

        throw new TimeoutException( "条件成立を待機中にタイムアウトした。" );
    }
}