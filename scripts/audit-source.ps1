param([switch]$PassThru)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$top = @('.editorconfig','.gitattributes','.gitignore','global.json','Directory.Build.props','CodexUpdateCenter.slnx','README.md','LICENSE','NOTICE.md','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md')
$files = [Collections.Generic.List[IO.FileInfo]]::new()
foreach ($name in $top) { $files.Add((Get-Item -LiteralPath (Join-Path $root $name))) }
$pending = [Collections.Generic.Queue[string]]::new()
foreach ($name in @('.github','docs','scripts','src','tests')) { $pending.Enqueue((Join-Path $root $name)) }
while ($pending.Count -gt 0) {
    $directory = Get-Item -LiteralPath $pending.Dequeue() -Force
    if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked source directory: $($directory.Name)" }
    foreach ($entry in Get-ChildItem -LiteralPath $directory.FullName -Force) {
        if ($entry.PSIsContainer -and $entry.Name -in @('bin','obj','artifacts','TestResults','node_modules')) { continue }
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked source entry: $($entry.Name)" }
        if ($entry.PSIsContainer) { $pending.Enqueue($entry.FullName) } else { $files.Add($entry) }
    }
}
$allowed = @('.cs','.csproj','.xaml','.manifest','.json','.ps1','.md','.yml','.yaml')
foreach ($file in $files) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked source file.' }
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\','/')
    if ($file.Extension -eq '.png' -and $relative.StartsWith('docs/images/', [StringComparison]::Ordinal)) {
        if ($file.Length -gt 2MB -or $file.Length -lt 8) { throw 'Documentation image size is invalid.' }
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        if ([Convert]::ToHexString($bytes[0..7]) -ne '89504E470D0A1A0A') { throw 'Documentation image is not PNG.' }
        continue # Images are manually reviewed for personal content before staging.
    }
    if ($file.Name -notin $top -and $file.Extension -notin $allowed) { throw "Unexpected source file: $($file.Name)" }
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match '(?i)(BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|sk-[A-Za-z0-9_-]{32,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|[A-Z]:[\\/]+Users[\\/]+[^\\/\s]+[\\/]|[A-Z]:[\\/]+codex\d+[\\/])') { throw "Private source content: $($file.Name)" }
}
if ($PassThru) { $files | Sort-Object FullName -Unique } else { Write-Output "Source checks passed ($($files.Count) files)." }
