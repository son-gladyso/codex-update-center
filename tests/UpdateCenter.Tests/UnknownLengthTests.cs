using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace UpdateCenter.Tests;

public sealed class UnknownLengthTests
{
    private sealed class Unseekable(byte[] bytes) : MemoryStream(bytes) { public override bool CanSeek => false; }
    private static HttpResponseMessage Response(byte[] bytes, bool known = false)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        { Content = known ? new ByteArrayContent(bytes) : new StreamContent(new Unseekable(bytes)) };
        response.Headers.ETag = new("\"one\"");
        return response;
    }
    private static ReleaseOffer Desktop(long size, bool estimate) => new(InstallKind.DesktopMsix, "0.0.0.0", OfficialNetwork.DesktopUrl, size, ETag: "\"one\"", SizeIsEstimate: estimate);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingHeadLengthDownloadsActualBytesAndRecordsCleanEof(bool getHasLength)
    {
        using var f = new Fixture();
        using var network = new OfficialNetwork(new Handler(request => request.Method == HttpMethod.Head ? Response([]) : Response([1, 2, 3], getHasLength)));
        var offer = await network.CheckAsync(InstallKind.DesktopMsix, default);
        Assert.True(offer.SizeIsEstimate);
        Assert.Equal(PackageDownloader.MaximumBytes, offer.Size);
        var path = Path.Combine(f.Root, "package");
        await new PackageDownloader(network).DownloadAsync(offer, path, null, default);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.Equal(3, Data.Read<DownloadCheckpoint>(path + ".checkpoint.json")!.Length);
    }

    [Fact]
    public async Task ExactHeadLengthStillWorksWhenGetLengthIsMissing()
    {
        using var f = new Fixture(); using var network = new OfficialNetwork(new Handler(_ => Response([1, 2, 3])));
        var path = Path.Combine(f.Root, "package");
        await new PackageDownloader(network).DownloadAsync(Desktop(3, false), path, null, default);
        Assert.Equal(3, new FileInfo(path).Length);
    }

    [Theory]
    [InlineData(536870912)]
    [InlineData(2147483648)]
    public async Task KnownSizesHaveNoMagicValueExemption(long size)
    {
        using var f = new Fixture(); using var network = new OfficialNetwork(new Handler(_ => Response([1, 2, 3], true)));
        var path = Path.Combine(f.Root, "package");
        await Assert.ThrowsAsync<InvalidDataException>(() => new PackageDownloader(network).DownloadAsync(Desktop(size, false), path, null, default));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task UnknownLengthDoesNotIgnoreOfferEtag()
    {
        using var f = new Fixture(); using var network = new OfficialNetwork(new Handler(_ => { var response = Response([1]); response.Headers.ETag = new("\"changed\""); return response; }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new PackageDownloader(network).DownloadAsync(Desktop(PackageDownloader.MaximumBytes, true), Path.Combine(f.Root, "package"), null, default));
    }

    private sealed class CancelProgress(CancellationTokenSource source) : IProgress<string>
    { public void Report(string value) => source.Cancel(); }

    [Fact]
    public async Task CancelledUnknownLengthPrefixResumesWithVerifiedRange()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Root, "package");
        var bytes = Enumerable.Range(0, 200000).Select(i => (byte)i).ToArray();
        using (var source = new CancellationTokenSource())
        using (var network = new OfficialNetwork(new Handler(_ => Response(bytes))))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PackageDownloader(network).DownloadAsync(Desktop(PackageDownloader.MaximumBytes, true), path, new CancelProgress(source), source.Token));
        }
        Assert.False(File.Exists(path));
        Assert.Null(Data.Read<DownloadCheckpoint>(path + ".checkpoint.json")!.Length);
        var prefix = File.ReadAllBytes(path + ".partial");
        Assert.InRange(prefix.Length, 1, bytes.Length - 1);
        using var resumed = new OfficialNetwork(new Handler(request =>
        {
            Assert.Equal(prefix.Length, request.Headers.Range!.Ranges.Single().From);
            Assert.Equal("\"one\"", request.Headers.IfRange!.EntityTag!.Tag);
            var response = Response(bytes[prefix.Length..], true);
            response.StatusCode = HttpStatusCode.PartialContent;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(prefix.Length, bytes.Length - 1, bytes.Length);
            return response;
        }));
        await new PackageDownloader(resumed).DownloadAsync(Desktop(PackageDownloader.MaximumBytes, true), path, null, default);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task UnknownStreamCannotExceedKnownLength()
    {
        using var f = new Fixture(); using var network = new OfficialNetwork(new Handler(_ => Response([1, 2, 3, 4])));
        var path = Path.Combine(f.Root, "package");
        await Assert.ThrowsAsync<InvalidDataException>(() => new PackageDownloader(network).DownloadAsync(Desktop(3, false), path, null, default));
        Assert.False(File.Exists(path));
    }
}
