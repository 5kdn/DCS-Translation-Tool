using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

using FlaApplication = FlaUI.Core.Application;

namespace DcsTranslationTool.EndToEndTests;

/// <summary>
/// publish済みWPFアプリケーションの更新操作を利用者経路で検証する。
/// </summary>
public sealed class ApplicationUpdateEndToEndTests {
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds( 30 );
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds( 120 );

    /// <summary>
    /// 現在版から差分を取得し、新版を再起動して利用者ファイルを保持することを確認する。
    /// </summary>
    [Fact]
    public Task 差分更新は実UIから適用して新版を再起動する() =>
        RunUpdateScenarioAsync( corruptDelta: false );

    /// <summary>
    /// 意味的に破損した差分を拒否し、完全版へフォールバックすることを確認する。
    /// </summary>
    [Fact]
    public Task 破損差分は完全版へフォールバックして新版を再起動する() =>
        RunUpdateScenarioAsync( corruptDelta: true );

    /// <summary>
    /// 後で再起動を選ぶとステージ済み更新を保持し、次回起動時に再確認することを確認する。
    /// </summary>
    [Fact]
    public Task 後で再起動はステージ済み更新を次回起動時に再確認する() =>
        RunLaterThenRestartScenarioAsync();

    /// <summary>
    /// 指定した差分条件で更新の全利用者経路を実行する。
    /// </summary>
    /// <param name="corruptDelta">差分マニフェストを破損させる場合はtrue。</param>
    private static async Task RunUpdateScenarioAsync( bool corruptDelta ) {
        var configuration = EndToEndTestConfiguration.Load();
        var scenarioName = corruptDelta ? "corrupt-delta-fallback" : "delta-success";
        using var workspace = new UpdateScenarioWorkspace( configuration, scenarioName, corruptDelta );
        await using var server = new LocalReleaseServer( configuration.RuntimeIdentifier, workspace.AssetsDirectory );
        await server.StartAsync( TestContext.Current.CancellationToken );
        workspace.WriteApplicationConfiguration( server.BaseUri );

        FlaApplication? launchedApplication = null;
        FlaApplication? restartedApplication = null;
        Window? currentWindow = null;
        using var automation = new UIA3Automation();
        try {
            var startInfo = CreateStartInfo( workspace );
            launchedApplication = FlaApplication.Launch( startInfo );
            workspace.TrackProcess( launchedApplication.ProcessId, workspace.ApplicationExecutablePath );
            currentWindow = await WaitForMainWindowAsync( launchedApplication, automation, UiTimeout );
            currentWindow.Focus();

            var desktop = automation.GetDesktop();
            var updateAction = await WaitForElementAsync( desktop, "UpdateSnackbarAction", UiTimeout );
            updateAction.AsButton().Invoke();

            var downloadDialog = await WaitForElementAsync( desktop, "ApplicationUpdateDownloadConfirmationDialog", UiTimeout );
            var downloadMessage = await WaitForElementAsync( downloadDialog, "ConfirmationDialogMessage", UiTimeout );
            Assert.Contains( "v1.2.0", downloadMessage.Name, StringComparison.Ordinal );
            Assert.DoesNotContain( "種類:", downloadMessage.Name, StringComparison.Ordinal );
            var downloadButton = await WaitForElementAsync( downloadDialog, "ConfirmationDialogRightButton", UiTimeout );
            downloadButton.AsButton().Invoke();

            var progressDialog = await WaitForElementAsync( desktop, "ApplicationUpdateProgressDialog", UiTimeout );
            var progressBar = (await WaitForElementAsync(
                progressDialog,
                "ApplicationUpdateProgressBar",
                UiTimeout )).AsProgressBar();
            var progressStatus = await WaitForElementAsync(
                progressDialog,
                "ApplicationUpdateProgressStatus",
                UiTimeout );
            await WaitForAsync<object>(
                () => !string.IsNullOrWhiteSpace( progressStatus.Name ) && progressBar.Value > progressBar.Minimum
                    ? new object()
                    : null,
                UiTimeout,
                "The update progress state did not become observable." );
            var cancelButton = await WaitForElementAsync(
                progressDialog,
                "ApplicationUpdateProgressCancelButton",
                UiTimeout );
            Assert.False( cancelButton.IsOffscreen );
            Assert.True( cancelButton.IsEnabled );

            var restartDialog = await WaitForElementAsync( desktop, "ApplicationUpdateRestartConfirmationDialog", UpdateTimeout );
            var restartMessage = await WaitForElementAsync( restartDialog, "ConfirmationDialogMessage", UiTimeout );
            Assert.Contains( "1.2.0", restartMessage.Name, StringComparison.Ordinal );
            var restartButton = await WaitForElementAsync( restartDialog, "ConfirmationDialogLeftButton", UiTimeout );
            Assert.Equal( "今すぐ再起動", restartButton.Name );
            restartButton.AsButton().Invoke();

            await WaitForProcessExitAsync( launchedApplication.ProcessId, UiTimeout );
            var restartedProcess = await WaitForRestartedProcessAsync(
                workspace.ApplicationExecutablePath,
                launchedApplication.ProcessId,
                UpdateTimeout );
            workspace.TrackProcess( restartedProcess.Id, workspace.ApplicationExecutablePath );
            restartedApplication = FlaApplication.Attach( restartedProcess );
            currentWindow = await WaitForMainWindowAsync( restartedApplication, automation, UiTimeout );
            Assert.Equal( "MainWindow", currentWindow.Properties.AutomationId.ValueOrDefault );
            Assert.Equal( new Version( 1, 2, 0, 0 ), NormalizeVersion( FileVersionInfo.GetVersionInfo( workspace.ApplicationExecutablePath ).FileVersion ) );

            workspace.AssertTargetManifest();
            workspace.AssertPreservedFiles();
            AssertRequestHistory( server.Requests, configuration.RuntimeIdentifier, corruptDelta );
            workspace.WriteRequestLog( server.Requests );
            restartedApplication.Dispose();
            restartedApplication = null;
            workspace.TerminateOwnedProcesses();
            workspace.AssertNoOwnedProcesses();
            workspace.MarkSucceeded();
        }
        catch {
            var processId = restartedApplication?.ProcessId ?? launchedApplication?.ProcessId;
            var automationRoot = currentWindow ?? TryFindProcessAutomationRoot( automation, processId );
            CaptureDiagnostics( workspace.RootDirectory, automationRoot, processId );
            workspace.WriteRequestLog( server.Requests );
            throw;
        }
        finally {
            restartedApplication?.Dispose();
            launchedApplication?.Dispose();
        }
    }

    /// <summary>
    /// ステージ済み更新を後で適用する操作の後、アプリを改めて起動する経路を実行する。
    /// </summary>
    private static async Task RunLaterThenRestartScenarioAsync() {
        var configuration = EndToEndTestConfiguration.Load();
        using var workspace = new UpdateScenarioWorkspace( configuration, "later-then-restart", corruptDelta: false );
        await using var server = new LocalReleaseServer( configuration.RuntimeIdentifier, workspace.AssetsDirectory );
        await server.StartAsync( TestContext.Current.CancellationToken );
        workspace.WriteApplicationConfiguration( server.BaseUri );

        FlaApplication? firstApplication = null;
        FlaApplication? secondApplication = null;
        FlaApplication? restartedApplication = null;
        Window? currentWindow = null;
        using var automation = new UIA3Automation();
        try {
            firstApplication = FlaApplication.Launch( CreateStartInfo( workspace ) );
            var firstProcessId = firstApplication.ProcessId;
            workspace.TrackProcess( firstProcessId, workspace.ApplicationExecutablePath );
            currentWindow = await WaitForMainWindowAsync( firstApplication, automation, UiTimeout );
            currentWindow.Focus();

            var desktop = automation.GetDesktop();
            var updateAction = await WaitForElementAsync( desktop, "UpdateSnackbarAction", UiTimeout );
            updateAction.AsButton().Invoke();
            var downloadDialog = await WaitForElementAsync( desktop, "ApplicationUpdateDownloadConfirmationDialog", UiTimeout );
            (await WaitForElementAsync( downloadDialog, "ConfirmationDialogRightButton", UiTimeout )).AsButton().Invoke();

            var restartDialog = await WaitForElementAsync( desktop, "ApplicationUpdateRestartConfirmationDialog", UpdateTimeout );
            var laterButton = await WaitForElementAsync( restartDialog, "ConfirmationDialogCenterButton", UiTimeout );
            Assert.Equal( "後で", laterButton.Name );
            laterButton.AsButton().Invoke();
            await WaitForAsync<object>(
                () => FindElement( desktop, "ApplicationUpdateRestartConfirmationDialog" ) is null ? new object() : null,
                UiTimeout,
                "The restart confirmation dialog did not close after selecting later." );
            workspace.AssertStagedUpdate();

            firstApplication.Dispose();
            firstApplication = null;
            workspace.TerminateOwnedProcesses();
            await WaitForProcessExitAsync( firstProcessId, UiTimeout );

            secondApplication = FlaApplication.Launch( CreateStartInfo( workspace ) );
            workspace.TrackProcess( secondApplication.ProcessId, workspace.ApplicationExecutablePath );
            currentWindow = await WaitForMainWindowAsync( secondApplication, automation, UiTimeout );
            currentWindow.Focus();
            await WaitForElementAsync( desktop, "UpdateSnackbarAction", UiTimeout );
            Assert.Equal( 1, server.Requests.Count( path => path == "/repos/dcs-translation-japanese/DCS-Translation-Tool/releases" ) );
            workspace.AssertStagedUpdate();

            updateAction = await WaitForElementAsync( desktop, "UpdateSnackbarAction", UiTimeout );
            updateAction.AsButton().Invoke();
            restartDialog = await WaitForElementAsync( desktop, "ApplicationUpdateRestartConfirmationDialog", UpdateTimeout );
            var restartButton = await WaitForElementAsync( restartDialog, "ConfirmationDialogLeftButton", UiTimeout );
            Assert.Equal( "今すぐ再起動", restartButton.Name );
            restartButton.AsButton().Invoke();

            var secondProcessId = secondApplication.ProcessId;
            await WaitForProcessExitAsync( secondProcessId, UiTimeout );
            var restartedProcess = await WaitForRestartedProcessAsync(
                workspace.ApplicationExecutablePath,
                secondProcessId,
                UpdateTimeout );
            workspace.TrackProcess( restartedProcess.Id, workspace.ApplicationExecutablePath );
            restartedApplication = FlaApplication.Attach( restartedProcess );
            currentWindow = await WaitForMainWindowAsync( restartedApplication, automation, UiTimeout );
            Assert.Equal( new Version( 1, 2, 0, 0 ), NormalizeVersion( FileVersionInfo.GetVersionInfo( workspace.ApplicationExecutablePath ).FileVersion ) );
            workspace.AssertTargetManifest();
            workspace.AssertPreservedFiles();

            workspace.WriteRequestLog( server.Requests );
            restartedApplication.Dispose();
            restartedApplication = null;
            secondApplication.Dispose();
            secondApplication = null;
            workspace.TerminateOwnedProcesses();
            workspace.AssertNoOwnedProcesses();
            workspace.MarkSucceeded();
        }
        catch {
            var processId = restartedApplication?.ProcessId ?? secondApplication?.ProcessId ?? firstApplication?.ProcessId;
            var automationRoot = currentWindow ?? TryFindProcessAutomationRoot( automation, processId );
            CaptureDiagnostics( workspace.RootDirectory, automationRoot, processId );
            workspace.WriteRequestLog( server.Requests );
            throw;
        }
        finally {
            restartedApplication?.Dispose();
            secondApplication?.Dispose();
            firstApplication?.Dispose();
        }
    }

    /// <summary>
    /// E2E構成を継承させるアプリケーション起動情報を生成する。
    /// </summary>
    /// <param name="workspace">対象シナリオのワークスペース。</param>
    /// <returns>FlaUIへ渡すプロセス起動情報を返す。</returns>
    private static ProcessStartInfo CreateStartInfo( UpdateScenarioWorkspace workspace ) {
        var startInfo = new ProcessStartInfo
        {
            FileName = workspace.ApplicationExecutablePath,
            WorkingDirectory = workspace.ApplicationDirectory,
            UseShellExecute = false
        };
        startInfo.Environment["DCS_TRANSLATION_TOOL_E2E_CONFIG"] = workspace.ConfigurationPath;
        return startInfo;
    }

    /// <summary>
    /// メインウィンドウがUI Automationから取得可能になるまで待機する。
    /// </summary>
    /// <param name="application">接続対象アプリケーション。</param>
    /// <param name="automation">UIA3 automation。</param>
    /// <param name="timeout">最大待機時間。</param>
    /// <returns>取得したメインウィンドウを返す。</returns>
    private static Task<Window> WaitForMainWindowAsync(
        FlaApplication application,
        UIA3Automation automation,
        TimeSpan timeout
    ) => WaitForAsync(
        () => {
            try {
                return automation.GetDesktop()
                    .FindFirstChild( condition => condition
                        .ByProcessId( application.ProcessId )
                        .And( condition.ByAutomationId( "MainWindow" ) ) )
                    ?.AsWindow();
            }
            catch(InvalidOperationException) {
                return null;
            }
        },
        timeout,
        "The main window was not available." );

    /// <summary>
    /// 指定AutomationIdの子孫要素が現れるまで待機する。
    /// </summary>
    /// <param name="root">検索ルート。</param>
    /// <param name="automationId">検索するAutomationId。</param>
    /// <param name="timeout">最大待機時間。</param>
    /// <returns>取得した要素を返す。</returns>
    private static Task<AutomationElement> WaitForElementAsync(
        AutomationElement root,
        string automationId,
        TimeSpan timeout
    ) => WaitForAsync(
        () => FindElement( root, automationId ),
        timeout,
        $"Automation element was not available: {automationId}" );

    /// <summary>
    /// 指定AutomationIdの子孫要素を取得する。
    /// </summary>
    /// <param name="root">検索ルートを受け取る。</param>
    /// <param name="automationId">検索するAutomationIdを受け取る。</param>
    /// <returns>取得できた場合は要素、そうでない場合はnullを返す。</returns>
    private static AutomationElement? FindElement( AutomationElement root, string automationId ) {
        try {
            return root.FindFirstDescendant( condition => condition.ByAutomationId( automationId ) );
        }
        catch(Exception ex) when(ex is InvalidOperationException or COMException) {
            return null;
        }
    }

    /// <summary>
    /// 旧アプリケーションプロセスの終了を待機する。
    /// </summary>
    /// <param name="processId">旧プロセスID。</param>
    /// <param name="timeout">最大待機時間。</param>
    private static async Task WaitForProcessExitAsync( int processId, TimeSpan timeout ) {
        await WaitForAsync<object>(
            () => {
                try {
                    using var process = Process.GetProcessById( processId );
                    return process.HasExited ? new object() : null;
                }
                catch(ArgumentException) {
                    return new object();
                }
            },
            timeout,
            "The previous application process did not exit." );
    }

    /// <summary>
    /// 更新後の展開先EXEから起動した新プロセスを待機する。
    /// </summary>
    /// <param name="executablePath">期待するEXEの絶対パス。</param>
    /// <param name="previousProcessId">除外する旧プロセスID。</param>
    /// <param name="timeout">最大待機時間。</param>
    /// <returns>再起動したプロセスを返す。</returns>
    private static Task<Process> WaitForRestartedProcessAsync(
        string executablePath,
        int previousProcessId,
        TimeSpan timeout
    ) => WaitForAsync(
        () => FindRestartedProcess( executablePath, previousProcessId ),
        timeout,
        "The updated application process did not start." );

    /// <summary>
    /// 更新後の実行ファイルに一致するプロセスを検索し、不一致のプロセスハンドルを解放する。
    /// </summary>
    /// <param name="executablePath">期待するEXEの絶対パス。</param>
    /// <param name="previousProcessId">除外する旧プロセスID。</param>
    /// <returns>一致したプロセス。一致しない場合はnull。</returns>
    private static Process? FindRestartedProcess( string executablePath, int previousProcessId ) {
        Process? matchedProcess = null;
        foreach(var process in Process.GetProcessesByName( Path.GetFileNameWithoutExtension( executablePath ) )) {
            try {
                if(matchedProcess is null &&
                    process.Id != previousProcessId &&
                    process.MainWindowHandle != IntPtr.Zero &&
                    PathsEqual( TryGetProcessPath( process ), executablePath )) {
                    matchedProcess = process;
                }
                else {
                    process.Dispose();
                }
            }
            catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception) {
                process.Dispose();
            }
        }

        return matchedProcess;
    }

    /// <summary>
    /// 値が取得できるまで短い間隔で期限付きポーリングを行う。
    /// </summary>
    /// <typeparam name="T">取得する参照型。</typeparam>
    /// <param name="valueFactory">現在値を取得する関数。</param>
    /// <param name="timeout">最大待機時間。</param>
    /// <param name="failureMessage">期限超過時のメッセージ。</param>
    /// <returns>取得した値を返す。</returns>
    /// <exception cref="TimeoutException">期限内に値を取得できない場合に発生する。</exception>
    private static async Task<T> WaitForAsync<T>(
        Func<T?> valueFactory,
        TimeSpan timeout,
        string failureMessage
    ) where T : class {
        var stopwatch = Stopwatch.StartNew();
        while(stopwatch.Elapsed < timeout) {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var value = valueFactory();
            if(value is not null) {
                return value;
            }

            await Task.Delay( TimeSpan.FromMilliseconds( 100 ), TestContext.Current.CancellationToken );
        }

        throw new TimeoutException( failureMessage );
    }

    /// <summary>
    /// HTTP要求履歴が差分選択と必要なフォールバックを示すことを確認する。
    /// </summary>
    /// <param name="requests">Releaseサーバー要求履歴。</param>
    /// <param name="runtimeIdentifier">対象RID。</param>
    /// <param name="corruptDelta">完全版取得を期待する場合はtrue。</param>
    private static void AssertRequestHistory(
        IReadOnlyCollection<string> requests,
        string runtimeIdentifier,
        bool corruptDelta
    ) {
        var firstDeltaPath = $"/assets/DCS-Translation-Tool-1.0.0-to-1.1.0-{runtimeIdentifier}.delta.zip";
        var secondDeltaPath = $"/assets/DCS-Translation-Tool-1.1.0-to-1.2.0-{runtimeIdentifier}.delta.zip";
        var fullPath = $"/assets/DCS-Translation-Tool-1.2.0-{runtimeIdentifier}.zip";
        var firstDeltaSidecarPath = $"{firstDeltaPath}.sha256";
        var secondDeltaSidecarPath = $"{secondDeltaPath}.sha256";
        var fullSidecarPath = $"{fullPath}.sha256";
        var orderedRequests = requests.ToArray();
        Assert.Equal( 1, orderedRequests.Count( path => path == "/repos/dcs-translation-japanese/DCS-Translation-Tool/releases" ) );
        Assert.Equal( 1, orderedRequests.Count( path => path == firstDeltaPath ) );
        Assert.Equal( 1, orderedRequests.Count( path => path == firstDeltaSidecarPath ) );
        Assert.Equal( 1, orderedRequests.Count( path => path == secondDeltaPath ) );
        Assert.Equal( 1, orderedRequests.Count( path => path == secondDeltaSidecarPath ) );
        Assert.True(
            Array.IndexOf( orderedRequests, firstDeltaPath ) < Array.IndexOf( orderedRequests, secondDeltaPath ),
            "Delta packages must be requested in version order." );
        if(corruptDelta) {
            Assert.Equal( 1, orderedRequests.Count( path => path == fullPath ) );
            Assert.Equal( 1, orderedRequests.Count( path => path == fullSidecarPath ) );
            Assert.True(
                Array.IndexOf( orderedRequests, secondDeltaPath ) < Array.IndexOf( orderedRequests, fullPath ),
                "The full package must be requested only after the delta package." );
        }
        else {
            Assert.DoesNotContain( fullPath, orderedRequests );
            Assert.DoesNotContain( fullSidecarPath, orderedRequests );
        }
    }

    /// <summary>
    /// 指定プロセスが所有するトップレベルUI Automation要素を取得する。
    /// </summary>
    /// <param name="automation">UIA3 automation。</param>
    /// <param name="processId">検索対象PID。</param>
    /// <returns>取得した要素。取得できない場合はnull。</returns>
    private static AutomationElement? TryFindProcessAutomationRoot( UIA3Automation automation, int? processId ) {
        if(processId is null) {
            return null;
        }

        try {
            return automation.GetDesktop()
                .FindFirstChild( condition => condition.ByProcessId( processId.Value ) );
        }
        catch(Exception ex) when(ex is InvalidOperationException or COMException) {
            return null;
        }
    }

    /// <summary>
    /// 失敗時の画面とUI Automationツリーを診断出力する。
    /// </summary>
    /// <param name="diagnosticsDirectory">診断出力先。</param>
    /// <param name="automationRoot">取得済みの対象プロセスUI Automationルート。</param>
    /// <param name="processId">診断対象PID。</param>
    private static void CaptureDiagnostics(
        string diagnosticsDirectory,
        AutomationElement? automationRoot,
        int? processId
    ) {
        try {
            Capture.Screen().ToFile( Path.Combine( diagnosticsDirectory, "screen.png" ) );
        }
        catch(Exception ex) when(ex is InvalidOperationException or ExternalException) {
            File.WriteAllText( Path.Combine( diagnosticsDirectory, "screen-error.txt" ), ex.ToString() );
        }

        try {
            if(automationRoot is null) {
                File.WriteAllText(
                    Path.Combine( diagnosticsDirectory, "automation-tree.txt" ),
                    $"UI Automation root was not available for process {processId?.ToString() ?? "unknown"}." );
            }
            else {
                var builder = new StringBuilder();
                AppendAutomationTree( automationRoot, builder, 0 );
                File.WriteAllText( Path.Combine( diagnosticsDirectory, "automation-tree.txt" ), builder.ToString() );
            }
        }
        catch(Exception ex) {
            File.WriteAllText( Path.Combine( diagnosticsDirectory, "automation-tree.txt" ), ex.ToString() );
        }
    }

    /// <summary>
    /// UI Automation要素ツリーを再帰的に文字列化する。
    /// </summary>
    /// <param name="element">現在の要素。</param>
    /// <param name="builder">出力先。</param>
    /// <param name="depth">現在の深さ。</param>
    private static void AppendAutomationTree( AutomationElement element, StringBuilder builder, int depth ) {
        builder.Append( ' ', depth * 2 )
            .Append( element.Properties.ControlType.ValueOrDefault )
            .Append( " Id=" ).Append( element.Properties.AutomationId.ValueOrDefault )
            .Append( " Name=" ).AppendLine( element.Properties.Name.ValueOrDefault );
        foreach(var child in element.FindAllChildren()) {
            AppendAutomationTree( child, builder, depth + 1 );
        }
    }

    /// <summary>
    /// プロセスの実行ファイルパスを安全に取得する。
    /// </summary>
    /// <param name="process">対象プロセス。</param>
    /// <returns>取得できた絶対パス。取得できない場合はnull。</returns>
    private static string? TryGetProcessPath( Process process ) {
        try {
            return process.MainModule?.FileName;
        }
        catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception) {
            return null;
        }
    }

    /// <summary>
    /// Windowsパスを大小文字を区別せず比較する。
    /// </summary>
    /// <param name="left">左辺パス。</param>
    /// <param name="right">右辺パス。</param>
    /// <returns>同一パスの場合はtrue。</returns>
    private static bool PathsEqual( string? left, string right ) =>
        left is not null &&
        string.Equals( Path.GetFullPath( left ), Path.GetFullPath( right ), StringComparison.OrdinalIgnoreCase );

    /// <summary>
    /// FileVersion文字列を4要素のVersionへ正規化する。
    /// </summary>
    /// <param name="fileVersion">FileVersion文字列。</param>
    /// <returns>正規化したバージョンを返す。</returns>
    private static Version NormalizeVersion( string? fileVersion ) =>
        Version.TryParse( fileVersion, out var version )
            ? new Version( version.Major, version.Minor, Math.Max( 0, version.Build ), Math.Max( 0, version.Revision ) )
            : new Version( 0, 0, 0, 0 );
}
