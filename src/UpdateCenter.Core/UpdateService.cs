using System.Runtime.InteropServices;

namespace UpdateCenter;

public sealed class UpdateService(UpdateState state, IInstallationSource discovery, IUpdatePlatform platform, IPackageVerifier verifier)
{
    public static void RequirePlatform()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) || RuntimeInformation.OSArchitecture != Architecture.X64) throw new PlatformNotSupportedException("此发行包支持 Windows 10 2004+ / Windows 11 x64。ARM64、WSL 和其他系统请使用官方更新工具。");
    }
    public async Task ApplyAsync(PreparedUpdate prepared, CancellationToken ct)
    {
        RequirePlatform(); using var operation = state.Lock();
        if (!prepared.Target.Supported || prepared.Target.Kind != prepared.Offer.Kind || !Data.Newer(prepared.Offer.Version, prepared.Target.Version)) throw new InvalidOperationException("所选候选不是更高版本 / Candidate is not an upgrade.");
        var found = await discovery.DiscoverAsync(ct);
        var current = found.Installations.SingleOrDefault(i => i.Id == prepared.Target.Id);
        if (current != prepared.Target) throw new InvalidOperationException("安装信息已变化，请重新扫描并准备 / Installation changed.");
        Data.NoLinks(prepared.Package);
        // Keep the verified bytes immutable through deployment. npm/Windows may read, never rewrite this handle.
        await using var packageLock = new FileStream(prepared.Package, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (await PackageVerifier.HashAsync(prepared.Package, ct) != prepared.Sha256 || await verifier.VerifyAsync(prepared.Package, prepared.Offer, ct) != prepared.Offer.Version)
            throw new InvalidDataException("候选包已变化 / Prepared package changed.");
        await platform.AssertIdleAsync(current, ct);
        state.Save("Installing", prepared, "已开始安装；若程序中断，下次必须重新扫描确认结果。");
        try
        {
            await platform.InstallAsync(prepared, ct);
            var after = (await discovery.DiscoverAsync(ct)).Installations.SingleOrDefault(i => i.Id == current.Id);
            if (after == null || after.Kind != current.Kind || after.Identity != current.Identity || after.Version != prepared.Offer.Version)
                throw new IOException("安装器返回后未检测到目标版本，请重新扫描。不会报告成功。");
            state.Save("Completed", prepared, "安装版本已重新检测并确认 / Installed version verified.");
        }
        catch
        {
            state.Save("NeedsReview", prepared, "安装未确认成功。请重新扫描当前版本；不会自动重试安装或降级。Windows/npm 日志可用于排查。"); throw;
        }
    }
    public async Task<PreparedUpdate> VerifyPreparedAsync(Installation target, ReleaseOffer offer, string path, CancellationToken ct)
    {
        var version = await verifier.VerifyAsync(path, offer, ct);
        var prepared = new PreparedUpdate(target, offer with { Version = version }, path, await PackageVerifier.HashAsync(path, ct), DateTimeOffset.UtcNow);
        state.Save("Prepared", prepared, Data.Newer(version, target.Version) ? "候选已验签，等待确认安装。" : "当前安装已是相同或更新版本，不进行降级。");
        return prepared;
    }
}
