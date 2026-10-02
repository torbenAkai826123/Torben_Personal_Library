param(
    [string]$Path,
    [string]$Assembly,
    [string]$Guid,
    [switch]$IncludeMeta,
    [switch]$Detail,
    [ValidateRange(1, 10000)] [int]$Limit = 50,
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

# 只讀查詢: 在工具內解析索引, 只輸出匹配項目; 不更新也不寫入 project_index/
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Path) -and [string]::IsNullOrWhiteSpace($Assembly) -and [string]::IsNullOrWhiteSpace($Guid)) {
    throw 'Specify at least one filter: -Path, -Assembly or -Guid. Listing the whole index is not supported.'
}

$normalizedGuid = $null
if (-not [string]::IsNullOrWhiteSpace($Guid)) {
    $normalizedGuid = $Guid.Trim().ToLowerInvariant()
    if ($normalizedGuid -notmatch '^[0-9a-f]{32}$') { throw 'Guid must be 32 hexadecimal characters.' }
}

$pathRegex = $null
if (-not [string]::IsNullOrWhiteSpace($Path)) {
    # 不分大小寫的子字串比對; 只有 * 與 ? 是萬用字元, [ ] 視為一般字元
    $pattern = [regex]::Escape($Path.Replace('\', '/')).Replace('\*', '.*').Replace('\?', '.')
    $pathRegex = [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
}

$projectPath = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
$indexPath = Join-Path $projectPath 'project_index'
$contentPath = Join-Path $indexPath 'content.json'
if (-not (Test-Path -LiteralPath $contentPath -PathType Leaf)) {
    throw 'Index not found. Run tools/Update-ProjectIndex.ps1 first; this query never updates the index.'
}
$content = ConvertFrom-Json -InputObject ([System.IO.File]::ReadAllText($contentPath, [System.Text.Encoding]::UTF8))
$indexedPaths = @{}
foreach ($property in $content.files.PSObject.Properties) { $indexedPaths[$property.Name] = $true }

$hits = [System.Collections.Generic.List[object]]::new()
foreach ($property in $content.files.PSObject.Properties) {
    $entry = $property.Value
    $hitPath = $property.Name
    $hitKind = [string]$entry.kind
    if ($entry.kind -eq 'unity-meta' -and -not $IncludeMeta) {
        # 資料夾沒有自己的檔案項目, GUID 查詢時以資料夾 .meta 代表資料夾
        $assetPath = $hitPath.Substring(0, $hitPath.Length - 5)
        if (-not $normalizedGuid -or $entry.unityGuid -ne $normalizedGuid -or $indexedPaths.ContainsKey($assetPath)) { continue }
        $hitPath = $assetPath
        $hitKind = 'folder'
    }
    if ($pathRegex -and -not $pathRegex.IsMatch($hitPath)) { continue }
    if ($Assembly -and -not ($entry.assembly -and [string]::Equals([string]$entry.assembly.name, $Assembly, [System.StringComparison]::OrdinalIgnoreCase))) { continue }
    if ($normalizedGuid -and $entry.assetGuid -ne $normalizedGuid -and $entry.unityGuid -ne $normalizedGuid) { continue }
    $hits.Add([PSCustomObject]@{ Path = $hitPath; Kind = $hitKind; Entry = $entry })
}

$filters = @()
if ($Path) { $filters += "path=$Path" }
if ($Assembly) { $filters += "assembly=$Assembly" }
if ($normalizedGuid) { $filters += "guid=$normalizedGuid" }
if ($IncludeMeta) { $filters += 'include-meta' }
Write-Output "index: generatedAt=$($content.generatedAt) files=$($indexedPaths.Count) (read-only; refresh with Update-ProjectIndex.ps1)"
Write-Output "query: $($filters -join ' ')"

if ($Assembly) {
    $definitions = @($content.assemblies.PSObject.Properties | Where-Object { [string]::Equals([string]$_.Value.name, $Assembly, [System.StringComparison]::OrdinalIgnoreCase) })
    if ($definitions.Count -eq 0) { Write-Output "definition: none for '$Assembly' in the index" }
    foreach ($definition in $definitions) { Write-Output "definition: $($definition.Value.kind) $($definition.Name)" }
}

$shownCount = [Math]::Min($hits.Count, $Limit)
Write-Output "matches: $($hits.Count) (showing $shownCount)"
if ($hits.Count -eq 0) {
    Write-Output 'No indexed file matched the query.'
    if ($normalizedGuid) { Write-Output 'Note: only assets inside the indexed roots carry GUIDs; references to a GUID inside scenes/prefabs are not indexed and need a source search.' }
    return
}

$warnings = [System.Collections.Generic.List[string]]::new()
for ($i = 0; $i -lt $shownCount; $i++) {
    $hit = $hits[$i]
    $entry = $hit.Entry
    $assemblyText = '-'
    if ($entry.assembly) {
        $assemblyText = [string]$entry.assembly.name
        if ($entry.assembly.kind -eq 'asmref') { $assemblyText += ' (asmref)' }
    }
    Write-Output "$($hit.Kind)`t$assemblyText`t$($hit.Path)"
    if ($Detail) {
        Write-Output "  assetGuid=$($entry.assetGuid) unityGuid=$($entry.unityGuid) reviewable=$($entry.reviewable) size=$($entry.sizeBytes) modified=$($entry.modifiedAt)"
        Write-Output "  sha256=$($entry.sha256) contextHash=$($entry.contextHash)"
    }
    $pathType = $(if ($hit.Kind -eq 'folder') { 'Container' } else { 'Leaf' })
    if (-not (Test-Path -LiteralPath (Join-Path $projectPath $hit.Path) -PathType $pathType)) {
        $warnings.Add("missing on disk (index may be stale): $($hit.Path)")
    }
}
if ($hits.Count -gt $shownCount) { Write-Output "... $($hits.Count - $shownCount) more; narrow the filters or raise -Limit." }

# 只轉出與本次結果相關的更新器警告
$changesPath = Join-Path $indexPath 'changes.json'
if (Test-Path -LiteralPath $changesPath -PathType Leaf) {
    $changes = ConvertFrom-Json -InputObject ([System.IO.File]::ReadAllText($changesPath, [System.Text.Encoding]::UTF8))
    foreach ($indexWarning in @($changes.warnings)) {
        $text = [string]$indexWarning
        if ($normalizedGuid -and $text.Contains($normalizedGuid)) { $warnings.Add($text); continue }
        foreach ($hit in $hits) {
            if ($text.Contains($hit.Path) -or ($hit.Entry.assetGuid -and $text.Contains([string]$hit.Entry.assetGuid))) { $warnings.Add($text); break }
        }
    }
}
foreach ($warning in $warnings) { Write-Output "warning: $warning" }
