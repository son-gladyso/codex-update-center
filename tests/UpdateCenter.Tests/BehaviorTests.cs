using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UpdateCenter.Tests;

public sealed class Fixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "cuc-public-tests", Guid.NewGuid().ToString("N"));
    public Fixture() => Directory.CreateDirectory(Root);
    public string File(string name, byte[]? content = null) { var path = Path.Combine(Root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); System.IO.File.WriteAllBytes(path, content ?? [1, 2, 3]); return path; }
    public void Dispose() { if (Directory.Exists(Root) && Data.Within(Root, Path.Combine(Path.GetTempPath(), "cuc-public-tests"))) Directory.Delete(Root, true); }
}
public sealed class DiscoveryTests
{
    private sealed class EmptyAppxRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(Command command, TimeSpan timeout, CancellationToken ct) => Task.FromResult(new CommandResult(0, "[]", ""));
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("{\"name\":123,\"version\":\"1.0.0\"}")]
    [InlineData("{\"name\":\"@openai/codex\",\"version\":123}")]
    [InlineData("null")]
    public async Task MalformedManifestDoesNotHideHealthyInstallation(string malformed)
    {
        using var f = new Fixture();
        f.File(@"broken\node_modules\@openai\codex\package.json", Encoding.UTF8.GetBytes(malformed));
        f.File(@"healthy\node_modules\@openai\codex\package.json", Encoding.UTF8.GetBytes("{\"name\":\"@openai/codex\",\"version\":\"1.0.0\"}"));
        var healthy = Path.Combine(f.Root, "healthy");
        var result = await new WindowsDiscovery(new EmptyAppxRunner(), [Path.Combine(f.Root, "broken"), healthy]).DiscoverAsync(default);
        Assert.Contains(result.Installations, install => install.Location == healthy && install.Version == "1.0.0");
        Assert.Contains(result.Warnings, warning => warning.Contains("无法可靠识别"));
    }
    private static AppPackage Package() => new("OpenAI.Codex", WindowsDiscovery.Publisher, "26.1.2.3", "X64", "OpenAI.Codex_official", @"C:\Program Files\WindowsApps\OpenAI.Codex_26", "Store", false);
    [Fact] public void OneOfficialInstallNeedsNoPrivateLayout() => Assert.Equal(InstallKind.DesktopMsix, WindowsDiscovery.FromAppx(Package()).Kind);
    [Theory] [InlineData("publisher")] [InlineData("name")] [InlineData("architecture")] [InlineData("developer")] [InlineData("signature")]
    public void SimilarLookingPackagesAreNeverClaimed(string change)
    {
        var p = Package(); p = change switch { "publisher" => p with { Publisher = "CN=attacker" }, "name" => p with { Name = "OpenAI.Codex.Other" }, "architecture" => p with { Architecture = "Arm64" }, "developer" => p with { IsDevelopmentMode = true }, _ => p with { SignatureKind = "Developer" } };
        Assert.Equal(InstallKind.Unsupported, WindowsDiscovery.FromAppx(p).Kind);
    }
    [Fact] public void TwoNpmPrefixesRemainIndependentAndUnicodePathsWork()
    {
        using var f = new Fixture();
        f.File(@"one space\node_modules\@openai\codex\package.json", Encoding.UTF8.GetBytes("{\"name\":\"@openai/codex\",\"version\":\"0.160.0\"}"));
        f.File(@"中文二\node_modules\@openai\codex\package.json", Encoding.UTF8.GetBytes("{\"name\":\"@openai/codex\",\"version\":\"0.162.0\"}"));
        var one = WindowsDiscovery.FromNpm(Path.Combine(f.Root, "one space"), true)!; var two = WindowsDiscovery.FromNpm(Path.Combine(f.Root, "中文二"), true)!;
        Assert.NotEqual(one.Id, two.Id); Assert.Equal("0.160.0", one.Version); Assert.Equal(InstallKind.Npm, two.Kind);
        Assert.Null(WindowsDiscovery.FromNpm(Path.Combine(f.Root, "absent"), true));
    }
    [Fact] public void MissingNpmToolDoesNotAdvertiseAutomaticUpdate()
    {
        using var f = new Fixture(); f.File(@"node_modules\@openai\codex\package.json", Encoding.UTF8.GetBytes("{\"name\":\"@openai/codex\",\"version\":\"0.162.0\"}"));
        Assert.Equal(InstallKind.Unsupported, WindowsDiscovery.FromNpm(f.Root, false)!.Kind);
    }
}
public sealed class StorageTests
{
    [Fact] public void CWithFullBudgetWinsOverMuchLargerD()
    {
        var c = new Volume(@"C:\", 40 * CacheStorage.GiB, 500 * CacheStorage.GiB, true); var d = new Volume(@"D:\", 200 * CacheStorage.GiB, 500 * CacheStorage.GiB, true);
        Assert.Equal(c, CacheStorage.Choose([d, c], 10 * CacheStorage.GiB));
    }
    [Fact] public void CReserveIsNotSpentToMakeDownloadFit()
    {
        var c = new Volume(@"C:\", 25 * CacheStorage.GiB, 100 * CacheStorage.GiB, true); var d = new Volume(@"D:\", 100 * CacheStorage.GiB, 500 * CacheStorage.GiB, true);
        Assert.Equal(d, CacheStorage.Choose([c, d], 10 * CacheStorage.GiB));
        Assert.Throws<IOException>(() => CacheStorage.Choose([c, d with { Internal = false }], 10 * CacheStorage.GiB));
    }
    [Fact] public void ExactBudgetBoundaryIsAccepted() { var c = new Volume(@"C:\", 30 * CacheStorage.GiB, 100 * CacheStorage.GiB, true); Assert.Equal(c, CacheStorage.Choose([c], 10 * CacheStorage.GiB)); }
}
public sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(handle(request)); }
public sealed class NetworkTests
{
    [Fact] public async Task TransientMetadataTransportFailureIsRetried()
    {
        var requests=0;
        using var network=new OfficialNetwork(new Handler(_=> ++requests == 1 ? throw new HttpRequestException("connection reset") : new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("ok") }));
        Assert.Equal("ok",await network.TextAsync("https://registry.npmjs.org/test",default)); Assert.Equal(2,requests);
    }
    [Fact] public async Task PermanentMetadataFailureIsNotRetried()
    {
        var requests=0; using var network=new OfficialNetwork(new Handler(_=> {requests++; return new HttpResponseMessage(HttpStatusCode.Forbidden);}));
        await Assert.ThrowsAsync<HttpRequestException>(()=>network.TextAsync("https://registry.npmjs.org/test",default)); Assert.Equal(1,requests);
    }
    private const string Url = "https://registry.npmjs.org/@openai/codex/-/codex-0.162.1.tgz";
    private static ReleaseOffer Offer() => new(InstallKind.Npm, "0.162.1", Url, 1024);
    [Theory] [InlineData("http://registry.npmjs.org/a")] [InlineData("https://registry.npmjs.org.attacker.test/a")] [InlineData("https://user@registry.npmjs.org/a")] [InlineData("https://registry.npmjs.org:8443/a")] [InlineData("https://127.0.0.1/a")]
    public void SourceBoundaryRejectsLookalikesAndDowngrades(string value) => Assert.Throws<InvalidDataException>(() => OfficialNetwork.Validate(new(value)));
    [Fact] public async Task RedirectCannotLeaveOfficialAllowlist()
    {
        var requests = 0; using var network = new OfficialNetwork(new Handler(_ => { requests++; var r = new HttpResponseMessage(HttpStatusCode.Redirect); r.Headers.Location = new("https://attacker.test/package"); return r; }));
        await Assert.ThrowsAsync<InvalidDataException>(() => network.TextAsync(Url, default)); Assert.Equal(1, requests);
    }
    [Fact] public async Task RealSavedPrefixIsResumedWithoutDuplicatingBytes()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Root, "package.tgz"); f.File("package.tgz.partial", [10,20]); Data.Write(path + ".checkpoint.json", new DownloadCheckpoint(Url, "\"stable\"", 4));
        using var network = new OfficialNetwork(new Handler(request => {
            Assert.Equal(2, request.Headers.Range!.Ranges.Single().From); Assert.Equal("\"stable\"", request.Headers.IfRange!.EntityTag!.Tag);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent([30,40]) }; response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2,3,4); response.Headers.ETag = new("\"stable\""); return response;
        }));
        await new PackageDownloader(network).DownloadAsync(Offer(), path, null, default);
        Assert.Equal(new byte[] {10,20,30,40}, await System.IO.File.ReadAllBytesAsync(path));
    }
    [Theory] [InlineData("etag")] [InlineData("offset")] [InlineData("status")]
    public async Task ResumeMismatchPreservesOriginalBytesAndNeverCompletes(string mismatch)
    {
        using var f = new Fixture(); var path = Path.Combine(f.Root, "package.tgz"); f.File("package.tgz.partial", [10,20]); Data.Write(path + ".checkpoint.json", new DownloadCheckpoint(Url, "\"stable\"", 4));
        using var network = new OfficialNetwork(new Handler(_ => { var r = new HttpResponseMessage(mismatch == "status" ? HttpStatusCode.OK : HttpStatusCode.PartialContent) { Content = new ByteArrayContent([30,40]) }; r.Headers.ETag = new(mismatch == "etag" ? "\"changed\"" : "\"stable\""); r.Content.Headers.ContentRange = new(mismatch == "offset" ? 1 : 2,3,4); return r; }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new PackageDownloader(network).DownloadAsync(Offer(), path, null, default));
        Assert.False(System.IO.File.Exists(path)); Assert.Equal(new byte[] {10,20}, await System.IO.File.ReadAllBytesAsync(path + ".partial"));
    }
    [Fact] public async Task CrashAfterLastWriteCanPromoteCompletedPartialWithoutInvalidRangeRequest()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Root, "package.tgz"); f.File("package.tgz.partial", [1,2,3]); Data.Write(path + ".checkpoint.json", new DownloadCheckpoint(Url, "\"stable\"", 3));
        using var network = new OfficialNetwork(new Handler(_ => throw new InvalidOperationException("Network must not be called")));
        await new PackageDownloader(network).DownloadAsync(Offer(), path, null, default); Assert.Equal(3, new FileInfo(path).Length);
    }
}
public sealed class SequenceSource(params DiscoveryResult[] values) : IInstallationSource
{ private int index; public Task<DiscoveryResult> DiscoverAsync(CancellationToken ct) => Task.FromResult(values[Math.Min(index++, values.Length - 1)]); }
public sealed class FakePlatform : IUpdatePlatform
{
    public int Installs; public bool Busy; public bool Fail; public Action? DuringInstall;
    public Task AssertIdleAsync(Installation target, CancellationToken ct) => Busy ? Task.FromException(new IOException("busy")) : Task.CompletedTask;
    public Task InstallAsync(PreparedUpdate prepared, CancellationToken ct) { Installs++; DuringInstall?.Invoke(); return Fail ? Task.FromException(new IOException("deployment failed")) : Task.CompletedTask; }
}
public sealed class TestVerifier : IPackageVerifier
{ public Task<string> VerifyAsync(string path, ReleaseOffer offer, CancellationToken ct) => Task.FromResult(offer.Version); }
public sealed class TransactionTests
{
    private static Installation Target(string root) => new("id", InstallKind.Npm, "Codex CLI", "0.160.0", root, "@openai/codex", "npm");
    private static async Task<PreparedUpdate> Prepared(Fixture f, Installation target) { var path = f.File("codex.tgz"); return new(target, new(InstallKind.Npm,"0.162.1","https://registry.npmjs.org/a",3), path, await PackageVerifier.HashAsync(path, default), DateTimeOffset.UtcNow); }
    [Theory] [InlineData("tampered")] [InlineData("changed-install")] [InlineData("busy")] [InlineData("downgrade")]
    public async Task UnsafePreconditionsNeverInvokeInstaller(string fault)
    {
        using var f = new Fixture(); var target = Target(f.Root); var package = await Prepared(f,target); var platform = new FakePlatform { Busy = fault == "busy" };
        if (fault == "tampered") System.IO.File.WriteAllBytes(package.Package,[9,9]);
        if (fault == "downgrade") package = package with { Offer = package.Offer with { Version = "0.159.0" } };
        var current = fault == "changed-install" ? target with { Version = "0.161.0" } : target;
        var service = new UpdateService(new(f.Root), new SequenceSource(new DiscoveryResult([current],[])), platform, new TestVerifier());
        await Assert.ThrowsAnyAsync<Exception>(() => service.ApplyAsync(package, default)); Assert.Equal(0,platform.Installs);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task FailedInstallerOrWrongPostVersionCannotClaimSuccess(bool fail)
    {
        using var f = new Fixture(); var target = Target(f.Root); var state = new UpdateState(f.Root); var platform = new FakePlatform { Fail = fail };
        var service = new UpdateService(state, new SequenceSource(new DiscoveryResult([target],[])), platform, new TestVerifier());
        var package = await Prepared(f,target); await Assert.ThrowsAsync<IOException>(() => service.ApplyAsync(package,default));
        Assert.Equal(1,platform.Installs); Assert.Equal("NeedsReview",state.Read()!.Phase);
    }
    [Fact] public async Task SuccessRequiresRediscoveryAndPackageCannotChangeDuringInstaller()
    {
        using var f = new Fixture(); var target = Target(f.Root); var package = await Prepared(f,target); var state = new UpdateState(f.Root);
        var platform = new FakePlatform { DuringInstall = () => Assert.Throws<IOException>(() => System.IO.File.WriteAllBytes(package.Package,[9])) };
        await new UpdateService(state,new SequenceSource(new([target],[]),new([target with { Version="0.162.1" }],[])),platform,new TestVerifier()).ApplyAsync(package,default);
        Assert.Equal("Completed",state.Read()!.Phase); Assert.Equal(1,platform.Installs);
    }
    [Fact] public void SecondProcessCannotAcquireActiveOperation()
    { using var f = new Fixture(); var state = new UpdateState(f.Root); using var lease = state.Lock(); Assert.Throws<IOException>(() => new UpdateState(f.Root).Lock()); }
    [Fact] public void FutureJournalSchemaFailsClosed()
    { using var f = new Fixture(); Data.Write(Path.Combine(f.Root,"job.json"),new Journal(999,"Completed",null,"fake",DateTimeOffset.UtcNow)); Assert.Throws<InvalidDataException>(() => new UpdateState(f.Root).Read()); }
}
