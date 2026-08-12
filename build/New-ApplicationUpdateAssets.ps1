<#
.SYNOPSIS
アプリケーション更新用の完全版および差分版アセットを生成する。

.DESCRIPTION
publish済みファイルへ更新マニフェストを追加し、完全版ZIPとSHA-256 sidecarを生成する。
互換性のある基準パッケージが指定された場合は、ファイル単位の差分ZIPとsidecarも生成し、
差分から再構築した内容が更新先マニフェストと一致することを検証する。

.PARAMETER StageDirectory
publish出力と配布文書を集約したステージングディレクトリを指定する。

.PARAMETER TargetVersion
生成する更新アセットの更新先バージョンを指定する。

.PARAMETER RuntimeIdentifier
生成対象のRIDをwin-x64またはwin-x86で指定する。

.PARAMETER OutputDirectory
生成したZIPとSHA-256 sidecarの出力先ディレクトリを指定する。

.PARAMETER BasePackagePath
差分生成の基準にする直前バージョンの完全版ZIPを指定する。省略時またはファイルが存在しない場合は完全版のみ生成する。

.OUTPUTS
なし。生成した更新アセットをOutputDirectoryへ保存する。

.EXAMPLE
./build/New-ApplicationUpdateAssets.ps1 -StageDirectory ./package/win-x64 -TargetVersion 1.4.0 -RuntimeIdentifier win-x64 -OutputDirectory ./assets/win-x64

win-x64向けの完全版更新アセットを生成する。

.EXAMPLE
./build/New-ApplicationUpdateAssets.ps1 -StageDirectory ./package/win-x64 -TargetVersion 1.4.0 -RuntimeIdentifier win-x64 -OutputDirectory ./assets/win-x64 -BasePackagePath ./previous/DCS-Translation-Tool-1.3.1-win-x64.zip

完全版に加えて、指定した基準パッケージからの差分更新アセットを生成する。
#>
param(
    [Parameter(Mandatory = $true)] [string] $StageDirectory,
    [Parameter(Mandatory = $true)] [string] $TargetVersion,
    [Parameter(Mandatory = $true)] [ValidateSet('win-x64', 'win-x86')] [string] $RuntimeIdentifier,
    [Parameter(Mandatory = $true)] [string] $OutputDirectory,
    [string] $BasePackagePath
)

$ErrorActionPreference = 'Stop'
$applicationId = 'DcsTranslationTool'
$applicationName = 'DCS-Translation-Tool'
$manifestName = 'update-manifest.json'
$entryPoint = 'DCS-Translation-Tool.exe'
$stage = [IO.Path]::GetFullPath($StageDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$targetVersionValue = $null
if (-not [Version]::TryParse($TargetVersion, [ref] $targetVersionValue)) {
    throw "target version is invalid: $TargetVersion"
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
$targetDeltaPattern = "$applicationName-*-to-$TargetVersion-$RuntimeIdentifier.delta.zip*"
Get-ChildItem -LiteralPath $output -File -Filter $targetDeltaPattern -ErrorAction SilentlyContinue |
    Remove-Item -Force

<#
.SYNOPSIS
ルートディレクトリからの相対パスを取得する。

.PARAMETER Root
相対パスの基準ディレクトリを指定する。

.PARAMETER Path
相対化するファイルまたはディレクトリのパスを指定する。

.OUTPUTS
スラッシュ区切りへ正規化した相対パスを返す。
#>
function Get-RelativePath([string] $Root, [string] $Path) {
    return [IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
}

<#
.SYNOPSIS
更新マニフェストで管理するファイル情報を取得する。

.PARAMETER Root
管理対象ファイルを列挙する配布ルートを指定する。

.OUTPUTS
相対パス、サイズ、SHA-256を持つ管理対象ファイル情報を返す。
#>
function Get-ManagedFiles([string] $Root) {
    return @(
        Get-ChildItem -LiteralPath $Root -File -Recurse |
            ForEach-Object {
                $relative = Get-RelativePath $Root $_.FullName
                if ($relative -ine $manifestName -and
                    $relative -notmatch '^(?i:Logs|TranslateFile|\.dcs-translation-cache)(/|$)') {
                    [ordered]@{
                        path = $relative
                        size = $_.Length
                        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                    }
                }
            } |
            Sort-Object path
    )
}

<#
.SYNOPSIS
値をUTF-8のJSONファイルとして保存する。

.PARAMETER Value
JSONへシリアライズする値を指定する。

.PARAMETER Path
JSONの保存先パスを指定する。

.OUTPUTS
なし。
#>
function Write-JsonUtf8([object] $Value, [string] $Path) {
    $json = $Value | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($Path, $json + "`r`n", [Text.UTF8Encoding]::new($false))
}

<#
.SYNOPSIS
ファイルのSHA-256 sidecarを生成する。

.PARAMETER Path
SHA-256を計算する更新アセットのパスを指定する。

.OUTPUTS
なし。対象ファイルと同じ場所へ.sha256ファイルを保存する。
#>
function Write-Sidecar([string] $Path) {
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$Path.sha256", "$hash  $([IO.Path]::GetFileName($Path))`r`n", [Text.UTF8Encoding]::new($false))
}

if (-not (Test-Path -LiteralPath (Join-Path $stage $entryPoint) -PathType Leaf)) {
    throw "entry point not found: $entryPoint"
}

$targetManifest = [ordered]@{
    schemaVersion = 1
    applicationId = $applicationId
    version = $TargetVersion
    runtimeIdentifier = $RuntimeIdentifier
    entryPoint = $entryPoint
    files = @(Get-ManagedFiles $stage)
}
Write-JsonUtf8 $targetManifest (Join-Path $stage $manifestName)

$fullName = "$applicationName-$TargetVersion-$RuntimeIdentifier.zip"
$fullPath = Join-Path $output $fullName
if (Test-Path -LiteralPath $fullPath) { Remove-Item -LiteralPath $fullPath -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $fullPath -CompressionLevel Optimal
& (Join-Path $PSScriptRoot 'Test-ApplicationUpdatePackage.ps1') `
    -PackagePath $fullPath `
    -ExpectedVersion $TargetVersion `
    -RuntimeIdentifier $RuntimeIdentifier
Write-Sidecar $fullPath

if ([string]::IsNullOrWhiteSpace($BasePackagePath) -or -not (Test-Path -LiteralPath $BasePackagePath -PathType Leaf)) {
    Write-Host 'Compatible base package was not supplied; full assets only.'
    return
}

$workRoot = Join-Path ([IO.Path]::GetTempPath()) "dcs-update-assets-$([Guid]::NewGuid().ToString('N'))"
try {
    $baseRoot = Join-Path $workRoot 'base'
    $deltaRoot = Join-Path $workRoot 'delta'
    $rebuiltRoot = Join-Path $workRoot 'rebuilt'
    Expand-Archive -LiteralPath $BasePackagePath -DestinationPath $baseRoot
    $baseManifestPath = Join-Path $baseRoot $manifestName
    if (-not (Test-Path -LiteralPath $baseManifestPath -PathType Leaf)) {
        Write-Host 'Base package has no update manifest; delta is omitted.'
        return
    }

    $baseManifest = Get-Content -LiteralPath $baseManifestPath -Raw | ConvertFrom-Json
    if ($baseManifest.schemaVersion -ne 1 -or
        $baseManifest.applicationId -cne $applicationId -or
        $baseManifest.runtimeIdentifier -cne $RuntimeIdentifier -or
        $baseManifest.entryPoint -cne $entryPoint) {
        Write-Host 'Base manifest is incompatible; delta is omitted.'
        return
    }

    $baseVersionValue = $null
    if (-not [Version]::TryParse([string] $baseManifest.version, [ref] $baseVersionValue)) {
        Write-Host 'Base manifest version is invalid; delta is omitted.'
        return
    }
    if ($baseVersionValue.Major -ne $targetVersionValue.Major) {
        Write-Host "Base and target major versions differ; delta is omitted. Base=$($baseManifest.version), Target=$TargetVersion"
        return
    }

    $baseByPath = @{}
    foreach ($file in $baseManifest.files) { $baseByPath[$file.path] = $file }
    $targetByPath = @{}
    foreach ($file in $targetManifest.files) { $targetByPath[$file.path] = $file }
    $changed = @($targetManifest.files | Where-Object {
        -not $baseByPath.ContainsKey($_.path) -or $baseByPath[$_.path].sha256 -cne $_.sha256 -or $baseByPath[$_.path].size -ne $_.size
    } | ForEach-Object { $_.path } | Sort-Object)
    $removed = @($baseManifest.files | Where-Object { -not $targetByPath.ContainsKey($_.path) } | ForEach-Object { $_.path } | Sort-Object)

    New-Item -ItemType Directory -Force -Path (Join-Path $deltaRoot 'payload') | Out-Null
    foreach ($relative in $changed) {
        $source = Join-Path $stage $relative
        $destination = Join-Path (Join-Path $deltaRoot 'payload') $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
    $deltaManifest = [ordered]@{
        schemaVersion = 1
        applicationId = $applicationId
        baseVersion = $baseManifest.version
        targetVersion = $TargetVersion
        runtimeIdentifier = $RuntimeIdentifier
        addedOrChangedFiles = $changed
        removedFiles = $removed
        targetManifest = $targetManifest
    }
    Write-JsonUtf8 $deltaManifest (Join-Path $deltaRoot 'delta-manifest.json')

    foreach ($file in $baseManifest.files) {
        $source = Join-Path $baseRoot $file.path
        $destination = Join-Path $rebuiltRoot $file.path
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
    foreach ($relative in $removed) {
        $path = Join-Path $rebuiltRoot $relative
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    foreach ($relative in $changed) {
        $source = Join-Path (Join-Path $deltaRoot 'payload') $relative
        $destination = Join-Path $rebuiltRoot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
    Write-JsonUtf8 $targetManifest (Join-Path $rebuiltRoot $manifestName)
    $rebuiltFiles = @(Get-ManagedFiles $rebuiltRoot)
    if (($rebuiltFiles | ConvertTo-Json -Depth 5 -Compress) -cne ($targetManifest.files | ConvertTo-Json -Depth 5 -Compress)) {
        throw 'Delta reconstruction does not match the target manifest.'
    }

    $deltaName = "$applicationName-$($baseManifest.version)-to-$TargetVersion-$RuntimeIdentifier.delta.zip"
    $deltaPath = Join-Path $output $deltaName
    Compress-Archive -Path (Join-Path $deltaRoot '*') -DestinationPath $deltaPath -CompressionLevel Optimal
    Write-Sidecar $deltaPath
}
finally {
    if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
}