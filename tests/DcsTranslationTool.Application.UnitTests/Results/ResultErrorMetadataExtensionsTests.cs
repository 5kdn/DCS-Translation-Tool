using DcsTranslationTool.Application.Results;

using FluentResults;

namespace DcsTranslationTool.Application.UnitTests.Results;

/// <summary>
/// <see cref="ResultErrorMetadataExtensions"/> の振る舞いを検証する。
/// </summary>
public sealed class ResultErrorMetadataExtensionsTests {
    /// <summary>
    /// kind メタデータから正しい分類を取得できることを確認する。
    /// </summary>
    [Fact]
    public void GetErrorKind_ShouldReturnKind_WhenMetadataContainsValidKind() {
        var error = new Error( "error" );
        error.Metadata["kind"] = nameof( ResultErrorKind.External );

        var kind = error.GetErrorKind();

        Assert.Equal( ResultErrorKind.External, kind );
    }

    /// <summary>
    /// kind メタデータの大文字小文字を無視して分類を取得できることを確認する。
    /// </summary>
    [Fact]
    public void GetErrorKind_ShouldIgnoreCase_WhenMetadataContainsKindText() {
        var error = new Error( "error" );
        error.Metadata["kind"] = "uNexPected";

        var kind = error.GetErrorKind();

        Assert.Equal( ResultErrorKind.Unexpected, kind );
    }

    /// <summary>
    /// kind メタデータが存在しない場合に null を返すことを確認する。
    /// </summary>
    [Fact]
    public void GetErrorKind_ShouldReturnNull_WhenKindMetadataDoesNotExist() {
        var error = new Error( "error" );

        var kind = error.GetErrorKind();

        Assert.Null( kind );
    }

    /// <summary>
    /// kind メタデータが null の場合に null を返すことを確認する。
    /// </summary>
    [Fact]
    public void GetErrorKind_ShouldReturnNull_WhenKindMetadataIsNull() {
        var error = new Error( "error" );
        error.Metadata["kind"] = null!;

        var kind = error.GetErrorKind();

        Assert.Null( kind );
    }

    /// <summary>
    /// kind メタデータが不正文字列の場合に null を返すことを確認する。
    /// </summary>
    [Fact]
    public void GetErrorKind_ShouldReturnNull_WhenKindMetadataIsInvalid() {
        var error = new Error( "error" );
        error.Metadata["kind"] = "invalid-kind";

        var kind = error.GetErrorKind();

        Assert.Null( kind );
    }

    /// <summary>
    /// null のエラーを渡した場合に <see cref="ArgumentNullException"/> を送出することを確認する。
    /// </summary>
    [Fact]
    public void GetErrorKind_ShouldThrowArgumentNullException_WhenErrorIsNull() {
        IError? error = null;

        Assert.Throws<ArgumentNullException>( () => error!.GetErrorKind() );
    }

    /// <summary>
    /// 先頭エラーの分類を返すことを確認する。
    /// </summary>
    [Fact]
    public void GetFirstErrorKind_ShouldReturnFirstErrorKind_WhenFirstErrorHasKindMetadata() {
        var result = Result.Fail( new Error( "first" ).WithMetadata( "kind", nameof( ResultErrorKind.NotFound ) ) );
        result.Reasons.Add( new Error( "second" ).WithMetadata( "kind", nameof( ResultErrorKind.External ) ) );

        var kind = result.GetFirstErrorKind();

        Assert.Equal( ResultErrorKind.NotFound, kind );
    }

    /// <summary>
    /// エラーが存在しない場合に null を返すことを確認する。
    /// </summary>
    [Fact]
    public void GetFirstErrorKind_ShouldReturnNull_WhenResultHasNoErrors() {
        var result = Result.Ok();

        var kind = result.GetFirstErrorKind();

        Assert.Null( kind );
    }

    /// <summary>
    /// 先頭エラーに kind がない場合は後続エラーを見ずに null を返すことを確認する。
    /// </summary>
    [Fact]
    public void GetFirstErrorKind_ShouldReturnNull_WhenFirstErrorDoesNotHaveKindMetadata() {
        var result = Result.Fail( new Error( "first" ) );
        result.Reasons.Add( new Error( "second" ).WithMetadata( "kind", nameof( ResultErrorKind.External ) ) );

        var kind = result.GetFirstErrorKind();

        Assert.Null( kind );
    }

    /// <summary>
    /// null の Result を渡した場合に <see cref="ArgumentNullException"/> を送出することを確認する。
    /// </summary>
    [Fact]
    public void GetFirstErrorKind_ShouldThrowArgumentNullException_WhenResultIsNull() {
        Result? result = null;

        Assert.Throws<ArgumentNullException>( () => result!.GetFirstErrorKind() );
    }
}