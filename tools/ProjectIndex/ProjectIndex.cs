#:property ImplicitUsings=disable
#:property Nullable=disable

// 專案增量索引工具 (跨平台): 以 dotnet run 執行, 規格見同目錄 README.md, 使用方式見倉庫根 INDEX_WORKFLOW.md
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

static class ProjectIndexTool
{
    const int IndexSchemaVersion = 2;
    const int AnalysisVersion = 1;
    const System.StringComparison IgnoreCase = System.StringComparison.OrdinalIgnoreCase;
    static readonly System.StringComparer KeyComparer = System.StringComparer.OrdinalIgnoreCase;
    static readonly JsonNodeOptions NodeOptions = new JsonNodeOptions { PropertyNameCaseInsensitive = true };
    static readonly JsonSerializerOptions IndentedJson = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly JsonSerializerOptions CompactJson = new JsonSerializerOptions { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly string[] StatusValues = { "unclassified", "draft", "approved", "withdrawn", "deprecated" };

    // 任何層級都排除的名稱
    static readonly HashSet<string> ExcludedNames = new HashSet<string>(KeyComparer)
    {
        ".git", ".vs", ".DS_Store", "PackageCache", "Obj", "Build", "Builds", "bin", "obj", "node_modules", "project_index",
    };

    // Unity 只在專案根產生的資料夾, 只比對第一層, 以免排除 Scripts/Core/Logs/ 這類同名來源目錄
    static readonly HashSet<string> ExcludedRootNames = new HashSet<string>(KeyComparer) { "Library", "Temp", "Logs", "UserSettings" };

    static readonly HashSet<string> BinaryExtensions = new HashSet<string>(KeyComparer)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".tga", ".psd", ".ttf", ".otf", ".fbx", ".blend", ".obj", ".mp3", ".wav", ".ogg", ".mp4", ".mov", ".dll", ".so", ".aab", ".apk", ".unitypackage", ".assetbundle", ".bytes", ".zip", ".7z", ".pdf",
    };

    static readonly HashSet<string> SerializedAssetExtensions = new HashSet<string>(KeyComparer)
    {
        ".unity", ".prefab", ".asset", ".controller", ".overridecontroller", ".mat", ".anim", ".playable", ".rendertexture", ".guiskin", ".preset", ".physicmaterial", ".physicsmaterial2d",
    };

    sealed class RootDefinition
    {
        public string FullPath;
        public string Name;
        public bool Recursive;
    }

    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) { throw new UsageException("Specify a command: update, find, set-analysis or set-status."); }
            var options = ParseOptions(args.Skip(1).ToArray());
            switch (args[0].ToLowerInvariant())
            {
                case "update": return Update(options);
                case "find": return Find(options);
                case "set-analysis": return SetAnalysis(options);
                case "set-status": return SetStatus(options);
                default: throw new UsageException($"Unknown command: {args[0]}");
            }
        }
        catch (System.Exception exception)
        {
            System.Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    sealed class UsageException : System.Exception
    {
        public UsageException(string message) : base(message) { }
    }

    // 參數格式: --name value 或 --flag; 名稱不分大小寫
    static Dictionary<string, string> ParseOptions(string[] args)
    {
        var flags = new HashSet<string>(KeyComparer) { "include-meta", "detail" };
        var options = new Dictionary<string, string>(KeyComparer);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", System.StringComparison.Ordinal)) { throw new UsageException($"Unexpected argument: {args[i]}"); }
            string name = args[i].Substring(2);
            if (flags.Contains(name)) { options[name] = "true"; continue; }
            if (i + 1 >= args.Length) { throw new UsageException($"Missing value for --{name}"); }
            options[name] = args[++i];
        }
        return options;
    }

    static string Option(Dictionary<string, string> options, string name) => options.TryGetValue(name, out var value) ? value : null;

    static string RequireOption(Dictionary<string, string> options, string name)
    {
        string value = Option(options, name);
        if (string.IsNullOrWhiteSpace(value)) { throw new UsageException($"--{name} is required."); }
        return value;
    }

    // 預設專案根: 本檔位於 tools/ProjectIndex/, 往上兩層
    static string ResolveProjectRoot(Dictionary<string, string> options)
    {
        string root = Option(options, "project-root");
        if (string.IsNullOrWhiteSpace(root))
        {
            string scriptDirectory = System.AppContext.GetData("EntryPointFileDirectoryPath") as string;
            root = string.IsNullOrEmpty(scriptDirectory) ? Directory.GetCurrentDirectory() : Path.Combine(scriptDirectory, "..", "..");
        }
        string fullPath = Path.GetFullPath(root).TrimEnd('/', '\\');
        if (!Directory.Exists(fullPath)) { throw new DirectoryNotFoundException($"Project root not found: {fullPath}"); }
        return fullPath;
    }

    static string GetRelativePath(string projectPath, string fullPath)
    {
        string relativePath = Path.GetRelativePath(projectPath, fullPath).Replace('\\', '/');
        if (relativePath == "." ) { return string.Empty; }
        if (relativePath == ".." || relativePath.StartsWith("../", System.StringComparison.Ordinal) || Path.IsPathRooted(relativePath)) { throw new System.InvalidOperationException($"Path is outside project root: {fullPath}"); }
        return relativePath;
    }

    static string ParentOf(string relativePath)
    {
        int slash = relativePath.LastIndexOf('/');
        return slash < 0 ? string.Empty : relativePath.Substring(0, slash);
    }

    static bool IsIncludedPath(string relativePath)
    {
        string[] parts = relativePath.Split('/');
        if (ExcludedRootNames.Contains(parts[0])) { return false; }
        foreach (string part in parts)
        {
            if (part.Contains("backup", IgnoreCase) || Regex.IsMatch(part, "^(?i)_?archive$") || ExcludedNames.Contains(part)) { return false; }
        }
        return !Regex.IsMatch(relativePath, "^(?i:docs/inbox|docs/archive|docs/ignore)(/|$)");
    }

    static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return System.Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    static string HashText(string value) => System.Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    static JsonNode ReadJsonFile(string path)
    {
        if (!File.Exists(path)) { return null; }
        string raw = File.ReadAllText(path, Encoding.UTF8);
        return string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw, NodeOptions);
    }

    static void WriteJsonAtomic(string path, JsonNode value)
    {
        string temporaryPath = $"{path}.{System.Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, value.ToJsonString(IndentedJson) + System.Environment.NewLine, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) { File.Delete(temporaryPath); }
        }
    }

    static string Text(JsonNode node, string key) => node is JsonObject obj && obj[key] is JsonValue value && value.TryGetValue(out string text) ? text : null;

    static int? Integer(JsonNode node, string key)
    {
        if (node is not JsonObject obj || obj[key] is not JsonValue value) { return null; }
        if (value.TryGetValue(out int number)) { return number; }
        if (value.TryGetValue(out double real)) { return (int)real; }
        return null;
    }

    static bool SameText(string left, string right) => string.Equals(left ?? string.Empty, right ?? string.Empty, IgnoreCase);

    // 舊 JSON 的 files 物件轉為不分大小寫的字典 (節點已脫離原父節點, 可直接放入新文件)
    static Dictionary<string, JsonNode> FilesOf(JsonNode document)
    {
        var files = new Dictionary<string, JsonNode>(KeyComparer);
        if (document?["files"] is JsonObject obj)
        {
            foreach (var property in obj) { files[property.Key] = property.Value?.DeepClone(); }
        }
        return files;
    }

    static JsonObject SortedObject(Dictionary<string, JsonNode> source)
    {
        var result = new JsonObject(NodeOptions);
        foreach (string key in source.Keys.OrderBy(k => k, KeyComparer)) { result[key] = source[key]?.DeepClone(); }
        return result;
    }

    static JsonArray StringArray(IEnumerable<string> values) => new JsonArray(values.Select(v => (JsonNode)JsonValue.Create(v)).ToArray());

    static string KindOf(string extension)
    {
        switch (extension)
        {
            case ".cs": return "csharp";
            case ".asmdef": return "assembly-definition";
            case ".asmref": return "assembly-reference";
            case ".meta": return "unity-meta";
        }
        if (SerializedAssetExtensions.Contains(extension)) { return "unity-serialized-asset"; }
        if (BinaryExtensions.Contains(extension)) { return "binary-asset"; }
        if (extension is ".csproj" or ".sln" or ".props" or ".targets") { return "dotnet-project"; }
        if (extension is ".md" or ".mdc") { return "documentation"; }
        if (extension == ".ps1") { return "powershell"; }
        return "configuration-or-data";
    }

    static int Update(Dictionary<string, string> options)
    {
        string projectPath = ResolveProjectRoot(options);
        string indexPath = Path.Combine(projectPath, "project_index");

        var roots = new List<RootDefinition>();
        string assetsRoot = Path.Combine(projectPath, "Assets", "[TorbenJuniorUtility]");
        foreach (var (path, name) in new[]
        {
            (assetsRoot, "unity-library-assets"),
            (Path.Combine(projectPath, "ProjectSettings"), "unity-project-settings"),
            (Path.Combine(projectPath, "dotnet"), "dotnet-source-and-projects"),
            (Path.Combine(projectPath, "docs"), "project-documentation"),
            (Path.Combine(projectPath, "tools"), "project-tools"),
        })
        {
            if (Directory.Exists(path)) { roots.Add(new RootDefinition { FullPath = path, Name = name, Recursive = true }); }
        }
        foreach (string rootFile in new[] { "AGENTS.md", "CLAUDE.md", "README.md", "INDEX_WORKFLOW.md", ".gitignore" })
        {
            string rootFilePath = Path.Combine(projectPath, rootFile);
            if (File.Exists(rootFilePath)) { roots.Add(new RootDefinition { FullPath = rootFilePath, Name = "project-guidance", Recursive = false }); }
        }
        foreach (string packageFile in new[] { "manifest.json", "packages-lock.json" })
        {
            string packagePath = Path.Combine(projectPath, "Packages", packageFile);
            if (File.Exists(packagePath)) { roots.Add(new RootDefinition { FullPath = packagePath, Name = "unity-package-manifest", Recursive = false }); }
        }

        // 含隱藏檔, 與舊版 Get-ChildItem -Force 相同
        var enumeration = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 };
        var candidatePaths = new Dictionary<string, string>(KeyComparer);
        foreach (var root in roots)
        {
            IEnumerable<string> items = root.Recursive ? Directory.EnumerateFiles(root.FullPath, "*", enumeration) : new[] { root.FullPath };
            foreach (string item in items)
            {
                string relativePath = GetRelativePath(projectPath, item);
                if (!IsIncludedPath(relativePath)) { continue; }
                candidatePaths[relativePath] = item;
            }
        }
        var sortedPaths = candidatePaths.Keys.OrderBy(k => k, KeyComparer).ToList();

        var oldFiles = FilesOf(ReadJsonFile(Path.Combine(indexPath, "content.json")));
        var cacheFiles = FilesOf(ReadJsonFile(Path.Combine(indexPath, "analysis-cache.json")));
        var oldStates = ReadJsonFile(Path.Combine(indexPath, "states.json"));
        var stateFiles = FilesOf(oldStates);

        string assetRootPrefix = GetRelativePath(projectPath, assetsRoot).TrimEnd('/') + "/";
        var guidPattern = new Regex(@"^guid:\s*([0-9a-f]{32})\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        var metaGuidsByAssetPath = new Dictionary<string, string>(KeyComparer);
        foreach (string relativePath in sortedPaths)
        {
            if (!relativePath.StartsWith(assetRootPrefix, IgnoreCase) || !relativePath.EndsWith(".meta", IgnoreCase)) { continue; }
            var match = guidPattern.Match(File.ReadAllText(candidatePaths[relativePath], Encoding.UTF8));
            if (match.Success) { metaGuidsByAssetPath[relativePath.Substring(0, relativePath.Length - 5)] = match.Groups[1].Value.ToLowerInvariant(); }
        }
        string MetaGuid(string assetPath) => metaGuidsByAssetPath.TryGetValue(assetPath, out var guid) ? guid : null;

        var documentOptions = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        var guidToAssemblyName = new Dictionary<string, string>(KeyComparer);
        var assemblyByDirectory = new Dictionary<string, JsonObject>(KeyComparer);
        var assemblyDocument = new JsonObject(NodeOptions);
        var warnings = new List<string>();
        foreach (string relativePath in sortedPaths.Where(p => p.EndsWith(".asmdef", IgnoreCase)))
        {
            try
            {
                var definition = JsonNode.Parse(File.ReadAllText(candidatePaths[relativePath], Encoding.UTF8), NodeOptions, documentOptions);
                string name = Text(definition, "name");
                if (string.IsNullOrWhiteSpace(name)) { throw new System.InvalidOperationException("Assembly definition has no name."); }
                var references = definition["references"] is JsonArray array ? array.Select(r => r is JsonValue v && v.TryGetValue(out string s) ? s : null) : Enumerable.Empty<string>();
                var info = new JsonObject { ["name"] = name, ["kind"] = "asmdef", ["path"] = relativePath, ["guid"] = MetaGuid(relativePath), ["references"] = StringArray(references) };
                assemblyByDirectory[ParentOf(relativePath)] = info;
                assemblyDocument[relativePath] = info.DeepClone();
                string guid = MetaGuid(relativePath);
                if (guid != null) { guidToAssemblyName[guid] = name; }
            }
            catch (System.Exception exception) { warnings.Add($"Could not parse {relativePath} : {exception.Message}"); }
        }
        foreach (string relativePath in sortedPaths.Where(p => p.EndsWith(".asmref", IgnoreCase)))
        {
            try
            {
                var reference = JsonNode.Parse(File.ReadAllText(candidatePaths[relativePath], Encoding.UTF8), NodeOptions, documentOptions);
                string rawTarget = Text(reference, "reference") ?? string.Empty;
                string target = rawTarget;
                var guidMatch = Regex.Match(rawTarget, "^(?i:GUID):(.+)$");
                if (guidMatch.Success) { target = guidToAssemblyName.TryGetValue(guidMatch.Groups[1].Value.ToLowerInvariant(), out var resolved) ? resolved : null; }
                string name = string.IsNullOrEmpty(target) ? "<unresolved>" : target;
                var info = new JsonObject { ["name"] = name, ["kind"] = "asmref", ["path"] = relativePath, ["guid"] = MetaGuid(relativePath), ["references"] = StringArray(new[] { rawTarget }) };
                assemblyByDirectory[ParentOf(relativePath)] = info;
                assemblyDocument[relativePath] = info.DeepClone();
                if (name == "<unresolved>") { warnings.Add($"Unresolved asmref target: {relativePath}"); }
            }
            catch (System.Exception exception) { warnings.Add($"Could not parse {relativePath} : {exception.Message}"); }
        }

        var newFiles = new Dictionary<string, JsonObject>(KeyComparer);
        var guidOwners = new Dictionary<string, string>(KeyComparer);
        var ambiguousGuids = new HashSet<string>(KeyComparer);
        string assetRootDirectory = assetRootPrefix.TrimEnd('/');
        foreach (string relativePath in sortedPaths)
        {
            string filePath = candidatePaths[relativePath];
            var item = new FileInfo(filePath);
            string extension = item.Extension.ToLowerInvariant();
            string metaGuid = MetaGuid(relativePath);
            string unityGuid = metaGuid;
            if (extension == ".meta" && relativePath.StartsWith(assetRootPrefix, IgnoreCase)) { unityGuid = MetaGuid(relativePath.Substring(0, relativePath.Length - 5)); }
            if (metaGuid != null)
            {
                if (guidOwners.ContainsKey(metaGuid)) { ambiguousGuids.Add(metaGuid); }
                else { guidOwners[metaGuid] = relativePath; }
            }

            // 由檔案所在目錄往上找最近的 asmdef／asmref
            JsonObject assemblyInfo = null;
            if (relativePath.StartsWith(assetRootPrefix, IgnoreCase))
            {
                string directory = ParentOf(relativePath);
                while (directory.StartsWith(assetRootDirectory, IgnoreCase))
                {
                    if (assemblyByDirectory.TryGetValue(directory, out assemblyInfo)) { break; }
                    string parent = ParentOf(directory);
                    if (parent.Length == 0 || parent == directory) { break; }
                    directory = parent;
                }
            }

            string kind = KindOf(extension);
            var context = new JsonObject { ["kind"] = kind, ["assembly"] = assemblyInfo?.DeepClone(), ["assetGuid"] = metaGuid };
            newFiles[relativePath] = new JsonObject
            {
                ["sha256"] = HashFile(filePath),
                ["sizeBytes"] = item.Length,
                ["modifiedAt"] = item.LastWriteTimeUtc.ToString("o"),
                ["kind"] = kind,
                ["reviewable"] = extension != ".meta" && !BinaryExtensions.Contains(extension),
                ["assetGuid"] = metaGuid,
                ["unityGuid"] = unityGuid,
                ["assembly"] = assemblyInfo?.DeepClone(),
                ["contextHash"] = HashText(context.ToJsonString(CompactJson)),
            };
        }
        foreach (string ambiguousGuid in ambiguousGuids)
        {
            warnings.Add($"Duplicate Unity GUID; move detection disabled for {ambiguousGuid}");
            guidOwners.Remove(ambiguousGuid);
        }

        // 以 GUID 偵測搬移, 並讓有效快取與人工狀態跟著資產移動
        var oldGuidOwners = new Dictionary<string, string>(KeyComparer);
        foreach (var (oldPath, oldEntry) in oldFiles)
        {
            string guid = Text(oldEntry, "assetGuid");
            if (string.IsNullOrEmpty(guid)) { continue; }
            if (oldGuidOwners.ContainsKey(guid)) { ambiguousGuids.Add(guid); }
            else { oldGuidOwners[guid] = oldPath; }
        }
        var moves = new List<JsonObject>();
        var moveFrom = new HashSet<string>(KeyComparer);
        var moveTo = new Dictionary<string, string>(KeyComparer);
        foreach (var (guid, toPath) in guidOwners)
        {
            if (ambiguousGuids.Contains(guid) || !oldGuidOwners.TryGetValue(guid, out var fromPath)) { continue; }
            if (fromPath == toPath) { continue; }
            bool contentChanged = !SameText(Text(oldFiles[fromPath], "sha256"), Text(newFiles[toPath], "sha256"));
            moves.Add(new JsonObject { ["guid"] = guid, ["from"] = fromPath, ["to"] = toPath, ["contentChanged"] = contentChanged });
            moveFrom.Add(fromPath);
            moveTo[toPath] = fromPath;
        }

        bool IsValidCache(JsonNode cached, JsonObject current) =>
            cached != null
            && SameText(Text(cached, "sourceHash"), Text(current, "sha256"))
            && SameText(Text(cached, "contextHash"), Text(current, "contextHash"))
            && Integer(cached, "analysisVersion") == AnalysisVersion
            && cached["analysis"] != null;

        foreach (var move in moves)
        {
            string sourcePath = Text(move, "from");
            string destinationPath = Text(move, "to");
            if (cacheFiles.TryGetValue(sourcePath, out var cached) && !cacheFiles.ContainsKey(destinationPath) && IsValidCache(cached, newFiles[destinationPath]))
            {
                cacheFiles[destinationPath] = cached;
            }
            if (stateFiles.TryGetValue(sourcePath, out var state) && !stateFiles.ContainsKey(destinationPath))
            {
                stateFiles[destinationPath] = state;
                stateFiles.Remove(sourcePath);
            }
        }

        var added = new List<string>();
        var modified = new List<string>();
        var removed = new List<string>();
        int unchangedCount = 0;
        foreach (string relativePath in sortedPaths)
        {
            if (oldFiles.TryGetValue(relativePath, out var oldEntry))
            {
                if (!SameText(Text(oldEntry, "sha256"), Text(newFiles[relativePath], "sha256"))) { modified.Add(relativePath); }
                else { unchangedCount++; }
            }
            else if (!moveTo.ContainsKey(relativePath)) { added.Add(relativePath); }
        }
        foreach (string oldPath in oldFiles.Keys)
        {
            if (!newFiles.ContainsKey(oldPath) && !moveFrom.Contains(oldPath)) { removed.Add(oldPath); }
        }

        var contentReview = new JsonArray();
        foreach (string relativePath in sortedPaths)
        {
            var fileEntry = newFiles[relativePath];
            if (!fileEntry["reviewable"].GetValue<bool>()) { continue; }
            if (IsValidCache(cacheFiles.GetValueOrDefault(relativePath), fileEntry)) { continue; }
            string reason = "uncached";
            if (!oldFiles.TryGetValue(relativePath, out var oldEntry)) { reason = moveTo.ContainsKey(relativePath) ? "moved" : "added"; }
            else if (!SameText(Text(oldEntry, "sha256"), Text(fileEntry, "sha256"))) { reason = "modified"; }
            else if (!SameText(Text(oldEntry, "contextHash"), Text(fileEntry, "contextHash"))) { reason = "context-changed"; }
            contentReview.Add((JsonNode)new JsonObject
            {
                ["path"] = relativePath,
                ["reason"] = reason,
                ["sourceHash"] = Text(fileEntry, "sha256"),
                ["contextHash"] = Text(fileEntry, "contextHash"),
                ["kind"] = Text(fileEntry, "kind"),
                ["assetGuid"] = Text(fileEntry, "assetGuid"),
                ["assembly"] = fileEntry["assembly"]?.DeepClone(),
            });
        }

        string now = System.DateTime.UtcNow.ToString("o");
        JsonArray RootsArray() => new JsonArray(roots.Select(r => (JsonNode)new JsonObject { ["path"] = GetRelativePath(projectPath, r.FullPath), ["name"] = r.Name }).ToArray());
        JsonArray Sorted(List<string> values) => StringArray(values.OrderBy(v => v, KeyComparer));
        JsonArray MovesArray() => new JsonArray(moves.Select(m => (JsonNode)m.DeepClone()).ToArray());

        var filesObject = new JsonObject(NodeOptions);
        foreach (string relativePath in sortedPaths) { filesObject[relativePath] = newFiles[relativePath]; }
        var manifest = new JsonObject { ["schemaVersion"] = IndexSchemaVersion, ["generatedAt"] = now, ["projectRoot"] = ".", ["roots"] = RootsArray() };
        var contentDocument = new JsonObject { ["schemaVersion"] = IndexSchemaVersion, ["generatedAt"] = now, ["scope"] = RootsArray(), ["files"] = filesObject, ["assemblies"] = assemblyDocument };
        var changesDocument = new JsonObject
        {
            ["schemaVersion"] = IndexSchemaVersion, ["generatedAt"] = now,
            ["added"] = Sorted(added), ["modified"] = Sorted(modified), ["removed"] = Sorted(removed), ["moved"] = MovesArray(),
            ["unchangedCount"] = unchangedCount, ["indexedCount"] = newFiles.Count, ["unreviewedCount"] = contentReview.Count, ["warnings"] = StringArray(warnings),
        };
        var queueDocument = new JsonObject
        {
            ["schemaVersion"] = IndexSchemaVersion, ["generatedAt"] = now, ["contentReview"] = contentReview,
            ["locationChanges"] = MovesArray(), ["removed"] = Sorted(removed), ["unreviewedCount"] = contentReview.Count,
        };
        var cacheDocument = new JsonObject { ["schemaVersion"] = IndexSchemaVersion, ["analysisVersion"] = AnalysisVersion, ["updatedAt"] = now, ["files"] = SortedObject(cacheFiles) };
        var stateDocument = new JsonObject { ["schemaVersion"] = IndexSchemaVersion, ["updatedAt"] = Text(oldStates, "updatedAt"), ["files"] = SortedObject(stateFiles) };

        Directory.CreateDirectory(indexPath);
        WriteJsonAtomic(Path.Combine(indexPath, "manifest.json"), manifest);
        WriteJsonAtomic(Path.Combine(indexPath, "content.json"), contentDocument);
        WriteJsonAtomic(Path.Combine(indexPath, "changes.json"), changesDocument);
        WriteJsonAtomic(Path.Combine(indexPath, "work-queue.json"), queueDocument);
        WriteJsonAtomic(Path.Combine(indexPath, "analysis-cache.json"), cacheDocument);
        WriteJsonAtomic(Path.Combine(indexPath, "states.json"), stateDocument);

        System.Console.WriteLine($"Index updated: {newFiles.Count} files in {roots.Count} explicit source roots.");
        System.Console.WriteLine($"Added: {added.Count}  Modified: {modified.Count}  Removed: {removed.Count}  Moved: {moves.Count}  Without valid analysis cache: {contentReview.Count} (not a to-do list)");
        foreach (string warning in warnings) { System.Console.Error.WriteLine($"WARNING: {warning}"); }
        return 0;
    }

    static JsonNode LoadContent(string projectPath, string missingMessage)
    {
        var content = ReadJsonFile(Path.Combine(projectPath, "project_index", "content.json"));
        if (content == null) { throw new FileNotFoundException(missingMessage); }
        return content;
    }

    // 只讀查詢: 在工具內解析索引, 只輸出匹配項目; 不更新也不寫入 project_index/
    static int Find(Dictionary<string, string> options)
    {
        string path = Option(options, "path");
        string assembly = Option(options, "assembly");
        string guid = Option(options, "guid");
        bool includeMeta = options.ContainsKey("include-meta");
        bool detail = options.ContainsKey("detail");
        int limit = 50;
        if (Option(options, "limit") is string limitText && (!int.TryParse(limitText, out limit) || limit < 1 || limit > 10000)) { throw new UsageException("--limit must be between 1 and 10000."); }
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(assembly) && string.IsNullOrWhiteSpace(guid))
        {
            throw new UsageException("Specify at least one filter: --path, --assembly or --guid. Listing the whole index is not supported.");
        }

        string normalizedGuid = null;
        if (!string.IsNullOrWhiteSpace(guid))
        {
            normalizedGuid = guid.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(normalizedGuid, "^[0-9a-f]{32}$")) { throw new UsageException("Guid must be 32 hexadecimal characters."); }
        }

        Regex pathRegex = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            // 不分大小寫的子字串比對; 只有 * 與 ? 是萬用字元, [ ] 視為一般字元
            string pattern = Regex.Escape(path.Replace('\\', '/')).Replace(@"\*", ".*").Replace(@"\?", ".");
            pathRegex = new Regex(pattern, RegexOptions.IgnoreCase);
        }

        string projectPath = ResolveProjectRoot(options);
        var content = LoadContent(projectPath, "Index not found. Run the update command first; this query never updates the index.");
        var files = content["files"] as JsonObject ?? new JsonObject(NodeOptions);
        var indexedPaths = new HashSet<string>(files.Select(p => p.Key), KeyComparer);

        var hits = new List<(string Path, string Kind, JsonNode Entry)>();
        foreach (var property in files)
        {
            var entry = property.Value;
            string hitPath = property.Key;
            string hitKind = Text(entry, "kind");
            if (hitKind == "unity-meta" && !includeMeta)
            {
                // 資料夾沒有自己的檔案項目, GUID 查詢時以資料夾 .meta 代表資料夾
                string assetPath = hitPath.Substring(0, hitPath.Length - 5);
                if (normalizedGuid == null || !SameText(Text(entry, "unityGuid"), normalizedGuid) || indexedPaths.Contains(assetPath)) { continue; }
                hitPath = assetPath;
                hitKind = "folder";
            }
            if (pathRegex != null && !pathRegex.IsMatch(hitPath)) { continue; }
            if (!string.IsNullOrEmpty(assembly) && !SameText(Text(entry?["assembly"], "name"), assembly)) { continue; }
            if (normalizedGuid != null && !SameText(Text(entry, "assetGuid"), normalizedGuid) && !SameText(Text(entry, "unityGuid"), normalizedGuid)) { continue; }
            hits.Add((hitPath, hitKind, entry));
        }

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(path)) { filters.Add($"path={path}"); }
        if (!string.IsNullOrEmpty(assembly)) { filters.Add($"assembly={assembly}"); }
        if (normalizedGuid != null) { filters.Add($"guid={normalizedGuid}"); }
        if (includeMeta) { filters.Add("include-meta"); }
        System.Console.WriteLine($"index: generatedAt={Text(content, "generatedAt")} files={indexedPaths.Count} (read-only; refresh with the update command)");
        System.Console.WriteLine($"query: {string.Join(" ", filters)}");

        if (!string.IsNullOrEmpty(assembly))
        {
            var definitions = (content["assemblies"] as JsonObject ?? new JsonObject()).Where(p => SameText(Text(p.Value, "name"), assembly)).ToList();
            if (definitions.Count == 0) { System.Console.WriteLine($"definition: none for '{assembly}' in the index"); }
            foreach (var definition in definitions) { System.Console.WriteLine($"definition: {Text(definition.Value, "kind")} {definition.Key}"); }
        }

        int shownCount = System.Math.Min(hits.Count, limit);
        System.Console.WriteLine($"matches: {hits.Count} (showing {shownCount})");
        if (hits.Count == 0)
        {
            System.Console.WriteLine("No indexed file matched the query.");
            if (normalizedGuid != null) { System.Console.WriteLine("Note: only assets inside the indexed roots carry GUIDs; references to a GUID inside scenes/prefabs are not indexed and need a source search."); }
            return 0;
        }

        var warnings = new List<string>();
        for (int i = 0; i < shownCount; i++)
        {
            var hit = hits[i];
            var entry = hit.Entry;
            string assemblyText = "-";
            if (entry?["assembly"] is JsonObject assemblyInfo)
            {
                assemblyText = Text(assemblyInfo, "name");
                if (Text(assemblyInfo, "kind") == "asmref") { assemblyText += " (asmref)"; }
            }
            System.Console.WriteLine($"{hit.Kind}\t{assemblyText}\t{hit.Path}");
            if (detail)
            {
                string reviewable = entry?["reviewable"] is JsonValue value && value.TryGetValue(out bool flag) ? flag.ToString() : string.Empty;
                System.Console.WriteLine($"  assetGuid={Text(entry, "assetGuid")} unityGuid={Text(entry, "unityGuid")} reviewable={reviewable} size={entry?["sizeBytes"]} modified={Text(entry, "modifiedAt")}");
                System.Console.WriteLine($"  sha256={Text(entry, "sha256")} contextHash={Text(entry, "contextHash")}");
            }
            string fullPath = Path.Combine(projectPath, hit.Path);
            bool exists = hit.Kind == "folder" ? Directory.Exists(fullPath) : File.Exists(fullPath);
            if (!exists) { warnings.Add($"missing on disk (index may be stale): {hit.Path}"); }
        }
        if (hits.Count > shownCount) { System.Console.WriteLine($"... {hits.Count - shownCount} more; narrow the filters or raise --limit."); }

        // 只轉出與本次結果相關的更新器警告
        var changes = ReadJsonFile(Path.Combine(projectPath, "project_index", "changes.json"));
        if (changes?["warnings"] is JsonArray indexWarnings)
        {
            foreach (var indexWarning in indexWarnings)
            {
                string text = indexWarning?.GetValue<string>() ?? string.Empty;
                if (normalizedGuid != null && text.Contains(normalizedGuid, System.StringComparison.Ordinal)) { warnings.Add(text); continue; }
                foreach (var hit in hits)
                {
                    string assetGuid = Text(hit.Entry, "assetGuid");
                    if (text.Contains(hit.Path, System.StringComparison.Ordinal) || (!string.IsNullOrEmpty(assetGuid) && text.Contains(assetGuid, System.StringComparison.Ordinal))) { warnings.Add(text); break; }
                }
            }
        }
        foreach (string warning in warnings) { System.Console.WriteLine($"warning: {warning}"); }
        return 0;
    }

    static string NormalizeIndexedPath(string path)
    {
        string normalizedPath = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedPath) || normalizedPath.StartsWith('/') || Regex.IsMatch(normalizedPath, @"(^|/)\.{1,2}(/|$)"))
        {
            throw new UsageException("Path must be a project-relative indexed path.");
        }
        return normalizedPath;
    }

    static JsonNode FindIndexedEntry(JsonNode content, string normalizedPath)
    {
        var entry = (content["files"] as JsonObject)?.FirstOrDefault(p => string.Equals(p.Key, normalizedPath, IgnoreCase)).Value;
        if (entry == null) { throw new System.InvalidOperationException($"Path is not in the current index: {normalizedPath}"); }
        return entry;
    }

    static int SetAnalysis(Dictionary<string, string> options)
    {
        string normalizedPath = NormalizeIndexedPath(RequireOption(options, "path"));
        string sourceHash = RequireOption(options, "source-hash");
        string contextHash = RequireOption(options, "context-hash");
        string analysisJson = RequireOption(options, "analysis-json");
        string projectPath = ResolveProjectRoot(options);
        string cachePath = Path.Combine(projectPath, "project_index", "analysis-cache.json");

        var entry = FindIndexedEntry(LoadContent(projectPath, "Run the update command before storing analysis."), normalizedPath);
        if (!(entry["reviewable"] is JsonValue reviewable && reviewable.GetValue<bool>())) { throw new System.InvalidOperationException($"Indexed path is not reviewable: {normalizedPath}"); }
        if (!SameText(Text(entry, "sha256"), sourceHash) || !SameText(Text(entry, "contextHash"), contextHash))
        {
            throw new System.InvalidOperationException("The supplied fingerprints are stale; update the index and review the current work queue.");
        }
        string absolutePath = Path.GetFullPath(Path.Combine(projectPath, normalizedPath));
        if (!absolutePath.StartsWith(projectPath + Path.DirectorySeparatorChar, IgnoreCase)) { throw new System.InvalidOperationException("Resolved path is outside the project root."); }
        if (!File.Exists(absolutePath)) { throw new FileNotFoundException("Indexed source file no longer exists."); }
        if (!SameText(HashFile(absolutePath), sourceHash)) { throw new System.InvalidOperationException("Source changed after indexing; refresh the index before storing analysis."); }

        var analysis = JsonNode.Parse(analysisJson, NodeOptions);
        if (analysis == null) { throw new UsageException("--analysis-json must contain a JSON value."); }

        var cacheFiles = new Dictionary<string, JsonNode>(KeyComparer);
        var existing = ReadJsonFile(cachePath);
        if (Integer(existing, "analysisVersion") == AnalysisVersion) { cacheFiles = FilesOf(existing); }
        string now = System.DateTime.UtcNow.ToString("o");
        cacheFiles[normalizedPath] = new JsonObject
        {
            ["sourceHash"] = sourceHash.ToLowerInvariant(),
            ["contextHash"] = contextHash.ToLowerInvariant(),
            ["analysisVersion"] = AnalysisVersion,
            ["analyzedAt"] = now,
            ["analysis"] = analysis,
        };
        WriteJsonAtomic(cachePath, new JsonObject { ["schemaVersion"] = IndexSchemaVersion, ["analysisVersion"] = AnalysisVersion, ["updatedAt"] = now, ["files"] = SortedObject(cacheFiles) });
        System.Console.WriteLine($"Stored analysis for {normalizedPath} at the current source/context fingerprints.");
        return 0;
    }

    static int SetStatus(Dictionary<string, string> options)
    {
        string normalizedPath = NormalizeIndexedPath(RequireOption(options, "path"));
        string statusInput = RequireOption(options, "status");
        string status = StatusValues.FirstOrDefault(s => string.Equals(s, statusInput, IgnoreCase));
        if (status == null) { throw new UsageException($"--status must be one of: {string.Join(", ", StatusValues)}"); }
        string reason = Option(options, "reason") ?? string.Empty;
        string projectPath = ResolveProjectRoot(options);
        string indexPath = Path.Combine(projectPath, "project_index");
        string statesPath = Path.Combine(indexPath, "states.json");

        FindIndexedEntry(LoadContent(projectPath, "Run the update command before setting file status."), normalizedPath);
        var stateFiles = FilesOf(ReadJsonFile(statesPath));
        string previousStatus = stateFiles.TryGetValue(normalizedPath, out var previous) ? Text(previous, "status") : "unclassified";
        string now = System.DateTime.UtcNow.ToString("o");
        stateFiles[normalizedPath] = new JsonObject { ["status"] = status, ["updatedAt"] = now, ["reason"] = reason };
        WriteJsonAtomic(statesPath, new JsonObject { ["schemaVersion"] = IndexSchemaVersion, ["updatedAt"] = now, ["files"] = SortedObject(stateFiles) });

        var historyEntry = new JsonObject { ["changedAt"] = now, ["path"] = normalizedPath, ["from"] = previousStatus, ["to"] = status, ["reason"] = reason, ["contentRead"] = false };
        File.AppendAllText(Path.Combine(indexPath, "status-history.jsonl"), historyEntry.ToJsonString(CompactJson) + System.Environment.NewLine, new UTF8Encoding(false));
        System.Console.WriteLine($"Status updated without reading the source content: {normalizedPath} ({previousStatus} -> {status})");
        return 0;
    }
}
