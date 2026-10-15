using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UpdateCenter.Tests;

public sealed class VerificationTests
{
    [Theory] [InlineData("valid",true)] [InlineData("tampered",false)] [InlineData("expired",false)] [InlineData("wrong-key",false)]
    public void RegistrySignaturesBindExactIdentityIntegrityAndUnexpiredKey(string mode, bool expected)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); const string integrity = "sha512-dGVzdA==";
        var payload = Encoding.UTF8.GetBytes("@openai/codex@0.162.1:" + integrity);
        var signature = Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence));
        using var metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { name="@openai/codex", version = mode=="tampered" ? "0.162.2" : "0.162.1", dist = new { integrity, signatures = new[] { new { keyid="trusted", sig=signature } } } }));
        using var keys = JsonDocument.Parse(JsonSerializer.Serialize(new { keys = new[] {new { keyid=mode=="wrong-key" ? "other":"trusted", key=Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), expires=DateTimeOffset.UtcNow.AddDays(mode=="expired" ? -1:1).ToString("O") }} }));
        Assert.Equal(expected,PackageVerifier.VerifyNpmSignature(metadata.RootElement,keys.RootElement,DateTimeOffset.UtcNow));
    }
    internal static string Msix(Fixture f, string publisher, bool tamper = false, bool unsignedExtra = false)
    {
        var path = Path.Combine(f.Root,"fixture.msix");
        var manifest = Encoding.UTF8.GetBytes($"<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\"><Identity Name=\"OpenAI.Codex\" Publisher=\"{publisher}\" Version=\"26.1007.9446.0\" ProcessorArchitecture=\"x64\" /></Package>");
        var payload = Encoding.UTF8.GetBytes("test application");
        string Item(string name,byte[] bytes) => $"<File Name=\"{name}\" Size=\"{bytes.Length}\"><Block Hash=\"{Convert.ToBase64String(SHA256.HashData(bytes))}\" /></File>";
        var map = $"<BlockMap xmlns=\"http://schemas.microsoft.com/appx/2010/blockmap\" HashMethod=\"http://www.w3.org/2001/04/xmlenc#sha256\">{Item("AppxManifest.xml",manifest)}{Item("app/ChatGPT.exe",payload)}</BlockMap>";
        using var zip = ZipFile.Open(path,ZipArchiveMode.Create);
        void Add(string name, byte[] content) { using var output = zip.CreateEntry(name).Open(); output.Write(content); }
        Add("AppxManifest.xml",manifest); Add("AppxBlockMap.xml",Encoding.UTF8.GetBytes(map)); Add("app/ChatGPT.exe",tamper ? Encoding.UTF8.GetBytes("evil application") : payload);
        if (unsignedExtra) Add("app/injected.dll",[1,2,3]); return path;
    }
    [Fact] public async Task SignedInventoryContentParserAcceptsCompleteFixture()
    { using var f = new Fixture(); Assert.Equal("26.1007.9446.0",await PackageVerifier.VerifyMsixContentsAsync(Msix(f,WindowsDiscovery.Publisher),default)); }
    [Theory] [InlineData("publisher")] [InlineData("tamper")] [InlineData("unsigned")]
    public async Task PackageInventoryRejectsWrongIdentityTamperingAndUnsignedPayload(string mode)
    { using var f = new Fixture(); var path=Msix(f,mode=="publisher" ? "CN=Untrusted":WindowsDiscovery.Publisher,mode=="tamper",mode=="unsigned"); await Assert.ThrowsAsync<InvalidDataException>(()=>PackageVerifier.VerifyMsixContentsAsync(path,default)); }
    [Fact] public void ActualWindowsTrustRejectsUnsignedMsixEvenWithValidBlockMap()
    { using var f = new Fixture(); Assert.Throws<InvalidDataException>(()=>WindowsTrust.Verify(Msix(f,WindowsDiscovery.Publisher))); }
    [Fact] public async Task NpmTamperingFailsBeforeAnyNetworkLookup()
    {
        using var f = new Fixture(); var path=f.File("codex.tgz",[1,2,3]); using var network=new OfficialNetwork(new Handler(_=>throw new Exception("Should not contact registry")));
        var offer=new ReleaseOffer(InstallKind.Npm,"0.162.1","https://registry.npmjs.org/a",3,"sha512-"+Convert.ToBase64String(SHA512.HashData(new byte[]{9})),"{}");
        await Assert.ThrowsAsync<InvalidDataException>(()=>new PackageVerifier(network,(_,_)=>Task.CompletedTask).VerifyAsync(path,offer,default));
    }
}
public sealed class ProcessTests
{
    [Fact] public void NpmUsesEffectiveSystemProxyWithoutDisablingTls()
    {
        var values=WindowsPlatform.NpmEnvironment(new System.Net.WebProxy("http://127.0.0.1:12345"));
        Assert.Equal("http://127.0.0.1:12345/",values["NPM_CONFIG_HTTPS_PROXY"]);
        Assert.Equal("true",values["NPM_CONFIG_STRICT_SSL"]);
        Assert.False(WindowsPlatform.NpmEnvironment(new System.Net.WebProxy()).ContainsKey("NPM_CONFIG_HTTPS_PROXY"));
    }
    [Fact] public async Task RealPowerShellGetsUnicodeAndMetacharactersAsData()
    {
        const string value="中文 path ' ; $(throw 'injected') & |\nline two";
        var result=await new CommandRunner().RunAsync(CommandRunner.PowerShell("$p=[Console]::In.ReadToEnd() | ConvertFrom-Json; [Console]::Write($p.Value)",new {Value=value}),TimeSpan.FromSeconds(20),default);
        Assert.Equal(0,result.ExitCode); Assert.Equal(value,result.Output);
    }
    [Fact] public async Task ActualHelperTimeoutStopsOwnedProcess()
    {
        using var f=new Fixture(); var marker=Path.Combine(f.Root,"unexpected.txt");
        var command=CommandRunner.PowerShell("$p=[Console]::In.ReadToEnd() | ConvertFrom-Json; Start-Sleep -Seconds 8; [IO.File]::WriteAllText($p.Marker,'unexpected')",new{Marker=marker});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new CommandRunner().RunAsync(command,TimeSpan.FromSeconds(1),default));
        Assert.False(System.IO.File.Exists(marker));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ActualPowerShellDiscoveryScriptPreservesArrayShape(int count)
    {
        // Exercise the Windows PowerShell 5.1 array serialization actually used by discovery.
        var fixture="$fixture=@(); for($i=0;$i -lt "+count+";$i++){ $fixture += [pscustomobject]@{Name='OpenAI.Codex';Publisher='"+WindowsDiscovery.Publisher+"';Version=[version]'26.1.2.3';Architecture='X64';PackageFamilyName=('family'+$i);InstallLocation='C:\\fixture';SignatureKind='Store';IsDevelopmentMode=$false} }; ";
        var script=fixture+WindowsDiscovery.AppxScript.Replace("Get-AppxPackage -Name 'OpenAI.Codex'","$fixture");
        var result=await new CommandRunner().RunAsync(CommandRunner.PowerShell(script),TimeSpan.FromSeconds(20),default);
        Assert.Equal(0,result.ExitCode); Assert.Equal(count,JsonSerializer.Deserialize<AppPackage[]>(result.Output,Data.Json)!.Length);
    }
    [Fact] public void NpmArgumentsCannotInjectShellAndUseVerifiedLocalTarball()
    {
        using var f=new Fixture(); var prefix=Path.Combine(f.Root,"npm 中文 & space"); var package=f.File("verified package.tgz");
        var command=WindowsPlatform.NpmCommand(new(f.File("node.exe"),f.File("npm-cli.js")),new("id",InstallKind.Npm,"CLI","0.161.0",prefix,"@openai/codex","npm"),new(InstallKind.Npm,"0.162.1","https://registry.npmjs.org/a",3),f.Root,Path.Combine(f.Root,"config"),package);
        Assert.Contains(prefix,command.Arguments); Assert.Contains(package,command.Arguments); Assert.Contains("--ignore-scripts",command.Arguments); Assert.DoesNotContain(command.Arguments,a=>a.Contains("@latest")); Assert.EndsWith("node.exe",command.Executable);
        Assert.Contains("@openai/codex-win32-x64@npm:@openai/codex@0.162.1-win32-x64",command.Arguments);
        Assert.Contains("--include=optional",command.Arguments);
    }
    private sealed class ProbeRunner(CommandResult result) : ICommandRunner
    {
        public Command? Seen;
        public Task<CommandResult> RunAsync(Command command, TimeSpan timeout, CancellationToken ct) { Seen=command; return Task.FromResult(result); }
    }
    [Theory] [InlineData(1,"",false)] [InlineData(0,"codex-cli 0.161.0",false)] [InlineData(0,"prefix codex-cli 0.162.1 suffix",false)] [InlineData(0,"codex-cli 0.162.1\r\n",true)]
    public async Task ActualCliVersionMustMatchBeforeSuccess(int exit, string output, bool success)
    {
        using var f=new Fixture(); var runner=new ProbeRunner(new(exit,output,""));
        var task=WindowsPlatform.VerifyNpmRuntimeAsync(runner,new(Path.Combine(f.Root,"node.exe"),"unused"),f.Root,"0.162.1",f.Root,default);
        if(success) await task; else await Assert.ThrowsAsync<IOException>(()=>task);
        Assert.Equal("--version",runner.Seen!.Arguments[1]);
        Assert.True(Data.Within(runner.Seen.Environment!["CODEX_HOME"],f.Root));
    }
}
