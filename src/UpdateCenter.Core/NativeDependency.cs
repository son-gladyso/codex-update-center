using System.Net;
using System.Text.Json;

namespace UpdateCenter;

public static class NativeDependency
{
    // Native tarballs can be much larger than the npm launcher package. Bounded
    // ranges avoid repeatedly discarding a whole transfer through a slow proxy.
    internal static async Task DownloadAsync(OfficialNetwork network, string url, string path, CancellationToken ct, IProgress<string>? progress = null)
    {
        const long chunk = 1024 * 1024, maximum = 1024L * 1024 * 1024;
        Data.NoLinks(path); Data.NoLinks(path + ".partial");
        if (File.Exists(path)) return; // Caller must verify the exact signed integrity.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var output = new FileStream(path + ".partial", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 131072, true))
        {
            output.Position = output.Length;
            long? total = null; var failures = 0;
            while (total == null || output.Length < total)
            {
                ct.ThrowIfCancellationRequested();
                var offset = output.Length;
                if (offset >= maximum) throw new InvalidDataException("Native package size limit exceeded.");
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromMinutes(2));
                    using var response = await network.SendAsync(new(url), HttpMethod.Get, offset, null, deadline.Token, Math.Min(offset + chunk - 1, (total ?? maximum) - 1));
                    var range = response.Content.Headers.ContentRange;
                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && range?.Length == offset && offset > 0) break;
                    response.EnsureSuccessStatusCode();
                    if (response.StatusCode != HttpStatusCode.PartialContent || range?.From != offset || range.To == null || range.Length is not > 0 ||
                        range.Length > maximum || range.To < offset || range.To >= range.Length || range.To - offset + 1 > chunk || total != null && total != range.Length)
                        throw new InvalidDataException("Native package range response mismatch.");
                    total = range.Length;
                    await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
                    var buffer = new byte[131072]; var remaining = range.To.Value - offset + 1;
                    while (remaining > 0)
                    {
                        var count = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), deadline.Token);
                        if (count == 0) throw new IOException("Truncated native package range.");
                        await output.WriteAsync(buffer.AsMemory(0, count), deadline.Token); remaining -= count;
                    }
                    await output.FlushAsync(deadline.Token); output.Flush(true); failures = 0;
                    progress?.Report($"原生组件 {output.Length / 1048576.0:F1} / {total.Value / 1048576.0:F1} MiB · 支持暂停续传");
                }
                catch (Exception error) when (!ct.IsCancellationRequested && failures++ < 2 && error is HttpRequestException or IOException or OperationCanceledException)
                { await Task.Delay(TimeSpan.FromSeconds(2 * failures), ct); }
            }
            output.Flush(true);
        }
        File.Move(path + ".partial", path);
    }

    public static async Task CacheAsync(ICommandRunner runner, NpmTool tool, string version, string cache, string config, string directory, CancellationToken ct, IProgress<string>? progress = null)
    {
        _ = Data.Stable(version); Data.NoLinks(cache); Data.NoLinks(directory);
        using var network = new OfficialNetwork();
        var nativeVersion = version + "-win32-x64";
        var metadata = await network.TextAsync("https://registry.npmjs.org/@openai%2fcodex/" + nativeVersion, ct);
        using var json = JsonDocument.Parse(metadata); var value = json.RootElement; var dist = value.GetProperty("dist");
        var url = dist.GetProperty("tarball").GetString()!;
        if (new Uri(url).Host != "registry.npmjs.org") throw new InvalidDataException("Native package registry mismatch.");
        var offer = new ReleaseOffer(InstallKind.Npm, nativeVersion, url, 256 * 1024 * 1024, dist.GetProperty("integrity").GetString(), metadata);
        var path = Path.Combine(directory, "native-" + nativeVersion + ".tgz");
        CacheStorage.RequireSpace(directory, 2 * CacheStorage.GiB);
        await DownloadAsync(network, url, path, ct, progress);
        progress?.Report("原生组件已下载，正在校验完整哈希和官方发布签名…");
        await using var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await new PackageVerifier(network, (_, _) => Task.CompletedTask).VerifyNativeAsync(path, offer, ct);
        var command = new Command(tool.Node, [tool.Cli, "cache", "add", path, "--cache", cache, "--ignore-scripts", "--userconfig", config, "--globalconfig", config + ".global"], Environment: WindowsPlatform.NpmEnvironment(HttpClient.DefaultProxy));
        var result = await runner.RunAsync(command, TimeSpan.FromMinutes(3), ct);
        if (result.ExitCode != 0) throw new IOException("无法准备已验签的 npm 原生组件缓存 / Native npm cache preparation failed.");
    }
}
