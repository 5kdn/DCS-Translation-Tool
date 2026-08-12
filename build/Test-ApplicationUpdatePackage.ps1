<#
.SYNOPSIS
アプリケーション更新用の完全版ZIPを検証する。

.DESCRIPTION
ZIP内の安全で一意な相対パス、更新マニフェストの識別情報、管理対象ファイルの集合、
ファイルサイズ、SHA-256、エントリポイント、およびlib配下の実行用レイアウトを検証する。
検証に失敗した場合はエラーを送出し、CIでのリリース公開を中止する。

.PARAMETER PackagePath
検証する完全版ZIPのパスを指定する。

.PARAMETER ExpectedVersion
更新マニフェストに期待する配布バージョンを指定する。

.PARAMETER RuntimeIdentifier
更新マニフェストに期待するRIDをwin-x64またはwin-x86で指定する。

.OUTPUTS
なし。検証に失敗した場合はエラーを送出する。

.EXAMPLE
./build/Test-ApplicationUpdatePackage.ps1 -PackagePath ./assets/win-x64/DCS-Translation-Tool-1.4.0-win-x64.zip -ExpectedVersion 1.4.0 -RuntimeIdentifier win-x64

win-x64向け完全版ZIPの内容と更新マニフェストを検証する。
#>
param(
    [Parameter(Mandatory = $true)] [string] $PackagePath,
    [Parameter(Mandatory = $true)] [string] $ExpectedVersion,
    [Parameter(Mandatory = $true)] [ValidateSet('win-x64', 'win-x86')] [string] $RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'
$applicationId = 'DcsTranslationTool'
$manifestName = 'update-manifest.json'
$entryPoint = 'DCS-Translation-Tool.exe'
$package = [IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $package -PathType Leaf)) {
    throw "package not found: $package"
}

<#
.SYNOPSIS
ZIP内の相対パスを検証して正規化する。

.PARAMETER Path
検証するZIPエントリの相対パスを指定する。

.OUTPUTS
スラッシュ区切りへ正規化した安全な相対パスを返す。
#>
function Get-NormalizedRelativePath([string] $Path) {
    $normalized = $Path.Replace('\', '/').TrimEnd('/')
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        [IO.Path]::IsPathRooted($Path) -or
        $normalized.StartsWith('/') -or
        ($normalized.Split('/') | Where-Object { $_ -in @('.', '..') -or $_.Contains(':') }).Count -ne 0) {
        throw "unsafe package path: $Path"
    }
    return $normalized
}

<#
.SYNOPSIS
大文字と小文字を区別しないパス重複がないことを検証する。

.PARAMETER Paths
確認済みパスを保持する集合を指定する。

.PARAMETER Path
新たに追加して一意性を検証する相対パスを指定する。

.OUTPUTS
なし。重複または大文字小文字の衝突がある場合はエラーを送出する。
#>
function Assert-UniquePath([Collections.Generic.HashSet[string]] $Paths, [string] $Path) {
    if (-not $Paths.Add($Path)) {
        throw "duplicate or case-colliding package path: $Path"
    }
}

$workRoot = Join-Path ([IO.Path]::GetTempPath()) "dcs-update-package-test-$([Guid]::NewGuid().ToString('N'))"
try {
    $archivePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $relative = Get-NormalizedRelativePath $entry.FullName
            Assert-UniquePath $archivePaths $relative
        }
    }
    finally {
        $archive.Dispose()
    }

    Expand-Archive -LiteralPath $package -DestinationPath $workRoot
    $manifestPath = Join-Path $workRoot $manifestName
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "update manifest is missing from '$([IO.Path]::GetFileName($package))'."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or
        $manifest.applicationId -cne $applicationId -or
        $manifest.version -cne $ExpectedVersion -or
        $manifest.runtimeIdentifier -cne $RuntimeIdentifier -or
        $manifest.entryPoint -cne $entryPoint) {
        throw "invalid update manifest in '$([IO.Path]::GetFileName($package))'."
    }

    $managedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $managedCollisionPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest.files) {
        $relative = Get-NormalizedRelativePath ([string] $file.path)
        Assert-UniquePath $managedCollisionPaths $relative
        $managedPaths.Add($relative) | Out-Null
        if ([long] $file.size -lt 0 -or ([string] $file.sha256) -notmatch '^[A-Fa-f0-9]{64}$') {
            throw "invalid manifest metadata for '$relative'."
        }

        $fullPath = [IO.Path]::GetFullPath((Join-Path $workRoot $relative))
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "managed file is missing from package: $relative"
        }
        $item = Get-Item -LiteralPath $fullPath
        if ($item.Length -ne [long] $file.size) {
            throw "managed file size mismatch: $relative"
        }
        $actualHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        if ($actualHash -cne ([string] $file.sha256).ToUpperInvariant()) {
            throw "managed file SHA-256 mismatch: $relative"
        }
    }

    if (-not $managedPaths.Contains($entryPoint)) {
        throw "entry point is not listed as a managed file: $entryPoint"
    }

    $requiredRuntimePaths = @(
        'lib/DcsTranslationTool.Presentation.Wpf.dll',
        'lib/DcsTranslationTool.Presentation.Wpf.deps.json',
        'lib/DcsTranslationTool.Presentation.Wpf.runtimeconfig.json',
        'lib/coreclr.dll',
        'lib/hostfxr.dll'
    )
    $missingRuntimePaths = @($requiredRuntimePaths | Where-Object { -not $managedPaths.Contains($_) })
    if ($missingRuntimePaths.Count -ne 0) {
        throw "required runtime files are missing from lib. Missing=[$($missingRuntimePaths -join ', ')]"
    }

    $rootRuntimePaths = @($managedPaths | Where-Object {
        -not $_.Contains('/') -and
        ($_.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase) -or
            $_.EndsWith('.deps.json', [StringComparison]::OrdinalIgnoreCase) -or
            $_.EndsWith('.runtimeconfig.json', [StringComparison]::OrdinalIgnoreCase) -or
            $_ -ieq 'createdump.exe')
    })
    if ($rootRuntimePaths.Count -ne 0) {
        throw "runtime files must be placed under lib. Root=[$($rootRuntimePaths -join ', ')]"
    }

    if (-not ($managedPaths | Where-Object { $_.StartsWith('lib/ja/', [StringComparison]::Ordinal) })) {
        throw 'Japanese satellite resources are missing from lib/ja.'
    }

    $actualPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    Get-ChildItem -LiteralPath $workRoot -File -Recurse | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($workRoot, $_.FullName).Replace('\', '/')
        if ($relative -cne $manifestName) {
            Assert-UniquePath $actualPaths $relative
        }
    }

    $missing = @($managedPaths | Where-Object { -not $actualPaths.Contains($_) })
    $unexpected = @($actualPaths | Where-Object { -not $managedPaths.Contains($_) })
    if ($missing.Count -ne 0 -or $unexpected.Count -ne 0) {
        throw "package file set does not match manifest. Missing=[$($missing -join ', ')] Unexpected=[$($unexpected -join ', ')]"
    }
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
