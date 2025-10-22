using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DcsTranslationTool.Shared.Models;

/// <summary>
/// アプリケーション全体の設定を保持するモデル。
/// </summary>
public class AppSettings : INotifyPropertyChanged {

    /// <summary>設定スキーマの互換性管理用。</summary>
    public int SchemaVersion { get; init; } = 1;

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>( ref T field, T value, [CallerMemberName] string? name = null ) {
        if(EqualityComparer<T>.Default.Equals( field, value )) return false;
        field = value;
        PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( name ) );
        return true;
    }

    #region Fields

    private string _applicationTitle = "DcsTranslationTool";
    private double _shellWidth = 600;
    private double _shellHeight = 400;
    private string _sourceAircraftDir = string.Empty;
    private string _sourceDlcCampaignDir = string.Empty;
    private string _sourceUserMissionDir = string.Empty;
    private string _translateFileDir = Path.Combine(AppContext.BaseDirectory, "TranslateFile");
    ///<summary>このアプリのリポジトリーURL。</summary>
    private const string _repositoryUrl = "https://github.com/5kdn/DCS-Translation-Tool/";

    #endregion

    #region Properties

    /// <summary>このアプリの最新版へのリンク。</summary>
    [JsonIgnore]
    public string ReleaseLatestUrl { get; init; } = _repositoryUrl + "releases/latest";

    /// <summary>このアプリのライセンス。</summary>
    [JsonIgnore]
    public string LicenseType { get; init; } = "MIT";

    [JsonIgnore]
    public string LicenseUrl { get; init; } = _repositoryUrl + "blob/master/LICENSE";

    /// <summary>このアプリのプライバシーに関する声明。</summary>
    [JsonIgnore]
    public string PrivacyStatement { get; init; } = _repositoryUrl + "blob/master/PRIVACY.md";

    /// <summary>アプリケーションのタイトル文字列。</summary>
    [JsonIgnore]
    public string ApplicationTitle {
        get => _applicationTitle;
        set => Set( ref _applicationTitle, value );
    }

    /// <summary>シェルウィンドウの幅。</summary>
    public double ShellWidth {
        get => _shellWidth;
        set => Set( ref _shellWidth, value );
    }

    /// <summary>シェルウィンドウの高さ。</summary>
    public double ShellHeight {
        get => _shellHeight;
        set => Set( ref _shellHeight, value );
    }

    public string SourceAircraftDir {
        get => _sourceAircraftDir;
        set => Set( ref _sourceAircraftDir, value );
    }

    public string SourceDlcCampaignDir {
        get => _sourceDlcCampaignDir;
        set => Set( ref _sourceDlcCampaignDir, value );
    }

    public string SourceUserMissionDir {
        get => _sourceUserMissionDir;
        set => Set( ref _sourceUserMissionDir, value );
    }

    public string TranslateFileDir {
        get => _translateFileDir;
        set => Set( ref _translateFileDir, value );
    }

    #endregion
}