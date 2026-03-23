using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;

using MoonSharp.Interpreter;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// MoonSharp を用いて Lua 構文検証を行うサービスを表す。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public sealed class LuaSyntaxValidationService( ILoggingService logger ) : ILuaSyntaxValidationService {
    /// <inheritdoc />
    public LuaSyntaxValidationResult Validate( IReadOnlyList<LuaSyntaxValidationTarget> targets ) {
        ArgumentNullException.ThrowIfNull( targets );

        List<LuaSyntaxValidationFailure> failures = [];
        foreach(var target in targets) {
            try {
                var script = new Script();
                _ = script.LoadString( target.Content );
            }
            catch(SyntaxErrorException ex) {
                logger.Warn( $"Lua 構文検証に失敗した。Path={target.FilePath}, Message={ex.DecoratedMessage ?? ex.Message}" );
                failures.Add( new LuaSyntaxValidationFailure( target.FilePath, ex.DecoratedMessage ?? ex.Message ) );
            }
            catch(InterpreterException ex) {
                logger.Warn( $"Lua 構文検証に失敗した。Path={target.FilePath}, Message={ex.DecoratedMessage ?? ex.Message}" );
                failures.Add( new LuaSyntaxValidationFailure( target.FilePath, ex.DecoratedMessage ?? ex.Message ) );
            }
        }

        return new LuaSyntaxValidationResult( failures );
    }
}