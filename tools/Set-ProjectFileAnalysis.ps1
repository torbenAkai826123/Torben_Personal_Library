param(
    [Parameter(Mandatory = $true)] [string]$Path,
    [Parameter(Mandatory = $true)] [string]$SourceHash,
    [Parameter(Mandatory = $true)] [string]$ContextHash,
    [Parameter(Mandatory = $true)] [string]$AnalysisJson,
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
$normalizedPath = $Path.Replace('\', '/')
if ([string]::IsNullOrWhiteSpace($normalizedPath) -or $normalizedPath.StartsWith('/') -or $normalizedPath -match '(^|/)\.{1,2}(/|$)') { throw 'Path must be a project-relative indexed path.' }

$indexPath = Join-Path $projectPath 'project_index'
$contentPath = Join-Path $indexPath 'content.json'
$cachePath = Join-Path $indexPath 'analysis-cache.json'
if (-not (Test-Path -LiteralPath $contentPath -PathType Leaf)) { throw 'Run Update-ProjectIndex.ps1 before storing analysis.' }
$content = Get-Content -LiteralPath $contentPath -Raw -Encoding UTF8 | ConvertFrom-Json
$fileProperty = $content.files.PSObject.Properties | Where-Object { [string]::Equals($_.Name, $normalizedPath, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
if (-not $fileProperty) { throw "Path is not in the current index: $normalizedPath" }
$entry = $fileProperty.Value
if (-not $entry.reviewable) { throw "Indexed path is not reviewable: $normalizedPath" }
if ($entry.sha256 -ne $SourceHash -or $entry.contextHash -ne $ContextHash) { throw 'The supplied fingerprints are stale; update the index and review the current work queue.' }
$absolutePath = [System.IO.Path]::GetFullPath((Join-Path $projectPath ($normalizedPath.Replace('/', '\'))))
if (-not $absolutePath.StartsWith($projectPath + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Resolved path is outside the project root.' }
if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) { throw 'Indexed source file no longer exists.' }
$actualHash = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $SourceHash.ToLowerInvariant()) { throw 'Source changed after indexing; refresh the index before storing analysis.' }

$analysis = ConvertFrom-Json -InputObject $AnalysisJson
if ($null -eq $analysis) { throw 'AnalysisJson must contain a JSON value.' }
$cache = [ordered]@{ schemaVersion = 2; analysisVersion = 1; updatedAt = [DateTime]::UtcNow.ToString('o'); files = [ordered]@{} }
if (Test-Path -LiteralPath $cachePath -PathType Leaf) {
    $existing = Get-Content -LiteralPath $cachePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($existing.analysisVersion -eq 1) {
        foreach ($property in $existing.files.PSObject.Properties) { $cache.files[$property.Name] = $property.Value }
    }
}
$cache.files[$normalizedPath] = [ordered]@{
    sourceHash = $SourceHash.ToLowerInvariant()
    contextHash = $ContextHash.ToLowerInvariant()
    analysisVersion = 1
    analyzedAt = [DateTime]::UtcNow.ToString('o')
    analysis = $analysis
}
$temporaryPath = "$cachePath.$([System.Guid]::NewGuid().ToString('N')).tmp"
try {
    [System.IO.File]::WriteAllText($temporaryPath, (ConvertTo-Json -InputObject $cache -Depth 30) + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $cachePath -Force
} finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
}
Write-Host "Stored analysis for $normalizedPath at the current source/context fingerprints."
