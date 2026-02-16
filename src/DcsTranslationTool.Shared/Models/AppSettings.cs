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
    private ApiRoutePreference _apiPreferredRoute = ApiRoutePreference.None;
    private DateTimeOffset? _apiPreferredRouteValidUntilUtc;
    private DateTimeOffset? _apiRouteLastVerifiedAtUtc;
    private DateTimeOffset? _apiRouteVerificationRetryAfterUtc;
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

    /// <summary>API通信で優先する経路を保持する。</summary>
    public ApiRoutePreference ApiPreferredRoute {
        get => _apiPreferredRoute;
        set => Set( ref _apiPreferredRoute, value );
    }

    /// <summary>優先経路の有効期限UTCを保持する。</summary>
    public DateTimeOffset? ApiPreferredRouteValidUntilUtc {
        get => _apiPreferredRouteValidUntilUtc;
        set => Set( ref _apiPreferredRouteValidUntilUtc, value );
    }

    /// <summary>優先経路を最後に検証した時刻UTCを保持する。</summary>
    public DateTimeOffset? ApiRouteLastVerifiedAtUtc {
        get => _apiRouteLastVerifiedAtUtc;
        set => Set( ref _apiRouteLastVerifiedAtUtc, value );
    }

    /// <summary>優先経路の再検証を再試行できる時刻UTCを保持する。</summary>
    public DateTimeOffset? ApiRouteVerificationRetryAfterUtc {
        get => _apiRouteVerificationRetryAfterUtc;
        set => Set( ref _apiRouteVerificationRetryAfterUtc, value );
    }

    #endregion
}

/// <summary>API通信で利用する経路種別を表す。</summary>
public enum ApiRoutePreference {
    None,
    Default,
    Ipv4,
    Ipv6,
}