[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$webRoot = Join-Path $repoRoot 'src\ModScope.Web'
$solutionPath = Join-Path $repoRoot 'ModScope.sln'

$nodeCommand = Get-Command node -ErrorAction Stop
$npmCommand = Get-Command npm -ErrorAction Stop
$dotnetCommand = Get-Command dotnet -ErrorAction Stop

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$CommandPath,
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [Parameter(Mandatory)]
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

Write-Host 'Installing frontend dependencies...'
Invoke-NativeCommand $npmCommand.Source @('ci') $webRoot

Write-Host 'Checking frontend...'
Invoke-NativeCommand $npmCommand.Source @('run', 'check') $webRoot

Write-Host 'Building frontend...'
Invoke-NativeCommand $npmCommand.Source @('run', 'build') $webRoot

Write-Host 'Building ModScope...'
Invoke-NativeCommand $dotnetCommand.Source @('build', $solutionPath, '--nologo', '--no-restore') $repoRoot

Write-Host 'ModScope Web UI build completed.'
