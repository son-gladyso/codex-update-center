using System.Text.Json;

namespace UpdateCenter;

public sealed record AppPackage(string Name, string Publisher, string Version, string Architecture, string PackageFamilyName, string InstallLocation, string SignatureKind, bool IsDevelopmentMode);
public sealed record NpmTool(string Node, string Cli);
public sealed class WindowsDiscovery(ICommandRunner runner, IEnumerable<string>? extraPrefixes = null) : IInstallationSource
{
    public const string Publisher = "CN=50BDFD77-8903-4850-9FFE-6E8522F64D5B";
    public const string PackageName = "OpenAI.Codex";
    internal const string DiscoverScript = "@(Get-AppxPackage -Name 'OpenAI.Codex' | ForEach-Object { [pscustomobject]@{Name=$_.Name;Publisher=$_.Publisher;Version=$_.Version.ToString();Architecture=$_.Architecture.ToString();PackageFamilyName=$_.PackageFamilyName;InstallLocation=$_.InstallLocation;SignatureKind=$_.SignatureKind.ToString();IsDevelopmentMode=$_.IsDevelopmentMode} }) | ConvertTo-Json -Depth 4 -Compress -AsArray";
    // Windows PowerShell 5.1 has no ConvertTo-Json -AsArray. Preserve empty/single arrays explicitly.
    internal static string AppxScript => DiscoverScript.Replace("@(Get-AppxPackage", "$items = @(Get-AppxPackage").Replace("}) | ConvertTo-Json -Depth 4 -Compress -AsArray", "}); ConvertTo-Json -InputObject @($items) -Depth 4 -Compress");
    public static string[] CandidatePrefixes() => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
        .Select(p => p.Trim().Trim('"')).Where(Path.IsPathFullyQualified)
        .Concat([Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm")])
        .Distinct(StringComparer.OrdinalIgnoreCase).Take(128).ToArray();
    public static NpmTool? FindNpm()
    {
        foreach (var directory in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs") }.Concat(CandidatePrefixes()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var node = Path.Combine(directory, "node.exe"); var cli = Path.Combine(directory, "node_modules", "npm", "bin", "npm-cli.js");
            if (!File.Exists(node) || !File.Exists(cli)) continue;
            try { Data.NoLinks(node); Data.NoLinks(cli); return new(node, cli); } catch (IOException) { }
        }
        return null;
    }
    public static Installation FromAppx(AppPackage p)
    {
        var supported = p.Name == PackageName && p.Publisher == Publisher && p.Architecture.Equals("X64", StringComparison.OrdinalIgnoreCase)
            && p.SignatureKind.Equals("Store", StringComparison.OrdinalIgnoreCase) && !p.IsDevelopmentMode
            && Version.TryParse(p.Version, out _) && Path.IsPathFullyQualified(p.InstallLocation) && !string.IsNullOrWhiteSpace(p.PackageFamilyName);
        return new(Data.Id("msix:" + p.PackageFamilyName), supported ? InstallKind.DesktopMsix : InstallKind.Unsupported, "Codex / ChatGPT 桌面版", p.Version, p.InstallLocation, p.PackageFamilyName,
            supported ? "Windows 签名安装包 · 当前用户" : "安装身份、签名类型或架构不受支持");
    }
    public static Installation? FromNpm(string prefix, bool toolAvailable)
    {
        prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(prefix));
        var manifest = Path.Combine(prefix, "node_modules", "@openai", "codex", "package.json");
        if (!File.Exists(manifest)) return null;
        Data.NoLinks(manifest);
        if (new FileInfo(manifest).Length > 1024 * 1024) throw new InvalidDataException("Package manifest too large.");
        using var json = JsonDocument.Parse(File.ReadAllText(manifest)); var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String)
            throw new JsonException("Package manifest must contain string name and version fields.");
        if (name.GetString() != "@openai/codex") return null;
        var version = versionElement.GetString() ?? "";
        var supported = toolAvailable && Version.TryParse(version, out _) && !version.Contains('-');
        return new(Data.Id("npm:" + prefix), supported ? InstallKind.Npm : InstallKind.Unsupported, "Codex CLI", version, prefix, "@openai/codex",
            supported ? "npm 全局安装 · 独立更新此目录" : "需要标准 Node.js/npm 安装及稳定版 CLI");
    }
    public async Task<DiscoveryResult> DiscoverAsync(CancellationToken ct)
    {
        var items = new List<Installation>(); var warnings = new List<string>();
        try
        {
            var result = await runner.RunAsync(CommandRunner.PowerShell(AppxScript), TimeSpan.FromSeconds(30), ct);
            if (result.ExitCode != 0) throw new IOException("Windows 安装列表查询失败 / Appx query failed.");
            items.AddRange((JsonSerializer.Deserialize<AppPackage[]>(result.Output, Data.Json) ?? []).Select(FromAppx));
        }
        catch (Exception e) when (e is not OperationCanceledException) { warnings.Add("无法完整检查 Windows 安装包；请检查当前用户权限。" ); }
        var npm = FindNpm();
        foreach (var prefix in CandidatePrefixes().Concat(extraPrefixes ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            try { var item = FromNpm(prefix, npm != null); if (item != null) items.Add(item); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or ArgumentException) { warnings.Add("有一个 CLI 候选目录无法可靠识别，未接管。" ); }
        }
        foreach (var directory in CandidatePrefixes().Concat([Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin")]))
        {
            var file = Path.Combine(directory, "codex.exe");
            if (!File.Exists(file) || items.Any(i => Data.Within(file, i.Location))) continue;
            items.Add(new(Data.Id(file), InstallKind.Unsupported, "Codex 可执行文件", "未知", directory, "standalone", "独立二进制：请使用原安装工具更新"));
        }
        return new(items.DistinctBy(i => i.Id).OrderBy(i => i.Kind).ThenBy(i => i.Location).ToArray(), warnings.Distinct().ToArray());
    }
}
