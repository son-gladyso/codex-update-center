using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace UpdateCenter;

public interface IPackageVerifier { Task<string> VerifyAsync(string package, ReleaseOffer offer, CancellationToken ct); }
public sealed class PackageVerifier(OfficialNetwork network, Func<string,CancellationToken,Task> verifyWindowsSignature) : IPackageVerifier
{
    public async Task<string> VerifyAsync(string package, ReleaseOffer offer, CancellationToken ct)
        => await VerifyArtifactAsync(package, offer, false, ct);
    internal Task<string> VerifyNativeAsync(string package, ReleaseOffer offer, CancellationToken ct)
        => VerifyArtifactAsync(package, offer, true, ct);
    private async Task<string> VerifyArtifactAsync(string package, ReleaseOffer offer, bool native, CancellationToken ct)
    {
        Data.NoLinks(package);
        if (offer.Kind == InstallKind.Npm)
        {
            if (offer.Metadata == null || offer.Integrity == null || !offer.Integrity.StartsWith("sha512-", StringComparison.Ordinal)) throw new InvalidDataException("Missing npm integrity.");
            await using var file = File.OpenRead(package); var hash = await SHA512.HashDataAsync(file, ct);
            if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(offer.Integrity[7..]))) throw new InvalidDataException("npm 安装包校验失败 / npm integrity mismatch.");
            using var metadata = JsonDocument.Parse(offer.Metadata); var value = metadata.RootElement;
            if (value.GetProperty("name").GetString() != "@openai/codex" || value.GetProperty("version").GetString() != offer.Version || value.GetProperty("dist").GetProperty("integrity").GetString() != offer.Integrity) throw new InvalidDataException("npm metadata mismatch.");
            if (native)
            {
                const string suffix = "-win32-x64";
                if (!offer.Version.EndsWith(suffix, StringComparison.Ordinal)) throw new InvalidDataException("Wrong native architecture.");
                _ = Data.Stable(offer.Version[..^suffix.Length]);
            }
            else _ = Data.Stable(offer.Version);
            using var keys = JsonDocument.Parse(await network.TextAsync("https://registry.npmjs.org/-/npm/v1/keys", ct));
            if (!VerifyNpmSignature(value, keys.RootElement, DateTimeOffset.UtcNow)) throw new InvalidDataException("npm 发布签名无效 / npm registry signature invalid.");
            return offer.Version;
        }
        if (offer.Kind != InstallKind.DesktopMsix || offer.Url != OfficialNetwork.DesktopUrl) throw new InvalidDataException("Unsupported artifact.");
        await verifyWindowsSignature(package, ct);
        return await VerifyMsixContentsAsync(package, ct);
    }
    internal static bool VerifyNpmSignature(JsonElement metadata, JsonElement keys, DateTimeOffset now)
    {
        var dist = metadata.GetProperty("dist"); var payload = Encoding.UTF8.GetBytes(metadata.GetProperty("name").GetString() + "@" + metadata.GetProperty("version").GetString() + ":" + dist.GetProperty("integrity").GetString());
        foreach (var signature in dist.GetProperty("signatures").EnumerateArray())
        foreach (var key in keys.GetProperty("keys").EnumerateArray())
        {
            if (signature.GetProperty("keyid").GetString() != key.GetProperty("keyid").GetString()) continue;
            if (key.TryGetProperty("expires", out var expiry) && expiry.ValueKind == JsonValueKind.String && DateTimeOffset.Parse(expiry.GetString()!, System.Globalization.CultureInfo.InvariantCulture) <= now) continue;
            using var algorithm = ECDsa.Create(); algorithm.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key.GetProperty("key").GetString()!), out _);
            if (algorithm.VerifyData(payload, Convert.FromBase64String(signature.GetProperty("sig").GetString()!), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)) return true;
        }
        return false;
    }
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > 16 * 1024 * 1024) throw new InvalidDataException("XML size exceeded.");
        using var stream = entry.Open(); using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 }); return XDocument.Load(reader);
    }
    internal static async Task<string> VerifyMsixContentsAsync(string path, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count > 30000) throw new InvalidDataException("Package entry limit exceeded.");
        var entries = new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(e => e.Name.Length > 0))
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Split('/').Any(p => p is "." or ".." || p.Contains(':')) || !entries.TryAdd(name, entry)) throw new InvalidDataException("Unsafe or duplicate package entry.");
        }
        var map = ReadXml(entries["AppxBlockMap.xml"]); XNamespace ns = "http://schemas.microsoft.com/appx/2010/blockmap";
        if (map.Root?.Attribute("HashMethod")?.Value != "http://www.w3.org/2001/04/xmlenc#sha256") throw new InvalidDataException("Unsupported block hash.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var item in map.Root.Elements(ns + "File"))
        {
            ct.ThrowIfCancellationRequested(); var name = item.Attribute("Name")!.Value.Replace('\\', '/');
            // MSIX OPC entries use percent escapes; the block map uses logical names.
            var entry = entries.Values.SingleOrDefault(e => Uri.UnescapeDataString(e.FullName.Replace('\\', '/')) == name) ?? throw new InvalidDataException("Missing signed package file.");
            if (!names.Add(name) || entry.Length != long.Parse(item.Attribute("Size")!.Value, System.Globalization.CultureInfo.InvariantCulture) || entry.Length < 0 || (total = checked(total + entry.Length)) > 12L * CacheStorage.GiB) throw new InvalidDataException("Invalid package inventory.");
            await using var input = entry.Open(); var remaining = entry.Length; var buffer = new byte[65536];
            foreach (var block in item.Elements(ns + "Block"))
            {
                if (remaining <= 0) throw new InvalidDataException("Excess package blocks.");
                var count = (int)Math.Min(remaining, buffer.Length); await input.ReadExactlyAsync(buffer.AsMemory(0, count), ct);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(buffer.AsSpan(0, count)), Convert.FromBase64String(block.Attribute("Hash")!.Value))) throw new InvalidDataException("MSIX 内容与签名块不一致 / MSIX block mismatch.");
                remaining -= count;
            }
            if (remaining != 0) throw new InvalidDataException("Missing package blocks.");
        }
        if (!names.Contains("AppxManifest.xml") || entries.Values.Any(e => e.FullName.StartsWith("app/", StringComparison.OrdinalIgnoreCase) && !names.Contains(Uri.UnescapeDataString(e.FullName)))) throw new InvalidDataException("Unsigned package payload.");
        var manifest = ReadXml(entries["AppxManifest.xml"]); var identity = manifest.Root?.Elements().SingleOrDefault(e => e.Name.LocalName == "Identity");
        var version = identity?.Attribute("Version")?.Value;
        if (identity?.Attribute("Name")?.Value != WindowsDiscovery.PackageName || identity.Attribute("Publisher")?.Value != WindowsDiscovery.Publisher || identity.Attribute("ProcessorArchitecture")?.Value != "x64" || !Version.TryParse(version, out _)) throw new InvalidDataException("MSIX 身份或架构不匹配 / Wrong MSIX identity.");
        return version!;
    }
    public static async Task<string> HashAsync(string path, CancellationToken ct)
    { Data.NoLinks(path); await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)); }
}
public static class WindowsTrust
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle, Subject; }
    [StructLayout(LayoutKind.Sequential)] private struct TrustData { public uint Size; public IntPtr Policy, Sip; public uint Ui, Revocation, Choice; public IntPtr File; public uint StateAction; public IntPtr State, Url; public uint Flags, Context; public IntPtr Signature; }
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    public static void Verify(string path)
    {
        Data.NoLinks(path);
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = Path.GetFullPath(path) };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>()); Marshal.StructureToPtr(file, pointer, false);
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Revocation = 1, Choice = 1, File = pointer, StateAction = 1, Flags = 0x80 };
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try { var result = WinVerifyTrust(new IntPtr(-1), ref action, ref data); if (result != 0) throw new InvalidDataException($"Windows 签名校验失败（0x{result:X8}）。检查系统时间、证书和网络后重试。"); }
        finally { data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data); Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }
}
