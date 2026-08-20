param(
    [string]$SourceRoot = "src",
    [string]$OutputPath = "artifacts/eac-safety-audit.json"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root $SourceRoot
$forbidden = @(
    'ReadProcessMemory',
    'WriteProcessMemory',
    'CreateRemoteThread',
    'VirtualAllocEx',
    'SetWindowsHookEx',
    'NtWriteVirtualMemory',
    'SharpPcap',
    'PacketDotNet',
    'MemoryMappedFileRights\.(Write|ReadWrite)',
    'CreateViewAccessor\([^\r\n]*MemoryMappedFileAccess\.(Write|ReadWrite)'
)
$files = Get-ChildItem -LiteralPath $source -Recurse -File |
    Where-Object { $_.Extension -in '.cs', '.csproj', '.props', '.targets' }
$findings = foreach ($pattern in $forbidden) {
    $files | Select-String -Pattern $pattern | ForEach-Object {
        [ordered]@{
            pattern = $pattern
            file = $_.Path.Substring($root.Length + 1).Replace('\', '/')
            line = $_.LineNumber
            text = $_.Line.Trim()
        }
    }
}
$readOnlyMap = $files | Select-String -SimpleMatch 'MemoryMappedFileRights.Read'
$synchronizeOnly = $files | Select-String -SimpleMatch 'private const uint Synchronize'
$report = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    passed = ($findings.Count -eq 0 -and $readOnlyMap.Count -gt 0 -and $synchronizeOnly.Count -gt 0)
    guarantees = [ordered]@{
        readOnlyNamedMappingFound = ($readOnlyMap.Count -gt 0)
        synchronizeOnlyEventFound = ($synchronizeOnly.Count -gt 0)
        forbiddenApiFindings = $findings.Count
    }
    findings = @($findings)
}
$destination = Join-Path $root $OutputPath
$directory = Split-Path -Parent $destination
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $destination -Encoding utf8
if (-not $report.passed) {
    $report | ConvertTo-Json -Depth 6 | Write-Error
    exit 1
}
Write-Host "EAC safety audit passed: $destination"
