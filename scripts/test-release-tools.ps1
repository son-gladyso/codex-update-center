[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $project ('artifacts/release-tools-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $fixture 'payload'
[IO.Directory]::CreateDirectory($payload) | Out-Null
$passed = [Collections.Generic.List[string]]::new()
function Expect-Rejected([string]$Name, [scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Expected rejection: $Name" }
    $passed.Add($Name)
}
# Synthetic files test packaging boundaries, never executable behavior.
foreach ($name in @('CodexUpdateCenter.exe','CodexUpdateCenter.dll','UpdateCenter.Core.dll','coreclr.dll','PresentationFramework.dll','LICENSE','NOTICE.md')) {
    [IO.File]::WriteAllText((Join-Path $payload $name), 'synthetic release fixture')
}
$verify = Join-Path $PSScriptRoot 'verify-release.ps1'
Expect-Rejected 'missing-manifest' { & $verify -Directory $payload }
& $verify -Directory $payload -ContentOnly | Out-Null
$passed.Add('explicit-content-only')
$entries = @(Get-ChildItem -LiteralPath $payload -File -Force | ForEach-Object {
    @{ Path = $_.Name; Bytes = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
})
$manifest = Join-Path $payload 'manifest.json'
$entries | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding utf8
& $verify -Directory $payload | Out-Null
$passed.Add('valid-manifest')
$notice = Join-Path $payload 'NOTICE.md'
[IO.File]::AppendAllText($notice, 'changed')
Expect-Rejected 'modified-content' { & $verify -Directory $payload }
[IO.File]::WriteAllText($notice, 'synthetic release fixture')
$hidden = Join-Path $payload 'hidden.dll'
[IO.File]::WriteAllText($hidden, 'unlisted')
[IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Hidden)
Expect-Rejected 'unlisted-hidden-file' { & $verify -Directory $payload }
[IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Normal)
[IO.File]::Delete($hidden)
@{ Path = '../outside.dll'; Bytes = 0; SHA256 = 'invalid' } | ConvertTo-Json | Set-Content -LiteralPath $manifest
Expect-Rejected 'manifest-path-escape' { & $verify -Directory $payload }
$entries | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding utf8
& $verify -Directory $payload | Out-Null

# Audit a standalone copy of the exact source inventory. Generated private markers
# stay only in ignored fixtures; no real credentials are accessed.
$source = Join-Path $fixture 'source'
$inventory = @(& (Join-Path $PSScriptRoot 'audit-source.ps1') -PassThru)
foreach ($file in $inventory) {
    $destination = Join-Path $source ([IO.Path]::GetRelativePath($project, $file.FullName))
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    [IO.File]::Copy($file.FullName, $destination)
}
$audit = Join-Path $source 'scripts/audit-source.ps1'
& $audit | Out-Null
$passed.Add('standalone-source-audit')
$injected = Join-Path $source 'docs/injected.md'
[IO.File]::WriteAllText($injected, ('ghp_' + ('x' * 40)))
Expect-Rejected 'credential-marker' { & $audit }
[IO.File]::WriteAllText($injected, ('C:' + '\Users\' + 'ExamplePerson\private.txt'))
Expect-Rejected 'personal-path' { & $audit }
[IO.File]::Delete($injected)
$sourceZip = Join-Path $fixture 'source.zip'
& (Join-Path $source 'scripts/package-source.ps1') -Output $sourceZip | Out-Null
$zip = [IO.Compression.ZipFile]::OpenRead($sourceZip)
try {
    $names = @($zip.Entries.FullName)
    if ('.github/workflows/build.yml' -notin $names -or '.gitignore' -notin $names) { throw 'Missing hidden source entries.' }
    if (@($names | Where-Object { $_ -match '(^|/)(artifacts|bin|obj)/' }).Count) { throw 'Source archive contains generated files.' }
    if ($names.Count -ne $inventory.Count) { throw 'Source archive inventory differs.' }
} finally { $zip.Dispose() }
$passed.Add('source-archive-inventory')
@{ Passed = $true; Checks = @($passed); Scope = 'Synthetic release and source boundary checks; no native installation.' } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'report.json') -Encoding utf8
Write-Output "Release tools: $($passed.Count) checks passed. Report: $fixture/report.json"
