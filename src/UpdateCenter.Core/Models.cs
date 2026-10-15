using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace UpdateCenter;

public enum InstallKind { DesktopMsix, Npm, Unsupported }
public sealed record Installation(string Id, InstallKind Kind, string Name, string Version, string Location, string Identity, string Detail)
{
    public string Display => $"{Name}  ·  {Version}  ·  {Detail}";
    public bool Supported => Kind != InstallKind.Unsupported;
}
public sealed record DiscoveryResult(Installation[] Installations, string[] Warnings);
public sealed record ReleaseOffer(InstallKind Kind, string Version, string Url, long Size, string? Integrity = null, string? Metadata = null, string? ETag = null);
public sealed record PreparedUpdate(Installation Target, ReleaseOffer Offer, string Package, string Sha256, DateTimeOffset At);
public sealed record Journal(int Schema, string Phase, PreparedUpdate? Prepared, string Message, DateTimeOffset At);
public sealed record Command(string Executable, string[] Arguments, string? Input = null, IReadOnlyDictionary<string,string>? Environment = null);
public sealed record CommandResult(int ExitCode, string Output, string Error);
public interface ICommandRunner { Task<CommandResult> RunAsync(Command command, TimeSpan timeout, CancellationToken ct); }
public interface IInstallationSource { Task<DiscoveryResult> DiscoverAsync(CancellationToken ct); }
public interface IUpdatePlatform
{
    Task AssertIdleAsync(Installation target, CancellationToken ct);
    Task InstallAsync(PreparedUpdate prepared, CancellationToken ct);
}
public static class Data
{
    public static string ProductVersion => typeof(Data).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+')[0];
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    public static string Id(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant())))[..24];
    public static Version Stable(string value) => Regex.IsMatch(value, @"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant) && Version.TryParse(value, out var version) ? version : throw new InvalidDataException("不支持的稳定版本号 / Invalid stable version.");
    public static bool Newer(string candidate, string installed) => Version.TryParse(candidate, out var next) && Version.TryParse(installed, out var current) && next > current;
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUpdateCenter", "Public");
    public static void NoLinks(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(path) || full.StartsWith(@"\\", StringComparison.Ordinal)) throw new IOException("需要本地绝对路径 / Local absolute path required.");
        for (var current = full; current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("路径包含链接，请选择真实目录 / Linked paths are not supported.");
    }
    public static bool Within(string file, string directory) => Path.GetFullPath(file).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static T? Read<T>(string path) { NoLinks(path); return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : default; }
    public static void Write<T>(string path, T value)
    {
        NoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value, Json); stream.Flush(true); }
        File.Move(temporary, path, true);
    }
}
public sealed class UpdateState
{
    public string Root { get; }
    public UpdateState(string root) { Root = Path.GetFullPath(root); Data.NoLinks(Root); Directory.CreateDirectory(Root); }
    public Journal? Read()
    {
        var value = Data.Read<Journal>(Path.Combine(Root, "job.json"));
        if (value != null && value.Schema != 1) throw new InvalidDataException("任务记录来自不兼容版本 / Unsupported journal schema.");
        return value;
    }
    public void Save(string phase, PreparedUpdate? prepared, string message) => Data.Write(Path.Combine(Root, "job.json"), new Journal(1, phase, prepared, message, DateTimeOffset.UtcNow));
    public FileStream Lock()
    {
        var path = Path.Combine(Root, "operation.lock"); Data.NoLinks(path);
        return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
}
