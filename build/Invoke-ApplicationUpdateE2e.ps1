<#
.SYNOPSIS
publish済みWPFアプリケーションを使用して更新E2Eテストを実行する。

.DESCRIPTION
指定RIDについて1.0.0、1.1.0、1.2.0のE2E専用自己完結publishを生成し、既存の更新アセット生成処理で
完全版と隣接バージョン間の差分版を作成する。生成後、FlaUI UIA3とlocalhost Releaseサーバーを使用する専用xUnit
プロジェクトを実行する。出力先はリポジトリ内のartifacts/e2e配下に限定する。

.PARAMETER RuntimeIdentifier
実行対象のRIDをwin-x64またはwin-x86で指定する。

.PARAMETER OutputDirectory
生成物と診断結果の保存先を指定する。省略時はartifacts/e2e/{RID}を使用する。

.OUTPUTS
なし。テスト生成物、TRX、失敗時診断情報をOutputDirectoryへ保存する。

.EXAMPLE
./build/Invoke-ApplicationUpdateE2e.ps1 -RuntimeIdentifier win-x64

win-x64向けの差分成功と完全版フォールバックE2Eを実行する。
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-x86')]
    [string] $RuntimeIdentifier,

    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\e2e'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $allowedRoot $RuntimeIdentifier
}

$output = [IO.Path]::GetFullPath($OutputDirectory)
$allowedPrefix = $allowedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "E2E output must be under $allowedRoot"
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

$baseStage = Join-Path $output 'base-stage'
$baseAssets = Join-Path $output 'base-assets'
$intermediateStage = Join-Path $output 'intermediate-stage'
$intermediateAssets = Join-Path $output 'intermediate-assets'
$targetStage = Join-Path $output 'target-stage'
$targetAssets = Join-Path $output 'target-assets'
$majorBoundaryAssets = Join-Path $output 'major-boundary-assets'
$diagnostics = Join-Path $output 'diagnostics'
$testResults = Join-Path $output 'test-results'
New-Item -ItemType Directory -Force -Path $baseStage, $baseAssets, $intermediateStage, $intermediateAssets, $targetStage, $targetAssets, $majorBoundaryAssets, $diagnostics, $testResults | Out-Null

$applicationProject = Join-Path $repoRoot 'src\DcsTranslationTool.Presentation.Wpf\DcsTranslationTool.Presentation.Wpf.csproj'
$e2eProject = Join-Path $repoRoot 'tests\DcsTranslationTool.EndToEndTests\DcsTranslationTool.EndToEndTests.csproj'

& dotnet restore $applicationProject --locked-mode '-p:E2ETesting=true'
if ($LASTEXITCODE -ne 0) { throw 'Application restore failed.' }

& dotnet publish $applicationProject `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    --no-restore `
    -o $baseStage `
    '-p:E2ETesting=true' `
    '-p:Version=1.0.0' `
    '-p:PublishSingleFile=false' `
    '-p:PublishTrimmed=false' `
    '-p:PublishReadyToRun=false'
if ($LASTEXITCODE -ne 0) { throw 'Base application publish failed.' }

& (Join-Path $PSScriptRoot 'New-ApplicationUpdateAssets.ps1') `
    -StageDirectory $baseStage `
    -TargetVersion '1.0.0' `
    -RuntimeIdentifier $RuntimeIdentifier `
    -OutputDirectory $baseAssets

& dotnet publish $applicationProject `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    --no-restore `
    -o $intermediateStage `
    '-p:E2ETesting=true' `
    '-p:Version=1.1.0' `
    '-p:PublishSingleFile=false' `
    '-p:PublishTrimmed=false' `
    '-p:PublishReadyToRun=false'
if ($LASTEXITCODE -ne 0) { throw 'Intermediate application publish failed.' }

$basePackage = Join-Path $baseAssets "DCS-Translation-Tool-1.0.0-$RuntimeIdentifier.zip"
& (Join-Path $PSScriptRoot 'New-ApplicationUpdateAssets.ps1') `
    -StageDirectory $intermediateStage `
    -TargetVersion '1.1.0' `
    -RuntimeIdentifier $RuntimeIdentifier `
    -OutputDirectory $intermediateAssets `
    -BasePackagePath $basePackage

& dotnet publish $applicationProject `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    --no-restore `
    -o $targetStage `
    '-p:E2ETesting=true' `
    '-p:Version=1.2.0' `
    '-p:PublishSingleFile=false' `
    '-p:PublishTrimmed=false' `
    '-p:PublishReadyToRun=false'
if ($LASTEXITCODE -ne 0) { throw 'Target application publish failed.' }

$intermediatePackage = Join-Path $intermediateAssets "DCS-Translation-Tool-1.1.0-$RuntimeIdentifier.zip"
& (Join-Path $PSScriptRoot 'New-ApplicationUpdateAssets.ps1') `
    -StageDirectory $targetStage `
    -TargetVersion '1.2.0' `
    -RuntimeIdentifier $RuntimeIdentifier `
    -OutputDirectory $targetAssets `
    -BasePackagePath $intermediatePackage

$staleMajorDelta = Join-Path $majorBoundaryAssets "DCS-Translation-Tool-1.2.0-to-2.0.0-$RuntimeIdentifier.delta.zip"
New-Item -ItemType File -Force -Path $staleMajorDelta, "$staleMajorDelta.sha256" | Out-Null
& (Join-Path $PSScriptRoot 'New-ApplicationUpdateAssets.ps1') `
    -StageDirectory $targetStage `
    -TargetVersion '2.0.0' `
    -RuntimeIdentifier $RuntimeIdentifier `
    -OutputDirectory $majorBoundaryAssets `
    -BasePackagePath $intermediatePackage
$majorFullPackage = Join-Path $majorBoundaryAssets "DCS-Translation-Tool-2.0.0-$RuntimeIdentifier.zip"
if (-not (Test-Path -LiteralPath $majorFullPackage -PathType Leaf) -or
    -not (Test-Path -LiteralPath "$majorFullPackage.sha256" -PathType Leaf)) {
    throw 'Major-boundary full assets were not generated.'
}
if (@(Get-ChildItem -LiteralPath $majorBoundaryAssets -File -Filter '*.delta.zip*').Count -ne 0) {
    throw 'Major-boundary delta assets were not removed.'
}

& dotnet restore $e2eProject --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'E2E test restore failed.' }

$runnerInfo = @(
    "ImageOS=$($env:ImageOS)",
    "ImageVersion=$($env:ImageVersion)",
    "OSVersion=$([Environment]::OSVersion.VersionString)",
    "ProcessArchitecture=$([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)"
)
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

public static class E2eDisplayNativeMethods
{
    [DllImport("user32.dll")]
    public static extern uint GetDpiForSystem();
}
'@
    $runnerInfo += "Dpi=$([E2eDisplayNativeMethods]::GetDpiForSystem())"
    $runnerInfo += "ScreenCount=$([Windows.Forms.Screen]::AllScreens.Count)"
    foreach ($screen in [Windows.Forms.Screen]::AllScreens) {
        $runnerInfo += "Screen=$($screen.DeviceName);Bounds=$($screen.Bounds);WorkingArea=$($screen.WorkingArea);Primary=$($screen.Primary)"
    }
}
catch {
    $runnerInfo += "DisplayInformation=unavailable;$($_.Exception.Message)"
}
[IO.File]::WriteAllLines((Join-Path $diagnostics 'runner-info.txt'), $runnerInfo, [Text.UTF8Encoding]::new($false))

$previousRid = $env:DCS_TRANSLATION_TOOL_E2E_RID
$previousArtifacts = $env:DCS_TRANSLATION_TOOL_E2E_ARTIFACTS
$previousDiagnostics = $env:DCS_TRANSLATION_TOOL_E2E_DIAGNOSTICS
try {
    $env:DCS_TRANSLATION_TOOL_E2E_RID = $RuntimeIdentifier
    $env:DCS_TRANSLATION_TOOL_E2E_ARTIFACTS = $output
    $env:DCS_TRANSLATION_TOOL_E2E_DIAGNOSTICS = $diagnostics

    & dotnet test $e2eProject `
        -c Release `
        --no-restore `
        --logger "trx;LogFileName=application-update-$RuntimeIdentifier.trx" `
        --results-directory $testResults
    if ($LASTEXITCODE -ne 0) { throw "Application update E2E failed for $RuntimeIdentifier." }
}
finally {
    $env:DCS_TRANSLATION_TOOL_E2E_RID = $previousRid
    $env:DCS_TRANSLATION_TOOL_E2E_ARTIFACTS = $previousArtifacts
    $env:DCS_TRANSLATION_TOOL_E2E_DIAGNOSTICS = $previousDiagnostics
}