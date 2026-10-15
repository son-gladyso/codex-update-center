[CmdletBinding()]
param([string]$Output = 'artifacts/release-local')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = Split-Path $PSScriptRoot -Parent
Set-Location $project
$version = ([xml](Get-Content (Join-Path $project 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+-preview(?:\.\d+)?$') { throw 'A preview version is required until the deployment matrix passes.' }
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") { throw 'Release tag does not match Directory.Build.props.' }
$destination = [IO.Path]::GetFullPath($Output, $project)
if (Test-Path -LiteralPath $destination) { throw 'Release output must be a new directory.' }
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Build on Windows x64 with PowerShell 7.' }
& (Join-Path $PSScriptRoot 'audit-source.ps1')
& (Join-Path $PSScriptRoot 'test-release-tools.ps1')
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE)." }
}
Invoke-Dotnet -Arguments @('restore', 'CodexUpdateCenter.slnx', '--locked-mode', '--disable-parallel')
Invoke-Dotnet -Arguments @('test', 'CodexUpdateCenter.slnx', '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '--blame-hang-timeout', '3m', '--logger', 'trx', '--results-directory', 'artifacts/test-results')
$publish = Join-Path $destination 'app'
Invoke-Dotnet -Arguments @('publish', 'src/UpdateCenter.App', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '--disable-build-servers', '-m:1', '-p:ContinuousIntegrationBuild=true', '-p:DebugType=none', '-o', $publish)
Copy-Item -LiteralPath (Join-Path $project 'README.md'),(Join-Path $project 'LICENSE'),(Join-Path $project 'NOTICE.md'),(Join-Path $project 'CHANGELOG.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $project 'docs') -Destination (Join-Path $publish 'docs') -Recurse
$assets = Get-Content 'src/UpdateCenter.App/obj/project.assets.json' -Raw | ConvertFrom-Json -AsHashtable
$packRoot = @($assets.packageFolders.Keys)[0]
foreach ($runtime in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')) {
    $library = @($assets.project.frameworks.Values.downloadDependencies | Where-Object { $_.name -eq $runtime })
    if ($library.Count -ne 1 -or $library[0].version -notmatch '^\[([^,]+), \1\]$') { throw "Cannot identify pinned runtime: $runtime" }
    $folder = Join-Path $packRoot "$runtime/$($Matches[1])"
    $notices = if ($runtime -eq 'microsoft.windowsdesktop.app.runtime.win-x64') { @('LICENSE') } else { @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT') }
    foreach ($notice in $notices) {
        $source = Join-Path $folder $notice
        if (-not (Test-Path -LiteralPath $source)) { throw "Missing runtime notice: $notice" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $publish "$runtime-$notice.txt")
    }
}
& (Join-Path $PSScriptRoot 'verify-release.ps1') -Directory $publish -ContentOnly
$files = @(Get-ChildItem -LiteralPath $publish -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
    [ordered]@{ Path = [IO.Path]::GetRelativePath($publish,$_.FullName).Replace('\','/'); Bytes = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$files | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $publish 'manifest.json') -Encoding utf8
& (Join-Path $PSScriptRoot 'verify-release.ps1') -Directory $publish
$zip = Join-Path $destination "CodexUpdateCenter-$version-win-x64.zip"
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $publish -File -Recurse -Force | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($publish, $file.FullName).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$sourceZip = Join-Path $destination "CodexUpdateCenter-$version-source.zip"
& (Join-Path $PSScriptRoot 'package-source.ps1') -Output $sourceZip
@($zip,$sourceZip) | ForEach-Object { "$((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash)  $([IO.Path]::GetFileName($_))" } | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Release created: $zip"
