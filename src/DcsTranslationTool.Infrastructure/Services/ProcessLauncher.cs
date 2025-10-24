using System.Diagnostics;

using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// <see cref="Process.Start(ProcessStartInfo)"/> を呼び出す。
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher {
    /// <inheritdoc/>
    public Process? Start( ProcessStartInfo startInfo ) => Process.Start( startInfo );
}