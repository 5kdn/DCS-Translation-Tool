using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Composition;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using NLog.Config;

using NLogManager = NLog.LogManager;

namespace DcsTranslationTool.IntegrationTests.Composition;

/// <summary>
/// <see cref="CompositionRegistration"/> の Infrastructure 登録を検証する。
/// </summary>
public sealed class CompositionRegistrationTests {
    /// <summary>
    /// 主要 Infrastructure サービスを解決できることを検証する。
    /// </summary>
    [Fact]
    public void Registerを呼び出すと主要Infrastructureサービスを解決できる() {
        using var context = CreateContext();

        Assert.IsType<LoggingService>( context.Get<ILoggingService>() );
        Assert.IsType<AppSettingsService>( context.Get<IAppSettingsService>() );
        Assert.IsType<TreeHttpClientProvider>( context.Get<ITreeHttpClientProvider>() );
        Assert.IsType<ApiService>( context.Get<IApiService>() );
        Assert.IsType<FileEntryHashCacheService>( context.Get<IFileEntryHashCacheService>() );
        Assert.IsType<TranslationDictionaryService>( context.Get<ITranslationDictionaryService>() );
        Assert.IsType<ZipService>( context.Get<IZipService>() );
        Assert.IsType<UpdateCheckService>( context.Get<IUpdateCheckService>() );
        Assert.NotNull( NLogManager.Configuration );
    }

    /// <summary>
    /// singleton 登録されたサービスが同一インスタンスを返すことを検証する。
    /// </summary>
    [Fact]
    public void Registerを呼び出すとsingletonサービスは同一インスタンスになる() {
        using var context = CreateContext();

        Assert.Same( context.Get<IApiService>(), context.Get<IApiService>() );
        Assert.Same( context.Get<IFileEntryHashCacheService>(), context.Get<IFileEntryHashCacheService>() );
        Assert.Same( context.Get<ITranslationDictionaryService>(), context.Get<ITranslationDictionaryService>() );
        Assert.Same( context.Get<IZipService>(), context.Get<IZipService>() );
    }

    /// <summary>
    /// テストコンテキストを生成する。
    /// </summary>
    /// <returns>生成したコンテキストを返す。</returns>
    private static RegistrationTestContext CreateContext() {
        var container = new SimpleContainer();
        var originalConfiguration = NLogManager.Configuration;

        try {
            CompositionRegistration.Register( container );
            return new RegistrationTestContext( container, originalConfiguration );
        }
        catch {
            NLogManager.Configuration = originalConfiguration;
            throw;
        }
    }

    /// <summary>
    /// 登録テスト用コンテキストを表す。
    /// </summary>
    /// <param name="container">DI コンテナ。</param>
    /// <param name="originalConfiguration">元の NLog 設定。</param>
    private sealed class RegistrationTestContext(
        SimpleContainer container,
        LoggingConfiguration? originalConfiguration
    ) : IDisposable {
        /// <summary>
        /// 指定型をコンテナから取得する。
        /// </summary>
        /// <typeparam name="T">取得型。</typeparam>
        /// <returns>取得したインスタンスを返す。</returns>
        public T Get<T>() where T : class {
            var instance = container.GetInstance( typeof( T ), null );
            Assert.NotNull( instance );
            return (T)instance;
        }

        /// <summary>
        /// 使用したリソースを破棄する。
        /// </summary>
        public void Dispose() {
            (container.GetInstance( typeof( IAppSettingsService ), null ) as IDisposable)?.Dispose();
            NLogManager.Configuration = originalConfiguration;
        }
    }
}