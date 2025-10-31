using System.Reflection;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using NLog.Config;

using NLogManager = NLog.LogManager;

namespace DcsTranslationTool.Composition.Tests;

public class CompositionRegistrationTests {
    [Fact]
    public void Registerを呼び出すとインフラサービスが登録される() {
        // Arrange
        using var context = CreateContext();
        var container = context.Container;

        // Act
        var loggingService = container.GetInstance( typeof( ILoggingService ), null );
        var appSettingsService = container.GetInstance( typeof( IAppSettingsService ), null );
        var apiService1 = container.GetInstance( typeof( IApiService ), null );
        var apiService2 = container.GetInstance( typeof( IApiService ), null );
        var zipService1 = container.GetInstance( typeof( IZipService ), null );
        var zipService2 = container.GetInstance( typeof( IZipService ), null );

        // Assert
        Assert.NotNull( loggingService );
        Assert.IsType<LoggingService>( loggingService );
        context.TrackedAppSettings = Assert.IsType<AppSettingsService>( appSettingsService );
        Assert.Same( apiService1, apiService2 );
        Assert.IsType<ApiService>( apiService1 );
        Assert.Same( zipService1, zipService2 );
        Assert.NotNull( NLogManager.Configuration );
    }

    [Fact]
    public void Registerを呼び出すとAppData配下に設定ファイルが作成される() {
        // Arrange
        using var context = CreateContext();
        var container = context.Container;
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly() ?? typeof( CompositionRegistration ).Assembly;
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "DcsTranslationTool";
        var appData = Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData );
        var expectedDirectory = Path.Combine( appData, title );
        var expectedFilePath = Path.Combine( expectedDirectory, "appsettings.json" );

        // Act
        var appSettingsService = container.GetInstance( typeof( IAppSettingsService ), null );
        var appSettingsImplementation = appSettingsService as AppSettingsService;
        var field = typeof( AppSettingsService ).GetField( "_filePath", BindingFlags.NonPublic | BindingFlags.Instance );
        var actualFilePath = field?.GetValue( appSettingsImplementation );
        context.DirectoryToDelete = expectedDirectory;

        // Assert
        context.TrackedAppSettings = Assert.IsType<AppSettingsService>( appSettingsService );
        Assert.NotNull( field );
        Assert.IsType<string>( actualFilePath );
        Assert.Equal( expectedFilePath, actualFilePath );
        Assert.True( Directory.Exists( expectedDirectory ) );
    }

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

    private sealed class RegistrationTestContext(
        SimpleContainer container,
        LoggingConfiguration? originalConfiguration
    ) : IDisposable {
        internal SimpleContainer Container { get; } = container;
        internal LoggingConfiguration? OriginalConfiguration { get; } = originalConfiguration;
        internal AppSettingsService? TrackedAppSettings { get; set; }
        internal string? DirectoryToDelete { get; set; }

        public void Dispose() {
            TrackedAppSettings?.Dispose();
            NLogManager.Configuration = OriginalConfiguration;
            if(DirectoryToDelete is { Length: > 0 } directory && Directory.Exists( directory )) {
                try {
                    if(!Directory.EnumerateFileSystemEntries( directory ).Any()) {
                        Directory.Delete( directory );
                    }
                }
                catch {
                    // 意図的に握りつぶす
                }
            }
        }
    }
}