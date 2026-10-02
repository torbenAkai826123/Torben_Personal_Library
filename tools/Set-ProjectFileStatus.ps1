param(
    [Parameter(Mandatory = $true)] [string]$Path,
    [Parameter(Mandatory = $true)] [ValidateSet('unclassified', 'draft', 'approved', 'withdrawn', 'deprecated')] [string]$Status,
    [string]$Reason = '',
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
$normalizedPath = $Path.Replace('\', '/')
if ([string]::IsNullOrWhiteSpace($normalizedPath) -or $normalizedPath.StartsWith('/') -or $normalizedPath -match '(^|/)\.{1,2}(/|$)') { throw 'Path must be a project-relative indexed path.' }
$indexPath = Join-Path $projectPath 'project_index'
$contentPath = Join-Path $indexPath 'content.json'
$statesPath = Join-Path $indexPath 'states.json'
$historyPath = Join-Path $indexPath 'status-history.jsonl'
if (-not (Test-Path -LiteralPath $contentPath -PathType Leaf)) { throw 'Run Update-ProjectIndex.ps1 before setting file status.' }
$content = Get-Content -LiteralPath $contentPath -Raw -Encoding UTF8 | ConvertFrom-Json
$fileProperty = $content.files.PSObject.Properties | Where-Object { [string]::Equals($_.Name, $normalizedPath, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
if (-not $fileProperty) { throw "Path is not in the current index: $normalizedPath" }

$stateFiles = [ordered]@{}
if (Test-Path -LiteralPath $statesPath -PathType Leaf) {
    $existing = Get-Content -LiteralPath $statesPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in $existing.files.PSObject.Properties) { $stateFiles[$property.Name] = $property.Value }
}
$previousStatus = 'unclassified'
if ($stateFiles.Contains($normalizedPath)) { $previousStatus = [string]$stateFiles[$normalizedPath].status }
$now = [DateTime]::UtcNow.ToString('o')
$stateFiles[$normalizedPath] = [ordered]@{ status = $Status; updatedAt = $now; reason = $Reason }
$stateDocument = [ordered]@{ schemaVersion = 2; updatedAt = $now; files = [ordered]@{} }
foreach ($statePath in ($stateFiles.Keys | Sort-Object)) { $stateDocument.files[$statePath] = $stateFiles[$statePath] }
$temporaryPath = "$statesPath.$([System.Guid]::NewGuid().ToString('N')).tmp"
try {
    [System.IO.File]::WriteAllText($temporaryPath, (ConvertTo-Json -InputObject $stateDocument -Depth 20) + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $statesPath -Force
} finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
}
$historyEntry = [ordered]@{ changedAt = $now; path = $normalizedPath; from = $previousStatus; to = $Status; reason = $Reason; contentRead = $false }
Add-Content -LiteralPath $historyPath -Value ($historyEntry | ConvertTo-Json -Compress) -Encoding UTF8
Write-Host "Status updated without reading the source content: $normalizedPath ($previousStatus -> $Status)"
