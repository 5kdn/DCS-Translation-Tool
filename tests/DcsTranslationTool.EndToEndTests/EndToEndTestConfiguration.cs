namespace DcsTranslationTool.EndToEndTests;

/// <summary>
/// 更新E2Eテストの生成済みアセットと診断出力先を表す。
/// </summary>
internal sealed record EndToEndTestConfiguration(
    string RuntimeIdentifier,
    string ArtifactsDirectory,
    string DiagnosticsDirectory,
    string BasePackagePath,
    string IntermediateAssetsDirectory,
    string TargetAssetsDirectory
) {
    private const string RuntimeIdentifierEnvironmentVariable = "DCS_TRANSLATION_TOOL_E2E_RID";
    private const string ArtifactsEnvironmentVariable = "DCS_TRANSLATION_TOOL_E2E_ARTIFACTS";
    private const string DiagnosticsEnvironmentVariable = "DCS_TRANSLATION_TOOL_E2E_DIAGNOSTICS";

    /// <summary>
    /// 環境変数からE2Eテスト構成を生成する。
    /// </summary>
    /// <returns>検証済みのE2Eテスト構成を返す。</returns>
    /// <exception cref="InvalidOperationException">必須値または生成済みアセットがない場合に発生する。</exception>
    public static EndToEndTestConfiguration Load() {
        var runtimeIdentifier = GetRequiredEnvironmentVariable( RuntimeIdentifierEnvironmentVariable );
        if(runtimeIdentifier is not ("win-x64" or "win-x86")) {
            throw new InvalidOperationException( $"Unsupported E2E RID: {runtimeIdentifier}" );
        }

        var artifactsDirectory = Path.GetFullPath( GetRequiredEnvironmentVariable( ArtifactsEnvironmentVariable ) );
        var diagnosticsDirectory = Path.GetFullPath( GetRequiredEnvironmentVariable( DiagnosticsEnvironmentVariable ) );
        var basePackagePath = Path.Combine(
            artifactsDirectory,
            "base-assets",
            $"DCS-Translation-Tool-1.0.0-{runtimeIdentifier}.zip" );
        var intermediateAssetsDirectory = Path.Combine( artifactsDirectory, "intermediate-assets" );
        var targetAssetsDirectory = Path.Combine( artifactsDirectory, "target-assets" );
        if(!File.Exists( basePackagePath ) ||
            !Directory.Exists( intermediateAssetsDirectory ) ||
            !Directory.Exists( targetAssetsDirectory )) {
            throw new InvalidOperationException( "E2E update assets have not been generated." );
        }

        Directory.CreateDirectory( diagnosticsDirectory );
        return new EndToEndTestConfiguration(
            runtimeIdentifier,
            artifactsDirectory,
            diagnosticsDirectory,
            basePackagePath,
            intermediateAssetsDirectory,
            targetAssetsDirectory );
    }

    /// <summary>
    /// 必須環境変数を取得する。
    /// </summary>
    /// <param name="name">環境変数名。</param>
    /// <returns>空白ではない環境変数値を返す。</returns>
    /// <exception cref="InvalidOperationException">環境変数が設定されていない場合に発生する。</exception>
    private static string GetRequiredEnvironmentVariable( string name ) {
        var value = Environment.GetEnvironmentVariable( name );
        return !string.IsNullOrWhiteSpace( value )
            ? value
            : throw new InvalidOperationException( $"{name} is required." );
    }
}