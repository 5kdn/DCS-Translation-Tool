using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// <see cref="LuaCodeStringDetector"/> の判定を検証する。
/// </summary>
public sealed class LuaCodeStringDetectorTests {
    [Theory]
    [InlineData( "trigger.action.outText('x', 10)" )]
    [InlineData( "Unit.getByName('A') ~= nil" )]
    [InlineData( "if a then return b end" )]
    [InlineData( "goto label\n::label::\nreturn" )]
    [InlineData( "local env = _ENV" )]
    [InlineData( "-- comment only\ntrigger.action.outText('x', 10)" )]
    [InlineData( "--[=[comment]=]\nUnit.getByName('A')" )]
    public void IsLuaCodeStringはLuaとして解釈可能な文字列を検出する( string value ) {
        var result = LuaCodeStringDetector.IsLuaCodeString( value );

        Assert.True( result );
    }

    [Theory]
    [InlineData( "-- comment only" )]
    [InlineData( " --[[ comment ]]" )]
    [InlineData( "--[=[ comment ]=]" )]
    [InlineData( "--[[line1\nline2]]" )]
    [InlineData( "Engage targets at dawn." )]
    [InlineData( "Pilot, check in on channel 2." )]
    [InlineData( "\"plain string literal\"" )]
    public void IsLuaCodeStringはコメントのみまたは通常文を除外する( string value ) {
        var result = LuaCodeStringDetector.IsLuaCodeString( value );

        Assert.False( result );
    }
}