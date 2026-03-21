namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services;

/// <summary>
/// <see cref="LoggingServiceTests"/> を非並列実行するコレクションを表す。
/// </summary>
[CollectionDefinition( nameof( LoggingServiceNonParallelCollection ), DisableParallelization = true )]
public sealed class LoggingServiceNonParallelCollection;