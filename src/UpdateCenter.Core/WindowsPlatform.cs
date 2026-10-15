using System.Runtime.InteropServices;
using System.Text.Json;
using System.Net;

namespace UpdateCenter;

public sealed class WindowsPlatform(ICommandRunner runner, string dataRoot) : IUpdatePlatform
{
    internal const string IdleScript = " $paths=@(Get-CimInstance Win32_Process -Filter \"Name = 'codex.exe' OR Name = 'ChatGPT.exe' OR Name = 'Codex.exe'\" -ErrorAction Stop | Select-Object -ExpandProperty ExecutablePath); ConvertTo-Json -InputObject @($paths) -Compress";
    internal const string InstallScript = " $p=[Console]::In.ReadToEnd() | ConvertFrom-Json; $old=@(Get-AppxPackage -Name 'OpenAI.Codex' | Where-Object { $_.PackageFamilyName -eq $p.Identity -and $_.Publisher -eq 'CN=50BDFD77-8903-4850-9FFE-6E8522F64D5B' }); if($old.Count -ne 1 -or $old[0].Version.ToString() -ne $p.Version) {throw 'Installation changed; rescan first'}; Add-AppxPackage -Path $p.Package -ErrorAction Stop";
    public async Task AssertIdleAsync(Installation target, CancellationToken ct)
    {
        var result = await runner.RunAsync(CommandRunner.PowerShell(IdleScript), TimeSpan.FromSeconds(30), ct);
        if (result.ExitCode != 0) throw new IOException("无法确认 Codex 是否退出，停止安装 / Cannot establish process state.");
        var paths = JsonSerializer.Deserialize<string?[]>(result.Output, Data.Json) ?? throw new InvalidDataException("Invalid process inventory.");
        // An inaccessible relevant process is not evidence that the target is idle.
        if (paths.Any(p => string.IsNullOrEmpty(p) || Data.Within(p, target.Location)))
            throw new IOException("请先保存工作并退出所选安装的 Codex；不会强制结束进程。若有其他用户的 Codex，请由其退出后重试。");
    }
    public static Command NpmCommand(NpmTool tool, Installation target, ReleaseOffer offer, string cache, string emptyConfig, string verifiedPackage)
    {
        if (target.Kind != InstallKind.Npm || offer.Kind != InstallKind.Npm || target.Identity != "@openai/codex") throw new InvalidDataException("Wrong installation adapter.");
        _ = Data.Stable(offer.Version); Data.NoLinks(target.Location); Data.NoLinks(tool.Node); Data.NoLinks(tool.Cli);
        Data.NoLinks(verifiedPackage);
        return new(tool.Node, [tool.Cli, "install", "--global", "--prefix", target.Location, verifiedPackage,
            "@openai/codex-win32-x64@npm:@openai/codex@" + offer.Version + "-win32-x64", "--include=optional",
            "--registry=https://registry.npmjs.org/", "--@openai:registry=https://registry.npmjs.org/", "--ignore-scripts", "--no-audit", "--no-fund", "--cache", cache,
            "--prefer-offline", "--maxsockets=2", "--fetch-retries=2", "--fetch-timeout=120000", "--userconfig", emptyConfig, "--globalconfig", emptyConfig + ".global"], Environment: NpmEnvironment(HttpClient.DefaultProxy));
    }
    internal static Dictionary<string,string> NpmEnvironment(IWebProxy proxy)
    {
        var values = new Dictionary<string,string> { ["NPM_CONFIG_STRICT_SSL"] = "true", ["NPM_CONFIG_FORCE"] = "false", ["NPM_CONFIG_UPDATE_NOTIFIER"] = "false" };
        var registry = new Uri("https://registry.npmjs.org/");
        if (!proxy.IsBypassed(registry) && proxy.GetProxy(registry) is { } endpoint && endpoint != registry)
        {
            if (endpoint.Scheme is not ("http" or "https")) throw new IOException("npm 更新需要 HTTP(S) 系统代理；请检查网络代理设置。");
            values["NPM_CONFIG_HTTPS_PROXY"] = endpoint.AbsoluteUri;
            values["NPM_CONFIG_PROXY"] = endpoint.AbsoluteUri;
        }
        return values;
    }
    public async Task PrepareNpmAsync(ReleaseOffer offer, string package, CancellationToken ct, IProgress<string>? progress = null)
    {
        if (offer.Kind != InstallKind.Npm) throw new InvalidOperationException("Wrong adapter.");
        var tool = WindowsDiscovery.FindNpm() ?? throw new IOException("找不到标准 npm 安装 / npm installation unavailable.");
        var config = Path.Combine(dataRoot, "npm-empty-config"); Data.NoLinks(config); Data.NoLinks(config + ".global");
        File.WriteAllText(config, ""); File.WriteAllText(config + ".global", "");
        var directory = Path.GetDirectoryName(package)!;
        await NativeDependency.CacheAsync(runner, tool, offer.Version, Path.Combine(directory, "npm-cache"), config, directory, ct, progress);
    }
    public async Task InstallAsync(PreparedUpdate prepared, CancellationToken ct)
    {
        await AssertIdleAsync(prepared.Target, ct);
        long installBudget = 4 * CacheStorage.GiB;
        if (prepared.Target.Kind == InstallKind.DesktopMsix)
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(prepared.Package);
            installBudget = checked(archive.Entries.Sum(e => e.Length) * 2 + CacheStorage.GiB);
        }
        CacheStorage.RequireSpace(prepared.Target.Location, installBudget);
        Command command;
        if (prepared.Target.Kind == InstallKind.DesktopMsix)
            command = CommandRunner.PowerShell(InstallScript, new { prepared.Target.Identity, prepared.Target.Version, prepared.Package });
        else if (prepared.Target.Kind == InstallKind.Npm)
        {
            var tool = WindowsDiscovery.FindNpm() ?? throw new IOException("找不到标准 npm 安装 / npm installation unavailable.");
            var config = Path.Combine(dataRoot, "npm-empty-config");
            var directory = Path.GetDirectoryName(prepared.Package)!;
            var cache = Path.Combine(directory, "npm-cache");
            await PrepareNpmAsync(prepared.Offer, prepared.Package, ct);
            // Native preparation can take time; check again immediately before mutation.
            await AssertIdleAsync(prepared.Target, ct);
            CacheStorage.RequireSpace(prepared.Target.Location, installBudget);
            var current = WindowsDiscovery.FromNpm(prepared.Target.Location, true);
            if (current == null || current.Version != prepared.Target.Version || current.Identity != prepared.Target.Identity || current.Kind != InstallKind.Npm)
                throw new IOException("准备期间安装已变化，请重新扫描；不会覆盖或降级其他更新。");
            command = NpmCommand(tool, prepared.Target, prepared.Offer, cache, config, prepared.Package);
        }
        else throw new InvalidOperationException("Unsupported installation.");
        var result = await runner.RunAsync(command, TimeSpan.FromMinutes(20), ct);
        if (result.ExitCode != 0) throw new IOException($"安装器未成功（退出码 {result.ExitCode}）。请重新扫描版本并检查 Windows 部署日志或 npm 日志。不会将此结果记为成功。");
        if (prepared.Target.Kind == InstallKind.Npm)
        {
            var tool = WindowsDiscovery.FindNpm() ?? throw new IOException("npm unavailable after installation.");
            await VerifyNpmRuntimeAsync(runner, tool, prepared.Target.Location, prepared.Offer.Version, dataRoot, ct);
        }
    }
    internal static async Task VerifyNpmRuntimeAsync(ICommandRunner runner, NpmTool tool, string prefix, string version, string dataRoot, CancellationToken ct)
    {
        var entry = Path.Combine(prefix, "node_modules", "@openai", "codex", "bin", "codex.js");
        Data.NoLinks(entry); Data.NoLinks(tool.Node); _ = Data.Stable(version);
        var isolatedHome = Path.Combine(dataRoot, "runtime-check", Guid.NewGuid().ToString("N"));
        Data.NoLinks(isolatedHome); Directory.CreateDirectory(isolatedHome);
        var probe = await runner.RunAsync(new(tool.Node, [entry, "--version"], Environment: new Dictionary<string,string> { ["CODEX_HOME"] = isolatedHome }), TimeSpan.FromSeconds(30), ct);
        if (probe.ExitCode != 0 || probe.Output.Trim() != "codex-cli " + version)
            throw new IOException("版本文件已更新，但 CLI 实际启动验证未通过。请检查 npm 原生组件；不会报告更新成功。");
    }
}

public static class NetworkCost
{
    public static string Read()
    {
        object? instance = null;
        try { instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"), true)!)!; ((ICost)instance).GetCost(out var cost, IntPtr.Zero); return cost == 1 ? "Unrestricted" : cost == 0 ? "Unknown" : "Metered"; }
        catch (Exception e) when (e is COMException or InvalidCastException or UnauthorizedAccessException) { return "Unknown"; }
        finally { if (instance != null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance); }
    }
    [ComImport, Guid("DCB00008-570F-4A9B-8D69-199FDBA5723B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICost { void GetCost(out uint cost, IntPtr destination); }
}
