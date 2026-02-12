using System.IO.Compression;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Interfaces;

using FluentResults;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// zip ファイル操作を提供するサービス。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public class ZipService( ILoggingService logger ) : IZipService {
    /// <inheritdoc />
    public Result<IReadOnlyList<string>> GetEntries( string zipFilePath ) {
        if(string.IsNullOrWhiteSpace( zipFilePath )) {
            logger.Warn( "zip ファイルのエントリ取得でパスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( $"zip ファイルパスが null または空です: {zipFilePath}", "ZIP_PATH_REQUIRED" ) );
        }
        if(!File.Exists( zipFilePath )) {
            logger.Warn( $"zip ファイルが存在しない。Path={zipFilePath}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {zipFilePath}", "ZIP_NOT_FOUND" ) );
        }
        try {
            logger.Debug( $"zip ファイルのエントリ一覧を取得する。Path={zipFilePath}" );
            using FileStream fs = new(zipFilePath, FileMode.Open, FileAccess.Read);
            using ZipArchive archive = new(fs, ZipArchiveMode.Read);
            List<string> entries = [.. archive.Entries.Select( e => e.FullName )];
            logger.Info( $"zip ファイルのエントリ一覧取得が完了した。Count={entries.Count}" );
            return Result.Ok<IReadOnlyList<string>>( entries );
        }
        catch(Exception ex) {
            logger.Error( $"zip ファイルのエントリ取得に失敗した。Path={zipFilePath}", ex );
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "ZIP_GET_ENTRIES_EXCEPTION" ) );
        }
    }

    /// <inheritdoc />
    public Result AddEntry( string zipFilePath, string entryPath, string filePath ) {
        if(string.IsNullOrWhiteSpace( zipFilePath )) {
            logger.Warn( "zip へのファイル追加で zip パスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( $"zip ファイルパスが null または空です: {zipFilePath}", "ZIP_PATH_REQUIRED" ) );
        }
        if(!File.Exists( zipFilePath )) {
            logger.Warn( $"zip ファイルが存在しない。Path={zipFilePath}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {zipFilePath}", "ZIP_NOT_FOUND" ) );
        }
        if(string.IsNullOrWhiteSpace( filePath )) {
            logger.Warn( "zip へのファイル追加で元ファイルパスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( $"追加するファイルパスが null または空です: {filePath}", "ZIP_SOURCE_PATH_REQUIRED" ) );
        }
        if(!File.Exists( filePath )) {
            logger.Warn( $"追加元ファイルが存在しない。Path={filePath}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {filePath}", "ZIP_SOURCE_NOT_FOUND" ) );
        }
        if(string.IsNullOrWhiteSpace( entryPath )) {
            logger.Warn( "zip へのファイル追加でエントリ名が空だった。" );
            return Result.Fail( ResultErrorFactory.Validation( $"値が null または空です: {nameof( entryPath )}", "ZIP_ENTRY_REQUIRED" ) );
        }

        try {
            logger.Debug( $"zip ファイルにエントリを追加する。Zip={zipFilePath}, Entry={entryPath}, Source={filePath}" );
            using FileStream fs = new(zipFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            using ZipArchive archive = new(fs, ZipArchiveMode.Update);
            archive.GetEntry( entryPath )?.Delete();
            archive.CreateEntryFromFile( filePath, entryPath, CompressionLevel.Fastest );
            logger.Info( $"zip ファイルにエントリを追加した。Zip={zipFilePath}, Entry={entryPath}" );
            return Result.Ok();
        }
        catch(InvalidDataException ex) {
            logger.Error( $"zip ファイルの構造が不正でエントリ追加に失敗した。Zip={zipFilePath}", ex );
            return Result.Fail( ResultErrorFactory.External( "zip ファイルが壊れている可能性があります", "ZIP_INVALID_DATA", ex ) );
        }
        catch(IOException ex) {
            logger.Error( $"zip ファイルへの書き込みに失敗した。Zip={zipFilePath}", ex );
            return Result.Fail( ResultErrorFactory.External( "zip ファイル書き込み中に入出力エラーが発生しました", "ZIP_IO_ERROR", ex ) );
        }
    }

    /// <inheritdoc />
    public Result AddEntry( string zipFilePath, string entryPath, byte[] data ) {
        if(string.IsNullOrWhiteSpace( zipFilePath )) {
            logger.Warn( "zip へのバイナリ追加で zip パスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( "zip ファイルパスが null または空です", "ZIP_PATH_REQUIRED" ) );
        }
        if(!File.Exists( zipFilePath )) {
            logger.Warn( $"zip ファイルが存在しない。Path={zipFilePath}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {zipFilePath}", "ZIP_NOT_FOUND" ) );
        }
        if(string.IsNullOrWhiteSpace( entryPath )) {
            logger.Warn( "zip へのバイナリ追加でエントリ名が空だった。" );
            return Result.Fail( ResultErrorFactory.Validation( "エントリーが null または空です", "ZIP_ENTRY_REQUIRED" ) );
        }
        if(data == null || data.Length == 0) {
            logger.Warn( "zip へのバイナリ追加でデータが空だった。" );
            return Result.Fail( ResultErrorFactory.Validation( "追加するデータが null または空です", "ZIP_DATA_REQUIRED" ) );
        }

        try {
            logger.Debug( $"zip ファイルにバイナリエントリを追加する。Zip={zipFilePath}, Entry={entryPath}, Length={data.Length}" );
            using FileStream fs = new(zipFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            using ZipArchive archive = new(fs, ZipArchiveMode.Update);
            archive.GetEntry( entryPath )?.Delete();
            ZipArchiveEntry entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
            using Stream entryStream = entry.Open();
            entryStream.Write( data );
            logger.Info( $"zip ファイルにバイナリエントリを追加した。Zip={zipFilePath}, Entry={entryPath}" );
            return Result.Ok();
        }
        catch(InvalidDataException ex) {
            logger.Error( $"zip ファイルの構造が不正でバイナリ追加に失敗した。Zip={zipFilePath}", ex );
            return Result.Fail( ResultErrorFactory.External( $"zip ファイルが壊れている可能性があります: {ex.Message}", "ZIP_INVALID_DATA", ex ) );
        }
        catch(IOException ex) {
            logger.Error( $"zip ファイルへのバイナリ書き込みに失敗した。Zip={zipFilePath}", ex );
            return Result.Fail( ResultErrorFactory.External( $"zip ファイル書き込み中に入出力エラーが発生しました: {ex.Message}", "ZIP_IO_ERROR", ex ) );
        }
    }

    /// <inheritdoc />
    public Result DeleteEntry( string zipFilePath, string entryPath ) {
        if(string.IsNullOrWhiteSpace( zipFilePath )) {
            logger.Warn( "zip エントリ削除で zip パスが空だった。" );
            return Result.Fail( ResultErrorFactory.Validation( "zip ファイルパスが空です", "ZIP_PATH_REQUIRED" ) );
        }
        if(!File.Exists( zipFilePath )) {
            logger.Warn( $"zip ファイルが存在しない。Path={zipFilePath}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {zipFilePath}", "ZIP_NOT_FOUND" ) );
        }
        if(string.IsNullOrWhiteSpace( entryPath )) {
            logger.Warn( "zip エントリ削除でエントリ名が空だった。" );
            return Result.Fail( ResultErrorFactory.Validation( "zip ファイルパスが空です", "ZIP_ENTRY_REQUIRED" ) );
        }

        try {
            logger.Debug( $"zip ファイルからエントリを削除する。Zip={zipFilePath}, Entry={entryPath}" );
            using FileStream stream = new(zipFilePath, FileMode.Open, FileAccess.ReadWrite);
            using ZipArchive archive = new(stream, ZipArchiveMode.Update);
            List<ZipArchiveEntry> targets = [.. archive.Entries.Where(e => e.FullName == entryPath || e.FullName.StartsWith(entryPath.TrimEnd('/') + '/'))];
            targets.ForEach( entry => entry.Delete() );
            logger.Info( $"zip ファイルからエントリを削除した。Zip={zipFilePath}, Deleted={targets.Count}" );
            return Result.Ok();
        }
        catch(InvalidDataException ex) {
            logger.Error( $"zip ファイルの構造が不正でエントリ削除に失敗した。Zip={zipFilePath}", ex );
            return Result.Fail( ResultErrorFactory.External( $"zip ファイル書き込み中に入出力エラーが発生した: {ex.Message}", "ZIP_INVALID_DATA", ex ) );
        }
        catch(IOException ex) {
            logger.Error( $"zip ファイルのエントリ削除中に I/O 例外が発生した。Zip={zipFilePath}", ex );
            return Result.Fail( ResultErrorFactory.External( $"zip ファイル書き込み中に入出力エラーが発生した: {ex.Message}", "ZIP_IO_ERROR", ex ) );
        }
    }
}