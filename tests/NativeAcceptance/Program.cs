using System.Diagnostics;
using System.Text.Json;
using UpdateCenter;

// Explicit opt-in executable, never part of the ordinary test suite. All writes stay
// in a newly created acceptance directory; existing installations are read-only.
if (args.Length is < 1 or > 3) throw new ArgumentException("NativeAcceptance <new-absolute-directory> [existing-official.msix] [previous-artifact-directory]");
var root=Path.GetFullPath(args[0]); Data.NoLinks(root);
if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Acceptance output must not exist.");
Directory.CreateDirectory(root);
// Reusing completed public package bytes avoids repeating large transfers when
// validating a later build. Every package is still verified before use.
if (args.Length == 3)
{
    var previous=Path.GetFullPath(args[2]); Data.NoLinks(previous);
    foreach(var name in new[] {"old.tgz","new.tgz"}.Concat(Directory.EnumerateFiles(previous,"native-*.tgz*").Where(p=>p.EndsWith(".tgz",StringComparison.Ordinal) || p.EndsWith(".tgz.partial",StringComparison.Ordinal)).Select(p=>Path.GetFileName(p))))
    {
        var source=Path.Combine(previous,name); Data.NoLinks(source);
        if(File.Exists(source)) File.Copy(source,Path.Combine(root,name),false);
    }
    var previousCache=Path.Combine(previous,"npm-cache","_cacache");
    Data.NoLinks(previousCache);
    if(Directory.Exists(previousCache))
        foreach(var source in Directory.EnumerateFiles(previousCache,"*",SearchOption.AllDirectories))
        {
            Data.NoLinks(source);
            var destination=Path.Combine(root,"npm-cache","_cacache",Path.GetRelativePath(previousCache,source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source,destination,false);
        }
}
var runner=new CommandRunner(); using var network=new OfficialNetwork();
var npm=WindowsDiscovery.FindNpm() ?? throw new IOException("Standard Node/npm required.");
var before=await new WindowsDiscovery(runner).DiscoverAsync(default); Data.Write(Path.Combine(root,"before.json"),before);
var prefix=Path.Combine(root,"隔离 npm prefix"); Directory.CreateDirectory(prefix);
var config=Path.Combine(root,"npm-config"); File.WriteAllText(config,""); File.WriteAllText(config+".global","");
var verifier=new PackageVerifier(network,(path,_)=>Task.Run(()=>WindowsTrust.Verify(path)));
var events=new List<object>(); var watch=Stopwatch.StartNew();
try
{
    var latest=await network.CheckAsync(InstallKind.Npm,default);
    const string oldVersion="0.161.0";
    if (!Data.Newer(latest.Version,oldVersion)) throw new InvalidOperationException("Test baseline is no longer older; choose a new published baseline deliberately.");
    var metadata=await network.TextAsync("https://registry.npmjs.org/@openai%2fcodex/"+oldVersion,default);
    using var document=JsonDocument.Parse(metadata); var dist=document.RootElement.GetProperty("dist");
    var old=new ReleaseOffer(InstallKind.Npm,oldVersion,dist.GetProperty("tarball").GetString()!,64*1024*1024,dist.GetProperty("integrity").GetString(),metadata);
    var oldFile=Path.Combine(root,"old.tgz"); await new PackageDownloader(network).DownloadAsync(old,oldFile,null,default); await verifier.VerifyAsync(oldFile,old,default);
    var placeholder=new Installation("seed",InstallKind.Npm,"Codex CLI","0.0.0",prefix,"@openai/codex","npm");
    await NativeDependency.CacheAsync(runner,npm,oldVersion,Path.Combine(root,"npm-cache"),config,root,default);
    var seed=await runner.RunAsync(WindowsPlatform.NpmCommand(npm,placeholder,old,Path.Combine(root,"npm-cache"),config,oldFile),TimeSpan.FromMinutes(12),default);
    if(seed.ExitCode!=0) throw new IOException("Baseline npm install failed: "+seed.Error);
    var target=WindowsDiscovery.FromNpm(prefix,true) ?? throw new IOException("Baseline was not discovered.");
    if(target.Version!=oldVersion) throw new IOException("Wrong baseline version.");
    async Task<string> VersionAsync()
    {
        var result=await runner.RunAsync(new(npm.Node,[Path.Combine(prefix,"node_modules","@openai","codex","bin","codex.js"),"--version"],Environment:new Dictionary<string,string>{{"CODEX_HOME",Path.Combine(root,"empty-codex-home")}}),TimeSpan.FromSeconds(30),default);
        if(result.ExitCode!=0) throw new IOException("CLI execution failed: "+result.Error); return result.Output.Trim();
    }
    var oldOutput=await VersionAsync(); if(!oldOutput.Contains(oldVersion,StringComparison.Ordinal)) throw new IOException("Old CLI runtime differs from manifest.");
    events.Add(new{Step="baseline-installed-and-executed",Version=oldVersion,Runtime=oldOutput}); Console.WriteLine("Baseline installed and executed: "+oldOutput);
    var state=new UpdateState(Path.Combine(root,"state"));
    var source=new WindowsDiscovery(runner,[prefix]); var platform=new WindowsPlatform(runner,state.Root);
    var service=new UpdateService(state,source,platform,verifier);
    var latestFile=Path.Combine(root,"new.tgz"); await new PackageDownloader(network).DownloadAsync(latest,latestFile,null,default);
    var prepared=await service.VerifyPreparedAsync(target,latest,latestFile,default);
    await service.ApplyAsync(prepared,default);
    var newOutput=await VersionAsync(); if(!newOutput.Contains(latest.Version,StringComparison.Ordinal)) throw new IOException("New CLI runtime differs from target.");
    if(state.Read()?.Phase!="Completed") throw new IOException("No completed journal.");
    events.Add(new{Step="public-service-upgrade-and-execution",Version=latest.Version,Runtime=newOutput,prepared.Sha256}); Console.WriteLine("Public service upgraded and executed: "+newOutput);
    if(args.Length>=2)
    {
        var msix=Path.GetFullPath(args[1]);
        var version=await verifier.VerifyAsync(msix,new(InstallKind.DesktopMsix,"0.0.0.0",OfficialNetwork.DesktopUrl,new FileInfo(msix).Length),default);
        events.Add(new{Step="real-msix-signature-and-blockmap",Version=version,Sha256=await PackageVerifier.HashAsync(msix,default)}); Console.WriteLine("Real signed MSIX verified: "+version);
    }
    var after=await new WindowsDiscovery(runner).DiscoverAsync(default); Data.Write(Path.Combine(root,"after.json"),after);
    if(JsonSerializer.Serialize(before.Installations,Data.Json)!=JsonSerializer.Serialize(after.Installations,Data.Json)) throw new IOException("Existing installation inventory changed.");
    Data.Write(Path.Combine(root,"report.json"),new{Passed=true,ElapsedSeconds=watch.Elapsed.TotalSeconds,ExistingInstallationsUnchanged=true,ReusedArtifacts=args.Length==3,Events=events,Scope="Real official npm installation/upgrade in a fresh prefix; native Windows MSIX verification if supplied. Completed package bytes and npm metadata cache may be reused and packages are reverified. No Windows app deployment performed."});
}
catch(Exception error)
{
    Data.Write(Path.Combine(root,"report.json"),new{Passed=false,ElapsedSeconds=watch.Elapsed.TotalSeconds,Events=events,Error=error.Message}); throw;
}
