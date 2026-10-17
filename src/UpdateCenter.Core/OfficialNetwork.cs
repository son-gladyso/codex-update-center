using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace UpdateCenter;

public sealed class OfficialNetwork : IDisposable
{
    public const string DesktopUrl = "https://persistent.oaistatic.com/codex-app-prod/ChatGPT-x64.msix";
    private readonly HttpClient client;
    public OfficialNetwork(HttpMessageHandler? handler = null)
    {
        client = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None });
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexUpdateCenter-Community/" + Data.ProductVersion);
    }
    public static void Validate(Uri uri)
    {
        if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            !(uri.Host == "registry.npmjs.org" || uri.Host == "persistent.oaistatic.com"))
            throw new InvalidDataException("更新地址不在官方来源清单中 / Update source not allowed.");
    }
    internal async Task<HttpResponseMessage> SendAsync(Uri uri, HttpMethod method, long offset, string? etag, CancellationToken ct, long? rangeEnd = null)
    {
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            Validate(uri);
            using var request = new HttpRequestMessage(method, uri);
            if (offset > 0 || rangeEnd != null) request.Headers.Range = new RangeHeaderValue(offset, rangeEnd);
            if (offset > 0 && etag != null) request.Headers.IfRange = new RangeConditionHeaderValue(EntityTagHeaderValue.Parse(etag));
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                var location = response.Headers.Location; response.Dispose();
                if (location == null) throw new IOException("更新地址重定向缺少目标 / Invalid redirect.");
                uri = new Uri(uri, location); continue;
            }
            return response;
        }
        throw new IOException("更新地址重定向次数过多 / Too many redirects.");
    }
    public async Task<string> TextAsync(string url, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await TextOnceAsync(url, ct); }
            catch (Exception error) when (attempt < 2 && !ct.IsCancellationRequested &&
                (error is OperationCanceledException || error is HttpRequestException request &&
                (request.StatusCode == null || request.StatusCode == HttpStatusCode.RequestTimeout || (int)request.StatusCode >= 500)))
            { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct); }
        }
    }
    private async Task<string> TextOnceAsync(string url, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(45));
        using var response = await SendAsync(new(url), HttpMethod.Get, 0, null, deadline.Token); response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token); using var result = new MemoryStream();
        var buffer = new byte[16384]; int read;
        while ((read = await stream.ReadAsync(buffer, deadline.Token)) > 0)
        { if (result.Length + read > 4 * 1024 * 1024) throw new InvalidDataException("更新元数据过大 / Metadata size exceeded."); await result.WriteAsync(buffer.AsMemory(0, read), deadline.Token); }
        return System.Text.Encoding.UTF8.GetString(result.ToArray());
    }
    public async Task<ReleaseOffer> CheckAsync(InstallKind kind, CancellationToken ct)
    {
        if (kind == InstallKind.Npm)
        {
            var metadata = await TextAsync("https://registry.npmjs.org/@openai%2fcodex/latest", ct);
            using var document = JsonDocument.Parse(metadata); var value = document.RootElement;
            if (value.GetProperty("name").GetString() != "@openai/codex") throw new InvalidDataException("npm identity mismatch.");
            var version = value.GetProperty("version").GetString()!; _ = Data.Stable(version);
            var dist = value.GetProperty("dist"); var url = dist.GetProperty("tarball").GetString()!; Validate(new(url));
            if (new Uri(url).Host != "registry.npmjs.org") throw new InvalidDataException("Wrong npm host.");
            return new(kind, version, url, 64L * 1024 * 1024, dist.GetProperty("integrity").GetString(), metadata, SizeIsEstimate: true);
        }
        if (kind != InstallKind.DesktopMsix) throw new InvalidOperationException("Unsupported installation.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(45));
        using var head = await SendAsync(new(DesktopUrl), HttpMethod.Head, 0, null, deadline.Token); head.EnsureSuccessStatusCode();
        // Reserve the full transfer limit when HEAD supplies no length. A budget
        // is not an exact identity check; GET and the verifier establish the bytes.
        var size = head.Content.Headers.ContentLength ?? PackageDownloader.MaximumBytes;
        if (size <= 0 || size > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("Invalid package size.");
        return new(kind, "0.0.0.0", DesktopUrl, size, ETag: head.Headers.ETag?.ToString(), SizeIsEstimate: head.Content.Headers.ContentLength == null);
    }
    public void Dispose() => client.Dispose();
}

public sealed record DownloadCheckpoint(string Url, string? ETag, long? Length);
public sealed class PackageDownloader(OfficialNetwork network)
{
    public const long MaximumBytes = 4L * 1024 * 1024 * 1024;
    public async Task DownloadAsync(ReleaseOffer offer, string destination, IProgress<string>? progress, CancellationToken ct)
    {
        OfficialNetwork.Validate(new(offer.Url)); Data.NoLinks(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var partial = destination + ".partial"; var checkpointPath = destination + ".checkpoint.json";
        Data.NoLinks(partial); Data.NoLinks(checkpointPath);
        if (File.Exists(destination)) return; // The caller always re-verifies the completed file.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var saved = Data.Read<DownloadCheckpoint>(checkpointPath);
                var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                if (offset > 0 && (saved == null || saved.Url != offer.Url || string.IsNullOrEmpty(saved.ETag) || saved.ETag.StartsWith("W/", StringComparison.Ordinal)))
                    throw new InvalidDataException("已有断点缺少可验证的包身份，请保留现场并重新准备到新目录。");
                if (offset > MaximumBytes || saved?.Length is <= 0 or > MaximumBytes ||
                    offset > 0 && (offer.ETag != null && saved!.ETag != offer.ETag ||
                        offer.Kind == InstallKind.DesktopMsix && !offer.SizeIsEstimate && saved!.Length != null && saved.Length != offer.Size))
                    throw new InvalidDataException("已有断点不符合候选身份或大小限制，请保留现场并重新准备。");
                if (offset > 0 && offset == saved!.Length) { File.Move(partial, destination); return; }
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromHours(2));
                using var response = await network.SendAsync(new(offer.Url), HttpMethod.Get, offset, saved?.ETag, deadline.Token);
                response.EnsureSuccessStatusCode();
                var etag = response.Headers.ETag?.ToString();
                var range = response.Content.Headers.ContentRange;
                long? total = offset > 0 ? range?.Length : response.Content.Headers.ContentLength;
                if (offset > 0 && (response.StatusCode != HttpStatusCode.PartialContent || response.Content.Headers.ContentRange?.From != offset ||
                    range?.Unit != "bytes" || total == null || range?.To != total - 1 || etag != saved!.ETag ||
                    saved.Length != null && total != saved.Length ||
                    response.Content.Headers.ContentLength is long remaining && remaining != total - offset))
                    throw new InvalidDataException("官方安装包已变化或服务器不支持续传。已有文件保留，请选择重新准备。");
                if (offset == 0 && response.StatusCode != HttpStatusCode.OK) throw new InvalidDataException("Unexpected download status.");
                if (total is <= 0 or > MaximumBytes || total != null && offset > total) throw new InvalidDataException("Invalid download size.");
                if (offer.Kind == InstallKind.DesktopMsix)
                {
                    if (!offer.SizeIsEstimate && total != null && total != offer.Size || offer.ETag != null && etag != offer.ETag)
                        throw new InvalidDataException("更新包在查询后已变化，请重新检查。");
                    if (!offer.SizeIsEstimate) total ??= offer.Size;
                }
                Data.Write(checkpointPath, new DownloadCheckpoint(offer.Url, etag, total));
                await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
                await using (var output = new FileStream(partial, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 131072, true))
                {
                    output.Position = offset; var buffer = new byte[131072]; var last = DateTimeOffset.MinValue;
                    while (true)
                    {
                        using var stall = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token); stall.CancelAfter(TimeSpan.FromSeconds(60));
                        var read = await input.ReadAsync(buffer, stall.Token);
                        if (read == 0)
                        {
                            if (offset == 0 || total != null && offset != total) throw new IOException("下载提前结束 / Truncated download.");
                            break;
                        }
                        if (offset + read > MaximumBytes || total != null && offset + read > total)
                            throw new InvalidDataException("Artifact size limit or declared length exceeded.");
                        await output.WriteAsync(buffer.AsMemory(0, read), deadline.Token); offset += read;
                        if (DateTimeOffset.UtcNow - last > TimeSpan.FromMilliseconds(250))
                        {
                            progress?.Report(total is long length ? $"下载 {offset / 1048576.0:F1} / {length / 1048576.0:F1} MiB · {offset * 100.0 / length:F1}%" : $"已下载 {offset / 1048576.0:F1} MiB · 总大小未知");
                            last = DateTimeOffset.UtcNow;
                        }
                    }
                    await output.FlushAsync(deadline.Token); output.Flush(true);
                }
                // Only a clean EOF records an actual length for an unknown-size
                // transfer. Cancellation retains null and cannot promote a prefix.
                Data.Write(checkpointPath, new DownloadCheckpoint(offer.Url, etag, offset));
                File.Move(partial, destination); return;
            }
            catch (Exception e) when (attempt < 2 && !ct.IsCancellationRequested && (e is HttpRequestException or IOException or OperationCanceledException))
            { progress?.Report("连接中断，保留断点后重试…"); await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), ct); }
        }
    }
}
