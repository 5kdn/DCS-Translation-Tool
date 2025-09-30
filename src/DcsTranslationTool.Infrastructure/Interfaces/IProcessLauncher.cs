using System.Diagnostics;

namespace DcsTranslationTool.Infrastructure.Interfaces;

/// <summary>
/// プロセス起動処理を抽象化する。
/// </summary>
public interface IProcessLauncher {
    /// <summary>
    /// プロセスを起動する。
    /// </summary>
    /// <param name="startInfo">起動に利用する情報。</param>
    /// <returns>起動したプロセスまたは <see langword="null"/>。</returns>
    Process? Start( ProcessStartInfo startInfo );
}