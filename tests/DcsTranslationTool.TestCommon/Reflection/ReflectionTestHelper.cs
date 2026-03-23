using System.Reflection;

namespace DcsTranslationTool.TestCommon.Reflection;

/// <summary>
/// テスト用リフレクション呼び出しを補助する。
/// </summary>
public static class ReflectionTestHelper {
    /// <summary>
    /// 非公開インスタンス メソッドを非同期タスクとして呼び出す。
    /// </summary>
    /// <param name="target">対象インスタンス。</param>
    /// <param name="methodName">メソッド名。</param>
    /// <param name="args">引数一覧。</param>
    /// <returns>返却されたタスクを返す。</returns>
    public static Task InvokeNonPublicTaskAsync( object target, string methodName, params object?[] args ) {
        var method = target.GetType().GetMethod( methodName, BindingFlags.Instance | BindingFlags.NonPublic )
            ?? throw new InvalidOperationException( $"{methodName} が見つからない。" );
        return (Task)(method.Invoke( target, args ) ?? Task.CompletedTask);
    }
}