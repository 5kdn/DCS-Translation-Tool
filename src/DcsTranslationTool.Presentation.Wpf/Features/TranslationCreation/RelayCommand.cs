using System.Windows.Input;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation で利用する単純なコマンドを表す。
/// </summary>
/// <param name="execute">実行処理。</param>
/// <param name="canExecute">実行可否判定処理。</param>
internal sealed class RelayCommand(
    Action execute,
    Func<bool>? canExecute = null ) : ICommand {
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute( object? parameter ) => canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute( object? parameter ) => execute();

    /// <summary>
    /// 実行可否変更を通知する。
    /// </summary>
    internal void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke( this, EventArgs.Empty );
}