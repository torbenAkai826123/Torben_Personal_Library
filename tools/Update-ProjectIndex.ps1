param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$indexSchemaVersion = 2
$analysisVersion = 1
$projectPath = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
$indexPath = Join-Path $projectPath 'project_index'

function Get-RelativePath([string]$fullPath) {
    if (-not $fullPath.StartsWith($projectPath + '\', [System.StringComparison]::OrdinalIgnoreCase) -and -not [string]::Equals($fullPath, $projectPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside project root: $fullPath"
    }
    return $fullPath.Substring($projectPath.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
}

function Get-TextHash([string]$value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Read-JsonFile([string]$path, [object]$defaultValue) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $defaultValue }
    $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    if ([string]::IsNullOrWhiteSpace($raw)) { return $defaultValue }
    return ConvertFrom-Json -InputObject $raw
}

function Write-JsonAtomic([string]$path, [object]$value) {
    $json = ConvertTo-Json -InputObject $value -Depth 30
    $temporaryPath = "$path.$([System.Guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText($temporaryPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporaryPath -Destination $path -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
}

function Test-IncludedPath([string]$relativePath) {
    $parts = $relativePath.Split('/')
    foreach ($part in $parts) {
        if ($part -match '(?i)backup' -or $part -match '^(?i)_?archive$' -or $part -in @('.git', '.vs', 'Library', 'PackageCache', 'Temp', 'Logs', 'Obj', 'Build', 'Builds', 'UserSettings', 'bin', 'obj', 'node_modules', 'project_index')) { return $false }
    }
    if ($relativePath -match '^(?i:docs/inbox|docs/archive|docs/ignore)(/|$)') { return $false }
    return $true
}

$binaryExtensions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($extension in @('.png', '.jpg', '.jpeg', '.webp', '.tga', '.psd', '.ttf', '.otf', '.fbx', '.blend', '.obj', '.mp3', '.wav', '.ogg', '.mp4', '.mov', '.dll', '.so', '.aab', '.apk', '.unitypackage', '.assetbundle', '.bytes', '.zip', '.7z', '.pdf')) { [void]$binaryExtensions.Add($extension) }

$roots = [System.Collections.Generic.List[object]]::new()
$assetsRoot = Join-Path $projectPath 'Assets/[TorbenJuniorUtility]'
foreach ($rootDefinition in @(
    @{ Path = $assetsRoot; Name = 'unity-library-assets'; Recursive = $true },
    @{ Path = (Join-Path $projectPath 'ProjectSettings'); Name = 'unity-project-settings'; Recursive = $true },
    @{ Path = (Join-Path $projectPath 'dotnet'); Name = 'dotnet-source-and-projects'; Recursive = $true },
    @{ Path = (Join-Path $projectPath 'docs'); Name = 'project-documentation'; Recursive = $true },
    @{ Path = (Join-Path $projectPath 'tools'); Name = 'project-tools'; Recursive = $true }
)) {
    if (Test-Path -LiteralPath $rootDefinition.Path -PathType Container) { $roots.Add($rootDefinition) }
}
foreach ($rootFile in @('AGENTS.md', 'CLAUDE.md', 'README.md', 'INDEX_WORKFLOW.md', '.gitignore')) {
    $rootFilePath = Join-Path $projectPath $rootFile
    if (Test-Path -LiteralPath $rootFilePath -PathType Leaf) {
        $roots.Add(@{ Path = $rootFilePath; Name = 'project-guidance'; Recursive = $false })
    }
}
$packagesRoot = Join-Path $projectPath 'Packages'
foreach ($packageFile in @('manifest.json', 'packages-lock.json')) {
    $packagePath = Join-Path $packagesRoot $packageFile
    if (Test-Path -LiteralPath $packagePath -PathType Leaf) { $roots.Add(@{ Path = $packagePath; Name = 'unity-package-manifest'; Recursive = $false }) }
}

$candidatePaths = @{}
foreach ($rootDefinition in $roots) {
    if ($rootDefinition.Recursive) {
        $items = Get-ChildItem -LiteralPath $rootDefinition.Path -File -Recurse -Force
    } else {
        $items = @(Get-Item -LiteralPath $rootDefinition.Path)
    }
    foreach ($item in $items) {
        $relativePath = Get-RelativePath $item.FullName
        if (-not (Test-IncludedPath $relativePath)) { continue }
        $candidatePaths[$relativePath] = $item.FullName
    }
}

$oldContent = Read-JsonFile (Join-Path $indexPath 'content.json') ([PSCustomObject]@{ schemaVersion = $indexSchemaVersion; files = [PSCustomObject]@{} })
$oldCache = Read-JsonFile (Join-Path $indexPath 'analysis-cache.json') ([PSCustomObject]@{ schemaVersion = $indexSchemaVersion; analysisVersion = $analysisVersion; files = [PSCustomObject]@{} })
$oldStates = Read-JsonFile (Join-Path $indexPath 'states.json') ([PSCustomObject]@{ schemaVersion = $indexSchemaVersion; files = [PSCustomObject]@{} })
$oldFiles = @{}
$cacheFiles = @{}
$stateFiles = @{}
foreach ($property in $oldContent.files.PSObject.Properties) { $oldFiles[$property.Name] = $property.Value }
foreach ($property in $oldCache.files.PSObject.Properties) { $cacheFiles[$property.Name] = $property.Value }
foreach ($property in $oldStates.files.PSObject.Properties) { $stateFiles[$property.Name] = $property.Value }

$assetRootPrefix = (Get-RelativePath $assetsRoot).TrimEnd('/') + '/'
$metaGuidsByAssetPath = @{}
foreach ($relativePath in @($candidatePaths.Keys)) {
    if (-not $relativePath.StartsWith($assetRootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or -not $relativePath.EndsWith('.meta', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
    $metaText = [System.IO.File]::ReadAllText($candidatePaths[$relativePath], [System.Text.Encoding]::UTF8)
    $guidMatch = [regex]::Match($metaText, '(?im)^guid:\s*([0-9a-f]{32})\s*$')
    if ($guidMatch.Success) { $metaGuidsByAssetPath[$relativePath.Substring(0, $relativePath.Length - 5)] = $guidMatch.Groups[1].Value.ToLowerInvariant() }
}

$guidToAssemblyName = @{}
$assemblyByDirectory = @{}
$assemblyDocument = [ordered]@{}
$warnings = [System.Collections.Generic.List[string]]::new()
foreach ($relativePath in @($candidatePaths.Keys | Sort-Object)) {
    if (-not $relativePath.EndsWith('.asmdef', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
    try {
        $definition = ConvertFrom-Json -InputObject ([System.IO.File]::ReadAllText($candidatePaths[$relativePath], [System.Text.Encoding]::UTF8))
        if ([string]::IsNullOrWhiteSpace([string]$definition.name)) { throw 'Assembly definition has no name.' }
        $definitionInfo = [ordered]@{ name = [string]$definition.name; kind = 'asmdef'; path = $relativePath; guid = $metaGuidsByAssetPath[$relativePath]; references = @($definition.references) }
        $assemblyByDirectory[(Split-Path -Parent $relativePath).Replace('\', '/')] = $definitionInfo
        $assemblyDocument[$relativePath] = $definitionInfo
        if ($definitionInfo.guid) { $guidToAssemblyName[$definitionInfo.guid] = $definitionInfo.name }
    } catch { $warnings.Add("Could not parse $relativePath : $($_.Exception.Message)") }
}
foreach ($relativePath in @($candidatePaths.Keys | Sort-Object)) {
    if (-not $relativePath.EndsWith('.asmref', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
    try {
        $reference = ConvertFrom-Json -InputObject ([System.IO.File]::ReadAllText($candidatePaths[$relativePath], [System.Text.Encoding]::UTF8))
        $target = [string]$reference.reference
        if ($target -match '^(?i:GUID):(.+)$') { $target = $guidToAssemblyName[$Matches[1].ToLowerInvariant()] }
        $referenceInfo = [ordered]@{ name = $(if ($target) { $target } else { '<unresolved>' }); kind = 'asmref'; path = $relativePath; guid = $metaGuidsByAssetPath[$relativePath]; references = @([string]$reference.reference) }
        $assemblyByDirectory[(Split-Path -Parent $relativePath).Replace('\', '/')] = $referenceInfo
        $assemblyDocument[$relativePath] = $referenceInfo
        if ($referenceInfo.name -eq '<unresolved>') { $warnings.Add("Unresolved asmref target: $relativePath") }
    } catch { $warnings.Add("Could not parse $relativePath : $($_.Exception.Message)") }
}

$newFiles = [ordered]@{}
$guidOwners = @{}
$ambiguousGuids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($relativePath in @($candidatePaths.Keys | Sort-Object)) {
    $filePath = $candidatePaths[$relativePath]
    $item = Get-Item -LiteralPath $filePath
    $fileHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $extension = $item.Extension.ToLowerInvariant()
    $metaGuid = $metaGuidsByAssetPath[$relativePath]
    $unityGuid = $metaGuid
    if ($extension -eq '.meta' -and $relativePath.StartsWith($assetRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $assetPathForMeta = $relativePath.Substring(0, $relativePath.Length - 5)
        $unityGuid = $metaGuidsByAssetPath[$assetPathForMeta]
    }
    if ($metaGuid) {
        if ($guidOwners.ContainsKey($metaGuid)) { [void]$ambiguousGuids.Add($metaGuid) }
        else { $guidOwners[$metaGuid] = $relativePath }
    }

    $assemblyInfo = $null
    if ($relativePath.StartsWith($assetRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $directory = (Split-Path -Parent $relativePath).Replace('\', '/')
        while ($directory.StartsWith($assetRootPrefix.TrimEnd('/'), [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($assemblyByDirectory.ContainsKey($directory)) { $assemblyInfo = $assemblyByDirectory[$directory]; break }
            $parent = Split-Path -Parent $directory
            if (-not $parent -or $parent -eq $directory) { break }
            $directory = $parent.Replace('\', '/')
        }
    }

    $kind = switch ($extension) {
        '.cs' { 'csharp' }
        '.asmdef' { 'assembly-definition' }
        '.asmref' { 'assembly-reference' }
        '.meta' { 'unity-meta' }
        { $_ -in @('.unity', '.prefab', '.asset', '.controller', '.overridecontroller', '.mat', '.anim', '.playable', '.rendertexture', '.guiskin', '.preset', '.physicmaterial', '.physicsmaterial2d') } { 'unity-serialized-asset' }
        { $binaryExtensions.Contains($_) } { 'binary-asset' }
        { $_ -in @('.csproj', '.sln', '.props', '.targets') } { 'dotnet-project' }
        { $_ -in @('.md', '.mdc') } { 'documentation' }
        '.ps1' { 'powershell' }
        default { 'configuration-or-data' }
    }

    $context = [ordered]@{ kind = $kind; assembly = $assemblyInfo; assetGuid = $metaGuid }
    $contextHash = Get-TextHash (ConvertTo-Json -InputObject $context -Depth 15 -Compress)
    $isReviewable = $extension -ne '.meta' -and -not $binaryExtensions.Contains($extension)
    $newFiles[$relativePath] = [ordered]@{
        sha256 = $fileHash
        sizeBytes = $item.Length
        modifiedAt = $item.LastWriteTimeUtc.ToString('o')
        kind = $kind
        reviewable = $isReviewable
        assetGuid = $metaGuid
        unityGuid = $unityGuid
        assembly = $assemblyInfo
        contextHash = $contextHash
    }
}
foreach ($ambiguousGuid in $ambiguousGuids) {
    $warnings.Add("Duplicate Unity GUID; move detection disabled for $ambiguousGuid")
    [void]$guidOwners.Remove($ambiguousGuid)
}

$oldGuidOwners = @{}
foreach ($oldPath in $oldFiles.Keys) {
    $guid = [string]$oldFiles[$oldPath].assetGuid
    if (-not $guid) { continue }
    if ($oldGuidOwners.ContainsKey($guid)) { [void]$ambiguousGuids.Add($guid) }
    else { $oldGuidOwners[$guid] = $oldPath }
}
$moves = [System.Collections.Generic.List[object]]::new()
$moveFrom = @{}
$moveTo = @{}
foreach ($guid in $guidOwners.Keys) {
    if ($ambiguousGuids.Contains($guid) -or -not $oldGuidOwners.ContainsKey($guid)) { continue }
    $fromPath = [string]$oldGuidOwners[$guid]
    $toPath = [string]$guidOwners[$guid]
    if ($fromPath -eq $toPath) { continue }
    $oldHash = [string]$oldFiles[$fromPath].sha256
    $newHash = [string]$newFiles[$toPath].sha256
    $moves.Add([ordered]@{ guid = $guid; from = $fromPath; to = $toPath; contentChanged = ($oldHash -ne $newHash) })
    $moveFrom[$fromPath] = $true
    $moveTo[$toPath] = $fromPath
}

foreach ($move in $moves) {
    $sourcePath = [string]$move.from
    $destinationPath = [string]$move.to
    if ($cacheFiles.ContainsKey($sourcePath) -and -not $cacheFiles.ContainsKey($destinationPath)) {
        $cached = $cacheFiles[$sourcePath]
        $current = $newFiles[$destinationPath]
        if ($cached.sourceHash -eq $current.sha256 -and $cached.contextHash -eq $current.contextHash -and $cached.analysisVersion -eq $analysisVersion -and $null -ne $cached.analysis) {
            $cacheFiles[$destinationPath] = $cached
        }
    }
    if ($stateFiles.ContainsKey($sourcePath) -and -not $stateFiles.ContainsKey($destinationPath)) {
        $stateFiles[$destinationPath] = $stateFiles[$sourcePath]
        [void]$stateFiles.Remove($sourcePath)
    }
}

$added = [System.Collections.Generic.List[string]]::new()
$modified = [System.Collections.Generic.List[string]]::new()
$removed = [System.Collections.Generic.List[string]]::new()
$unchangedCount = 0
foreach ($relativePath in $newFiles.Keys) {
    if ($oldFiles.ContainsKey($relativePath)) {
        if ($oldFiles[$relativePath].sha256 -ne $newFiles[$relativePath].sha256) { $modified.Add($relativePath) }
        else { $unchangedCount++ }
    } elseif (-not $moveTo.ContainsKey($relativePath)) { $added.Add($relativePath) }
}
foreach ($oldPath in $oldFiles.Keys) {
    if (-not $newFiles.Contains($oldPath) -and -not $moveFrom.ContainsKey($oldPath)) { $removed.Add($oldPath) }
}

$contentReview = [System.Collections.Generic.List[object]]::new()
foreach ($relativePath in $newFiles.Keys) {
    $fileEntry = $newFiles[$relativePath]
    if (-not $fileEntry.reviewable) { continue }
    $cached = $cacheFiles[$relativePath]
    $isValidCache = $cached -and $cached.sourceHash -eq $fileEntry.sha256 -and $cached.contextHash -eq $fileEntry.contextHash -and $cached.analysisVersion -eq $analysisVersion -and $null -ne $cached.analysis
    if ($isValidCache) { continue }
    $reason = 'uncached'
    if (-not $oldFiles.ContainsKey($relativePath)) { $reason = $(if ($moveTo.ContainsKey($relativePath)) { 'moved' } else { 'added' }) }
    elseif ($oldFiles[$relativePath].sha256 -ne $fileEntry.sha256) { $reason = 'modified' }
    elseif ($oldFiles[$relativePath].contextHash -ne $fileEntry.contextHash) { $reason = 'context-changed' }
    $contentReview.Add([ordered]@{ path = $relativePath; reason = $reason; sourceHash = $fileEntry.sha256; contextHash = $fileEntry.contextHash; kind = $fileEntry.kind; assetGuid = $fileEntry.assetGuid; assembly = $fileEntry.assembly })
}

$now = [DateTime]::UtcNow.ToString('o')
$manifest = [ordered]@{ schemaVersion = $indexSchemaVersion; generatedAt = $now; projectRoot = '.'; roots = @($roots | ForEach-Object { [ordered]@{ path = (Get-RelativePath $_.Path); name = $_.Name } }) }
$contentDocument = [ordered]@{ schemaVersion = $indexSchemaVersion; generatedAt = $now; scope = $manifest.roots; files = $newFiles; assemblies = $assemblyDocument }
$changesDocument = [ordered]@{ schemaVersion = $indexSchemaVersion; generatedAt = $now; added = @($added | Sort-Object); modified = @($modified | Sort-Object); removed = @($removed | Sort-Object); moved = @($moves); unchangedCount = $unchangedCount; indexedCount = $newFiles.Count; unreviewedCount = $contentReview.Count; warnings = @($warnings) }
$queueDocument = [ordered]@{ schemaVersion = $indexSchemaVersion; generatedAt = $now; contentReview = @($contentReview); locationChanges = @($moves); removed = @($removed | Sort-Object); unreviewedCount = $contentReview.Count }
$cacheDocument = [ordered]@{ schemaVersion = $indexSchemaVersion; analysisVersion = $analysisVersion; updatedAt = $now; files = [ordered]@{} }
foreach ($cachePath in ($cacheFiles.Keys | Sort-Object)) { $cacheDocument.files[$cachePath] = $cacheFiles[$cachePath] }
$stateDocument = [ordered]@{ schemaVersion = $indexSchemaVersion; updatedAt = $(if ($oldStates.updatedAt) { $oldStates.updatedAt } else { $null }); files = [ordered]@{} }
foreach ($statePath in ($stateFiles.Keys | Sort-Object)) { $stateDocument.files[$statePath] = $stateFiles[$statePath] }

New-Item -ItemType Directory -Path $indexPath -Force | Out-Null
Write-JsonAtomic (Join-Path $indexPath 'manifest.json') $manifest
Write-JsonAtomic (Join-Path $indexPath 'content.json') $contentDocument
Write-JsonAtomic (Join-Path $indexPath 'changes.json') $changesDocument
Write-JsonAtomic (Join-Path $indexPath 'work-queue.json') $queueDocument
Write-JsonAtomic (Join-Path $indexPath 'analysis-cache.json') $cacheDocument
Write-JsonAtomic (Join-Path $indexPath 'states.json') $stateDocument

Write-Host "Index updated: $($newFiles.Count) files in $($roots.Count) explicit source roots."
Write-Host "Added: $($added.Count)  Modified: $($modified.Count)  Removed: $($removed.Count)  Moved: $($moves.Count)  Without valid analysis cache: $($contentReview.Count) (not a to-do list)"
if ($warnings.Count -gt 0) { Write-Warning ($warnings -join [Environment]::NewLine) }
