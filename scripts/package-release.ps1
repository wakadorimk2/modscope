[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$ReleaseVersion,

    [string]$OutputDirectory = 'artifacts\release',

    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [switch]$Force,

    [switch]$AllowDirty,

    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repoRoot 'ModScope.sln'
$desktopProjectPath = Join-Path $repoRoot 'src\ModScope.Desktop\ModScope.Desktop.csproj'
$buildScriptPath = Join-Path $repoRoot 'scripts\build.ps1'
$licensePath = Join-Path $repoRoot 'LICENSE'

if (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$packageName = "ModScope-$ReleaseVersion-windows-x64"
$stagingRoot = Join-Path $outputRoot ".staging-$packageName"
$packageRoot = Join-Path $stagingRoot $packageName
$archivePath = Join-Path $outputRoot "$packageName.zip"
$manifestPath = Join-Path $outputRoot "$packageName.manifest.json"

function Assert-NoReparsePoints {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $items = @(
        Get-Item -LiteralPath $Path -Force
        Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    )

    foreach ($item in $items) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Reparse points are not allowed in the package path: $($item.FullName)"
        }
    }
}

function Remove-SafePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($item.PSIsContainer) {
        Assert-NoReparsePoints -Path $Path
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        return
    }

    Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandPath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $CommandPath @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "$CommandPath failed with exit code $exitCode."
    }
}

function Get-NativeCommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandPath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    Push-Location -LiteralPath $WorkingDirectory
    try {
        $output = @(& $CommandPath @Arguments)
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "$CommandPath failed with exit code $exitCode."
    }

    return (($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine).Trim()
}

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Solution was not found: $solutionPath"
}

if (-not (Test-Path -LiteralPath $desktopProjectPath -PathType Leaf)) {
    throw "Desktop project was not found: $desktopProjectPath"
}

if (-not (Test-Path -LiteralPath $buildScriptPath -PathType Leaf)) {
    throw "Build script was not found: $buildScriptPath"
}

if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    throw "License file was not found: $licensePath"
}

$outputRootParent = [IO.Directory]::GetParent($outputRoot)
if ([string]::IsNullOrWhiteSpace($outputRootParent)) {
    throw "Output directory must not be a filesystem root: $outputRoot"
}

if (Test-Path -LiteralPath $outputRoot -PathType Leaf) {
    throw "Output directory is a file: $outputRoot"
}

if (Test-Path -LiteralPath $outputRoot) {
    Assert-NoReparsePoints -Path $outputRoot
}
else {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
}

$replaceTargets = @($stagingRoot, $archivePath, $manifestPath)
foreach ($target in $replaceTargets) {
    if (Test-Path -LiteralPath $target) {
        if (-not $Force) {
            throw "Output already exists. Use -Force only to replace this exact generated target: $target"
        }

        Remove-SafePath -Path $target
    }
}

$gitCommand = (Get-Command git -ErrorAction Stop).Source
$dotnetCommand = (Get-Command dotnet -ErrorAction Stop).Source

$sourceCommit = Get-NativeCommandOutput -CommandPath $gitCommand -Arguments @('rev-parse', 'HEAD') -WorkingDirectory $repoRoot
if ($sourceCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Could not resolve a commit SHA for the package source: $sourceCommit"
}

$dirtyOutput = Get-NativeCommandOutput -CommandPath $gitCommand -Arguments @('status', '--porcelain') -WorkingDirectory $repoRoot
$workingTreeDirty = -not [string]::IsNullOrWhiteSpace($dirtyOutput)
if ($workingTreeDirty -and -not $AllowDirty) {
    throw 'The working tree is dirty. Commit or stash changes, or pass -AllowDirty for a diagnostic package.'
}

$stagingCreated = $false
try {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    $stagingCreated = $true

    Write-Host "Restoring .NET dependencies..."
    Invoke-NativeCommand -CommandPath $dotnetCommand -Arguments @('restore', $solutionPath, '--runtime', 'win-x64', '--nologo') -WorkingDirectory $repoRoot

    Write-Host "Building the frontend and solution..."
    & $buildScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "$buildScriptPath failed with exit code $LASTEXITCODE."
    }

    Write-Host "Publishing Windows x64 application..."
    Invoke-NativeCommand -CommandPath $dotnetCommand -Arguments @(
        'publish',
        $desktopProjectPath,
        '--configuration', $Configuration,
        '--runtime', 'win-x64',
        '--self-contained', 'false',
        '--output', $packageRoot,
        '--nologo',
        '--no-restore'
    ) -WorkingDirectory $repoRoot

    $pdbFiles = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.pdb' -ErrorAction Stop)
    foreach ($pdbFile in $pdbFiles) {
        Remove-Item -LiteralPath $pdbFile.FullName -Force -ErrorAction Stop
    }

    $forbiddenNames = @(
        'ModScope.Desktop.exe.WebView2',
        'EBWebView',
        'Cache',
        'Cookies',
        'Crashpad',
        'DIPS',
        'History',
        'Local State',
        'Network',
        'Preferences',
        'Session Storage',
        'Top Sites'
    )

    $packageEntries = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -Force -ErrorAction Stop)
    foreach ($entry in $packageEntries) {
        if ($forbiddenNames -contains $entry.Name) {
            throw "Browser or user-state data was found in the package: $($entry.FullName)"
        }
    }

    Copy-Item -LiteralPath $licensePath -Destination (Join-Path $packageRoot 'LICENSE') -Force

    $packageReadme = @"
ModScope Early Access
=====================

ModScope is a browse-first Windows workspace for discovering, organizing, and understanding 7 Days to Die mods managed by Mod Organizer 2.

This package is an Early Access build.

Requirements
------------
- Windows x64
- .NET 10 Desktop Runtime
- Microsoft Edge WebView2 Runtime
- A user-selected Mod Organizer 2 source for local Mod Knowledge features

Installation
------------
1. Extract this archive to a writable local directory.
2. Install the requirements listed above.
3. Start ModScope.Desktop.exe.
4. Select an explicitly approved MO2 source when the application requests one.

Boundaries
----------
- ModScope does not replace Mod Organizer 2 as the source of truth.
- Local Mod Knowledge is derived from the selected local source.
- Web compatibility and version observations are evidence. They are not runtime guarantees.
- This Early Access package may contain incomplete behavior or known limitations.

Build metadata
--------------
- Version: $ReleaseVersion
- Runtime: win-x64
- Source commit: $sourceCommit
- Working tree dirty: $workingTreeDirty

The source, license, limitations, and support links are provided on the Nexus Mods page.
"@

    $readmePath = Join-Path $packageRoot 'README-NEXUS.txt'
    Set-Content -LiteralPath $readmePath -Value $packageReadme -Encoding UTF8

    $fileRecords = @(
        Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Force -ErrorAction Stop |
            Sort-Object FullName |
            ForEach-Object {
                $relativePath = $_.FullName.Substring($packageRoot.Length).TrimStart([char[]]@('\', '/'))
                [ordered]@{
                    path = $relativePath.Replace('\', '/')
                    sizeBytes = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )

    $archiveEntries = @(Get-ChildItem -LiteralPath $packageRoot -Force -ErrorAction Stop | Select-Object -ExpandProperty FullName)
    if ($archiveEntries.Count -eq 0) {
        throw "The package directory is empty: $packageRoot"
    }

    Write-Host "Creating archive..."
    Compress-Archive -Path $archiveEntries -DestinationPath $archivePath -CompressionLevel Optimal -Force

    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Archive was not created: $archivePath"
    }

    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest = [ordered]@{
        schemaVersion = 1
        product = 'ModScope'
        version = $ReleaseVersion
        runtimeIdentifier = 'win-x64'
        configuration = $Configuration
        sourceCommit = $sourceCommit
        workingTreeDirty = $workingTreeDirty
        archive = [ordered]@{
            file = [IO.Path]::GetFileName($archivePath)
            sizeBytes = (Get-Item -LiteralPath $archivePath -Force).Length
            sha256 = $archiveHash
        }
        files = $fileRecords
    }

    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}
finally {
    if ($stagingCreated -and -not $KeepStaging -and (Test-Path -LiteralPath $stagingRoot)) {
        Remove-SafePath -Path $stagingRoot
    }
}

Write-Host "Package: $archivePath"
Write-Host "Manifest: $manifestPath"
