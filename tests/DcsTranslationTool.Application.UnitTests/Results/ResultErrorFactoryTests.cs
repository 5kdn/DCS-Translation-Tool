using DcsTranslationTool.Application.Results;

using FluentResults;

namespace DcsTranslationTool.Application.UnitTests.Results;

/// <summary>
/// <see cref="ResultErrorFactory"/> の振る舞いを検証する。
/// </summary>
public sealed class ResultErrorFactoryTests {
    /// <summary>
    /// Validation が Validation 分類の <see cref="ResultError"/> を返すことを確認する。
    /// </summary>
    [Fact]
    public void Validation_ShouldReturnResultErrorWithValidationKindMetadata() {
        var error = AssertResultError( ResultErrorFactory.Validation( "validation error" ) );

        Assert.Equal( ResultErrorKind.Validation, error.Kind );
        Assert.Equal( nameof( ResultErrorKind.Validation ), error.Metadata["kind"] );
        Assert.Equal( "validation error", error.Message );
    }

    /// <summary>
    /// NotFound が NotFound 分類の <see cref="ResultError"/> を返すことを確認する。
    /// </summary>
    [Fact]
    public void NotFound_ShouldReturnResultErrorWithNotFoundKindMetadata() {
        var error = AssertResultError( ResultErrorFactory.NotFound( "not found" ) );

        Assert.Equal( ResultErrorKind.NotFound, error.Kind );
        Assert.Equal( nameof( ResultErrorKind.NotFound ), error.Metadata["kind"] );
        Assert.Equal( "not found", error.Message );
    }

    /// <summary>
    /// Conflict が Conflict 分類の <see cref="ResultError"/> を返すことを確認する。
    /// </summary>
    [Fact]
    public void Conflict_ShouldReturnResultErrorWithConflictKindMetadata() {
        var error = AssertResultError( ResultErrorFactory.Conflict( "conflict" ) );

        Assert.Equal( ResultErrorKind.Conflict, error.Kind );
        Assert.Equal( nameof( ResultErrorKind.Conflict ), error.Metadata["kind"] );
        Assert.Equal( "conflict", error.Message );
    }

    /// <summary>
    /// External が External 分類の <see cref="ResultError"/> を返すことを確認する。
    /// </summary>
    [Fact]
    public void External_ShouldReturnResultErrorWithExternalKindMetadata() {
        var error = AssertResultError( ResultErrorFactory.External( "external error" ) );

        Assert.Equal( ResultErrorKind.External, error.Kind );
        Assert.Equal( nameof( ResultErrorKind.External ), error.Metadata["kind"] );
        Assert.Equal( "external error", error.Message );
    }

    /// <summary>
    /// code を指定した場合に code メタデータが設定されることを確認する。
    /// </summary>
    [Fact]
    public void Validation_ShouldSetCodeMetadata_WhenCodeIsSpecified() {
        var error = AssertResultError( ResultErrorFactory.Validation( "validation error", "TEST_CODE" ) );

        Assert.Equal( "TEST_CODE", error.Metadata["code"] );
    }

    /// <summary>
    /// code が null、空文字、空白のみの場合に code メタデータが設定されないことを確認する。
    /// </summary>
    /// <param name="code">検証する code。</param>
    [Theory]
    [InlineData( null )]
    [InlineData( "" )]
    [InlineData( " " )]
    [InlineData( "   " )]
    public void Validation_ShouldNotSetCodeMetadata_WhenCodeIsNullOrWhiteSpace( string? code ) {
        var error = AssertResultError( ResultErrorFactory.Validation( "validation error", code ) );

        Assert.False( error.Metadata.ContainsKey( "code" ) );
    }

    /// <summary>
    /// exception を指定した場合に原因例外が関連付けられることを確認する。
    /// </summary>
    [Fact]
    public void Validation_ShouldAttachExceptionalReason_WhenExceptionIsSpecified() {
        var exception = new InvalidOperationException( "boom" );

        var error = AssertResultError( ResultErrorFactory.Validation( "validation error", exception: exception ) );

        var exceptionalError = Assert.IsType<ExceptionalError>( Assert.Single( error.Reasons ) );
        Assert.Same( exception, exceptionalError.Exception );
    }

    /// <summary>
    /// Unexpected が Unexpected 分類の <see cref="ExceptionalError"/> を返すことを確認する。
    /// </summary>
    [Fact]
    public void Unexpected_ShouldReturnExceptionalErrorWithUnexpectedKindMetadata() {
        var exception = new InvalidOperationException( "boom" );

        var error = Assert.IsType<ExceptionalError>( ResultErrorFactory.Unexpected( exception ) );

        Assert.Equal( nameof( ResultErrorKind.Unexpected ), error.Metadata["kind"] );
        Assert.Equal( exception.Message, error.Message );
        Assert.Same( exception, error.Exception );
    }

    /// <summary>
    /// Unexpected で code を指定した場合に code メタデータが設定されることを確認する。
    /// </summary>
    [Fact]
    public void Unexpected_ShouldSetCodeMetadata_WhenCodeIsSpecified() {
        var error = Assert.IsType<ExceptionalError>(
            ResultErrorFactory.Unexpected( new InvalidOperationException( "boom" ), "TEST_CODE" ) );

        Assert.Equal( "TEST_CODE", error.Metadata["code"] );
    }

    /// <summary>
    /// Unexpected で code を指定しない場合に code メタデータが設定されないことを確認する。
    /// </summary>
    [Theory]
    [InlineData( null )]
    [InlineData( "" )]
    [InlineData( " " )]
    [InlineData( "   " )]
    public void Unexpected_ShouldNotSetCodeMetadata_WhenCodeIsNullOrWhiteSpace( string? code ) {
        var error = Assert.IsType<ExceptionalError>(
            ResultErrorFactory.Unexpected( new InvalidOperationException( "boom" ), code ) );

        Assert.False( error.Metadata.ContainsKey( "code" ) );
    }

    /// <summary>
    /// IError が <see cref="ResultError"/> であることを確認して返す。
    /// </summary>
    /// <param name="error">検証対象のエラー。</param>
    /// <returns><see cref="ResultError"/>。</returns>
    private static ResultError AssertResultError( IError error ) => Assert.IsType<ResultError>( error );
}