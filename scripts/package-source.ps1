param([string]$Output)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Output) {
    $version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
    $Output = "artifacts/CodexUpdateCenter-$version-source.zip"
}
$destination = [IO.Path]::GetFullPath($Output,$root)
if(Test-Path -LiteralPath $destination) { throw 'Source archive must not exist.' }
$files = @(& (Join-Path $PSScriptRoot 'audit-source.ps1') -PassThru)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
$archive = [IO.Compression.ZipFile]::Open($destination,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($file in $files | Sort-Object FullName -Unique) {
        $relative = [IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Output "Source archive created: $destination"
