[CmdletBinding()]
param(
    [switch]$Test,
    [string]$CodeWalkerPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts'
$project = Join-Path $repoRoot 'src/YtypEditor/YtypEditor.csproj'
$dependencyCommit = '485d56bec00262ed7fa472261cce7bbc6202b96e'

function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed (exit $LASTEXITCODE)." }
}

if (!$CodeWalkerPath) {
    $CodeWalkerPath = Join-Path $repoRoot 'vendor/CodeWalker'
    if (!(Test-Path -LiteralPath $CodeWalkerPath)) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $CodeWalkerPath) -Force | Out-Null
        Invoke-Checked git @('clone', '--no-checkout', '--depth', '1', 'https://github.com/dexyfex/CodeWalker.git', $CodeWalkerPath)
        Invoke-Checked git @('-C', $CodeWalkerPath, 'fetch', '--depth', '1', 'origin', $dependencyCommit)
        Invoke-Checked git @('-C', $CodeWalkerPath, 'checkout', '--detach', $dependencyCommit)
    }
}
$CodeWalkerPath = (Resolve-Path -LiteralPath $CodeWalkerPath).Path
$dependencyProject = Join-Path $CodeWalkerPath 'CodeWalker.Core/CodeWalker.Core.csproj'
if (!(Test-Path -LiteralPath $dependencyProject)) { throw 'CodeWalker.Core.csproj was not found.' }
$gitSafePath = $CodeWalkerPath.Replace('\', '/')
$gitOptions = @('-c', "safe.directory=$gitSafePath", '-C', $CodeWalkerPath)
$actualCommit = & git @gitOptions rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $dependencyCommit) {
    throw "CodeWalker must be checked out at $dependencyCommit. Existing source was left unchanged."
}

# Only these generated staging directories are replaced; source and existing ZIPs are retained.
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$publishDir = Join-Path $artifacts 'publish'
$releaseDir = Join-Path $artifacts 'release'
$artifactsPrefix = (Resolve-Path -LiteralPath $artifacts).Path.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
foreach ($stagingDir in @($publishDir, $releaseDir)) {
    if (Test-Path -LiteralPath $stagingDir) {
        $resolved = (Resolve-Path -LiteralPath $stagingDir).Path
        if (!(($resolved + [IO.Path]::DirectorySeparatorChar).StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase))) {
            throw "Refusing to clean outside artifacts: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

Invoke-Checked dotnet @(
    'publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-o', $publishDir, '--nologo', '-v', 'minimal',
    "-p:CodeWalkerProjectPath=$dependencyProject", '-p:NuGetAudit=false',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false'
)

$exe = Join-Path $publishDir 'YtypEditor.exe'
if ($Test) {
    $testProcess = Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
    if ($testProcess.ExitCode -ne 0) {
        $resultsFile = Join-Path $publishDir 'test-results.txt'
        if (Test-Path -LiteralPath $resultsFile) { Get-Content -LiteralPath $resultsFile | Write-Host }
        throw "Self-tests failed (exit $($testProcess.ExitCode))."
    }
    Get-Content -LiteralPath (Join-Path $publishDir 'test-results.txt') | Write-Host
}

$appDir = Join-Path $releaseDir 'YtypEditor'
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination $appDir
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/USER_GUIDE.md') -Destination (Join-Path $appDir 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses') -Destination $appDir -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $appDir 'licenses/Editor-MIT.txt')

[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = $projectXml.Project.PropertyGroup.Version
$zip = Join-Path $artifacts "gta-v-ytyp-editor-$version-win-x64.zip"
Compress-Archive -LiteralPath $appDir -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $([IO.Path]::GetFileName($zip))" -Encoding ascii
Write-Host "Release prepared: $zip"
