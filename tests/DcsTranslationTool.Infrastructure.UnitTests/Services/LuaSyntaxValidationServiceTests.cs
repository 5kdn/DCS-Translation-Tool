using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.UnitTests.Services;

/// <summary>
/// <see cref="LuaSyntaxValidationService"/> の動作を検証する。
/// </summary>
public sealed class LuaSyntaxValidationServiceTests {
    /// <summary>
    /// 正常な Lua テキストを検証すると成功することを確認する。
    /// </summary>
    [Fact]
    public void Validateは正常なLuaテキストを検証すると成功する() {
        var loggerMock = new Mock<ILoggingService>();
        var sut = new LuaSyntaxValidationService( loggerMock.Object );

        var result = sut.Validate(
        [
            new LuaSyntaxValidationTarget( @"C:\work\sample.lua", "return 1" ),
            new LuaSyntaxValidationTarget( @"C:\work\dictionary", "dictionary = {}" ),
        ] );

        Assert.True( result.IsSuccess );
        Assert.Empty( result.Failures );
    }

    /// <summary>
    /// 不正な Lua テキストを検証すると失敗ファイルを返すことを確認する。
    /// </summary>
    [Fact]
    public void Validateは不正なLuaテキストを検証すると失敗ファイルを返す() {
        var loggerMock = new Mock<ILoggingService>();
        var sut = new LuaSyntaxValidationService( loggerMock.Object );

        var result = sut.Validate(
        [
            new LuaSyntaxValidationTarget( @"C:\work\broken.lua", "function(" ),
        ] );

        Assert.False( result.IsSuccess );
        var failure = Assert.Single( result.Failures );
        Assert.Equal( @"C:\work\broken.lua", failure.FilePath );
        Assert.False( string.IsNullOrWhiteSpace( failure.Message ) );
    }
}