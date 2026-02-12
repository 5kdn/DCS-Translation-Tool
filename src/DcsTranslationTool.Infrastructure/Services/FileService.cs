using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// ファイル入出力を実装するサービス。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public class FileService( ILoggingService logger ) : IFileService {
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <inheritdoc/>
    public T? Read<T>( string folderPath, string fileName ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( folderPath );
        ArgumentException.ThrowIfNullOrWhiteSpace( fileName );

        var path = Path.Combine(folderPath, fileName);
        if(!File.Exists( path )) {
            logger.Debug( $"読み込み対象ファイルが存在しない。Path={path}" );
            return default;
        }

        logger.Debug( $"ファイルを読み込む。Path={path}" );
        var json = File.ReadAllText(path, Encoding.UTF8);
        if(string.IsNullOrWhiteSpace( json )) {
            logger.Warn( $"ファイル内容が空のため既定値を返す。Path={path}" );
            return default;
        }

        var result = JsonSerializer.Deserialize<T>( json, JsonSerializerOptions );
        logger.Info( $"ファイルの読み込みに成功した。Path={path}" );
        return result;
    }

    /// <inheritdoc/>
    public void Save<T>( string folderPath, string fileName, T content ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( folderPath );
        ArgumentException.ThrowIfNullOrWhiteSpace( fileName );

        if(!Directory.Exists( folderPath )) Directory.CreateDirectory( folderPath );

        var fileContent = JsonSerializer.Serialize( content, JsonSerializerOptions );
        var targetPath = Path.Combine( folderPath, fileName );
        logger.Debug( $"ファイルを保存する。Path={targetPath}" );
        File.WriteAllText( targetPath, fileContent, Encoding.UTF8 );
        logger.Info( $"ファイルの保存が完了した。Path={targetPath}" );
    }

    /// <inheritdoc/>
    public void Delete( string folderPath, string fileName ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( folderPath );
        ArgumentException.ThrowIfNullOrWhiteSpace( fileName );

        var targetPath = Path.Combine(folderPath, fileName);
        if(File.Exists( targetPath )) {
            File.Delete( targetPath );
            logger.Info( $"ファイルを削除した。Path={targetPath}" );
        }
        else {
            logger.Debug( $"削除対象ファイルが存在しない。Path={targetPath}" );
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync( string path, string content ) {
        if(string.IsNullOrWhiteSpace( path ))
            throw new ArgumentException( "保存先のパスが空です", nameof( path ) );

        var dirName = Path.GetDirectoryName( path );
        try {
            if(!string.IsNullOrEmpty( dirName )) Directory.CreateDirectory( dirName );
            logger.Debug( $"ファイルを非同期で保存する。Path={path}, Length={content?.Length ?? 0}" );
            await File.WriteAllTextAsync( path, content );
            logger.Info( $"ファイルの非同期保存が完了した。Path={path}" );
        }
        catch(IOException ex) {
            logger.Error( $"ファイルの非同期保存に失敗した。Path={path}", ex );
            throw new IOException( $"ファイルの保存に失敗した: {path}", ex );
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync( string path, byte[] content ) {
        if(string.IsNullOrWhiteSpace( path ))
            throw new ArgumentException( "保存先のパスが空です", nameof( path ) );
        ArgumentNullException.ThrowIfNull( content );
        var dirName = Path.GetDirectoryName( path );
        try {
            if(!string.IsNullOrEmpty( dirName )) Directory.CreateDirectory( dirName );
            logger.Debug( $"バイナリファイルを非同期で保存する。Path={path}, Length={content.Length}" );
            await File.WriteAllBytesAsync( path, content );
            logger.Info( $"バイナリファイルの非同期保存が完了した。Path={path}" );
        }
        catch(IOException ex) {
            logger.Error( $"バイナリファイルの非同期保存に失敗した。Path={path}", ex );
            throw new IOException( $"ファイルの保存に失敗した: {path}", ex );
        }
    }

}