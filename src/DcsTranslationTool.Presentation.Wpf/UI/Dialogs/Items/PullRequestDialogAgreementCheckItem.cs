using Caliburn.Micro;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Items;

public class PullRequestDialogAgreementCheckItem( string message ) : PropertyChangedBase {

    #region Fields

    private bool _isAgreed = false;

    #endregion

    #region Properties

    /// <summary>
    /// ユーザーが同意したか
    /// </summary>
    public bool IsAgreed {
        get => _isAgreed;
        set {
            Set( ref _isAgreed, value );
        }
    }

    /// <summary>
    /// ユーザーに同意を求めるメッセージ
    /// </summary>
    public string Message { get; init; } = message;

    #endregion
}