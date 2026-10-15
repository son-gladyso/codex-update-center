param([Parameter(Mandatory)][string]$Directory, [switch]$ContentOnly)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Directory).Path
$allowedRoot = @('.exe','.dll','.json','.md','.txt','.config')
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Force) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked release entry: $($file.Name)" }
    $relative = [IO.Path]::GetRelativePath($root,$file.FullName)
    if (-not $file.PSIsContainer -and $file.Extension -eq '.png' -and $relative.Replace('\','/').StartsWith('docs/images/', [StringComparison]::Ordinal)) {
        if ($file.Length -gt 2MB -or $file.Length -lt 8) { throw 'Invalid documentation image size.' }
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        if ([Convert]::ToHexString($bytes[0..7]) -ne '89504E470D0A1A0A') { throw 'Invalid documentation image.' }
        continue # Still included in manifest/hash verification below.
    }
    if ($relative -match '(?i)(^|[\\/])(data|cache|logs|snapshots|node_modules|\.git|\.codex|artifacts)([\\/]|$)' -or $file.Name -match '(?i)(auth\.json|\.pfx$|\.p12$|\.pem$|\.key$|\.sqlite|\.pdb$|^\.env$)') { throw "Private or development artifact: $relative" }
    if (-not $file.PSIsContainer -and $file.Extension -notin $allowedRoot -and $file.Name -ne 'LICENSE') { throw "Unexpected release file: $relative" }
    if (-not $file.PSIsContainer -and $file.Extension -in @('.md','.json','.txt','.config')) {
        $content = Get-Content -LiteralPath $file.FullName -Raw
        if ($content -match '(?i)([A-Z]:[\\/]+Users[\\/]+[^\\/\s]+[\\/]|[A-Z]:[\\/]+codex\d+[\\/]|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|sk-[A-Za-z0-9_-]{32,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})') { throw "Private content: $relative" }
    }
}
foreach ($required in @('CodexUpdateCenter.exe','CodexUpdateCenter.dll','UpdateCenter.Core.dll','coreclr.dll','PresentationFramework.dll','LICENSE','NOTICE.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $required))) { throw "Missing release file: $required" }
}
$manifestPath = Join-Path $root 'manifest.json'
if (-not $ContentOnly -and -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Missing release manifest.' }
if (Test-Path -LiteralPath $manifestPath) {
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)) {
        if ([IO.Path]::IsPathRooted($entry.Path) -or $entry.Path.Contains(':') -or ($entry.Path -split '[/\\]') -contains '..') { throw 'Unsafe manifest path.' }
        $file = [IO.Path]::GetFullPath((Join-Path $root $entry.Path))
        if (-not $file.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or -not $seen.Add($file)) { throw 'Invalid manifest inventory.' }
        if ((Get-Item -LiteralPath $file).Length -ne $entry.Bytes -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "File integrity mismatch: $($entry.Path)" }
    }
    $actual = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object FullName -NE $manifestPath)
    if ($seen.Count -ne $actual.Count) { throw 'Unlisted release files.' }
}
Write-Output 'Release content checks passed.'
