using System.Diagnostics.CodeAnalysis;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Resources;

using FluentResults;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// アプリケーション更新の取得進捗とキャンセル操作を提供する。
/// </summary>
public sealed class ApplicationUpdateProgressDialogParameters : PropertyChangedBase, IDisposable {
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Func<IProgress<ApplicationUpdateProgress>, CancellationToken, Task<Result<ApplicationUpdateStageResult>>> _operation;
    private readonly ILoggingService? _logger;
    private double _percentage;
    private string _status = string.Empty;
    private bool _isRunning = true;

    /// <summary>
    /// 更新進捗ダイアログを初期化する。
    /// </summary>
    /// <param name="title">タイトルを受け取る。</param>
    /// <param name="operation">進捗とキャンセルを受け取る更新処理を受け取る。</param>
    /// <param name="logger">更新処理の例外を記録するロガーを受け取る。</param>
    public ApplicationUpdateProgressDialogParameters(
        string title,
        Func<IProgress<ApplicationUpdateProgress>, CancellationToken, Task<Result<ApplicationUpdateStageResult>>> operation,
        ILoggingService? logger = null
    ) {
        this.Title = title;
        this._operation = operation;
        this._logger = logger;
    }

    /// <summary>
    /// タイトルを取得する。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 進捗率を取得する。
    /// </summary>
    public double Percentage {
        get => this._percentage;
        private set => this.Set( ref this._percentage, value );
    }

    /// <summary>
    /// 現在の処理内容を取得する。
    /// </summary>
    public string Status {
        get => this._status;
        private set => this.Set( ref this._status, value );
    }

    /// <summary>
    /// キャンセル操作が有効かどうかを取得する。
    /// </summary>
    public bool IsRunning {
        get => this._isRunning;
        private set => this.Set( ref this._isRunning, value );
    }

    /// <summary>
    /// キャンセルボタンの表示文字列を取得する。
    /// </summary>
    [SuppressMessage( "Performance", "CA1822:Mark members as static", Justification = "WPFのインスタンスDataContextからバインドするプロパティとして公開するため。" )]
    public string CancelButtonText => GetResourceString( "ApplicationUpdateCancelButton" );

    /// <summary>
    /// 更新処理を開始し、完了後にダイアログを閉じる。
    /// </summary>
    /// <param name="dialogContent">ダイアログの内容要素を受け取る。</param>
    /// <returns>非同期処理を返す。</returns>
    public async Task RunAsync( object dialogContent ) {
        var progress = new Progress<ApplicationUpdateProgress>( value => {
            this.Percentage = value.Percentage;
            this.Status = value.Phase switch
            {
                ApplicationUpdateProgressPhase.Downloading => GetResourceString( "ApplicationUpdateProgressDownloading" ),
                ApplicationUpdateProgressPhase.Verifying => GetResourceString( "ApplicationUpdateProgressVerifying" ),
                _ => GetResourceString( "ApplicationUpdateProgressStaging" )
            };
        } );

        Result<ApplicationUpdateStageResult> result;
        try {
            result = await this._operation( progress, this._cancellationTokenSource.Token );
        }
        catch(OperationCanceledException) {
            result = Result.Fail( GetResourceString( "ApplicationUpdateCanceledMessage" ) );
        }
        catch(Exception ex) {
            this._logger?.Error( "アプリケーション更新の取得処理で予期しないエラーが発生した。", ex );
            result = Result.Fail( GetResourceString( "ApplicationUpdatePrepareFailureMessage" ) );
        }
        finally {
            this.IsRunning = false;
        }

        DialogHost.CloseDialogCommand.Execute( result, dialogContent as System.Windows.IInputElement );
    }

    /// <summary>
    /// 更新処理のキャンセルを要求する。
    /// </summary>
    public void Cancel() => this._cancellationTokenSource.Cancel();

    /// <inheritdoc />
    public void Dispose() => this._cancellationTokenSource.Dispose();

    private static string GetResourceString( string name ) => Strings_Shared.ResourceManager.GetString( name ) ?? name;
}