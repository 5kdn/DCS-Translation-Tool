namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// アプリケーション情報を提供するサービスのインターフェース
/// </summary>
public interface IApplicationInfoService {
    /// <summary>
    /// バージョン情報を取得する。<br/>
    /// バージョン情報が取得できない場合は 0.0.0.0 を返す
    /// </summary>
    /// <returns>アプリケーションのバージョンを返す</returns>
    Version GetVersion();
}