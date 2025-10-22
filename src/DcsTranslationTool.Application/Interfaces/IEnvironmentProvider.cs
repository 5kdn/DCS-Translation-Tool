namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// 環境変数を取得するプロバイダ
/// </summary>
public interface IEnvironmentProvider {
    /// <summary>
    /// 指定された環境変数の値を取得する
    /// </summary>
    /// <param name="variable">環境変数名</param>
    /// <returns>取得した値。存在しない場合は string.Empty</returns>
    string GetEnvironmentVariable( string variable );

    /// <summary>
    /// %UserProfile% のパスを取得する
    /// </summary>
    /// <returns>UserProfile のパス。存在しない場合は string.Empty</returns>
    string GetUserProfilePath();
}