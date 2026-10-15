using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text;

namespace UpdateCenter;

public sealed record Volume(string Root, long Free, long Total, bool Internal);
public static class CacheStorage
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetVolumeNameForVolumeMountPoint(string path, StringBuilder name, int length);
    public static string VolumeIdentity(string path)
    {
        Data.NoLinks(path); var output = new StringBuilder(1024);
        if (!GetVolumeNameForVolumeMountPoint(Path.GetPathRoot(Path.GetFullPath(path))!, output, output.Capacity)) throw new IOException("无法识别缓存磁盘，暂停任务 / Cannot identify cache volume.");
        return output.ToString();
    }
    public static void RequireSpace(string path, long required)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
        if (!drive.IsReady) throw new IOException("缓存磁盘离线 / Cache volume offline.");
        _ = Choose([new Volume(drive.Name, drive.AvailableFreeSpace, drive.TotalSize, true)], Math.Max(1, required));
    }
    public const long GiB = 1024L * 1024 * 1024;
    public static long Reserve(Volume volume) => Math.Max(volume.Root.StartsWith("C:", StringComparison.OrdinalIgnoreCase) ? 20 * GiB : 10 * GiB, Math.Min(25 * GiB, volume.Total / 20));
    public static Volume Choose(IEnumerable<Volume> volumes, long required)
    {
        if (required <= 0) throw new ArgumentOutOfRangeException(nameof(required));
        return volumes.Where(v => v.Internal && v.Free >= required && v.Free - required >= Reserve(v))
            .OrderByDescending(v => v.Root.StartsWith("C:", StringComparison.OrdinalIgnoreCase)).ThenByDescending(v => v.Free)
            .FirstOrDefault() ?? throw new IOException("没有满足更新预算和安全余量的本地磁盘 / Insufficient free space.");
    }
    public static async Task<Volume[]> ObserveAsync(ICommandRunner runner, CancellationToken ct)
    {
        const string script = "$letters=@(Get-Partition | Where-Object DriveLetter | ForEach-Object { $p=$_; $d=$p | Get-Disk; if($d.BusType -in @('SATA','SAS','ATA','NVMe','SCM','RAID')) { [string]$p.DriveLetter } }); ConvertTo-Json -InputObject @($letters) -Compress";
        var result = await runner.RunAsync(CommandRunner.PowerShell(script), TimeSpan.FromSeconds(30), ct);
        var letters = result.ExitCode == 0 ? JsonSerializer.Deserialize<string[]>(result.Output, Data.Json) ?? [] : [];
        return DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed && d.DriveFormat == "NTFS")
            .Select(d => new Volume(d.Name, d.AvailableFreeSpace, d.TotalSize, letters.Contains(d.Name[..1], StringComparer.OrdinalIgnoreCase) || d.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase))).ToArray();
    }
    public static string Create(Volume volume, string stateRoot)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var basePath = Path.GetPathRoot(local)!.Equals(volume.Root, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(local, "CodexUpdateCenter", "Public", "cache")
            : Path.Combine(volume.Root, "CodexUpdateCenterCache", WindowsIdentity.GetCurrent().User!.Value);
        var directory = Path.Combine(basePath, Data.Id(stateRoot), Guid.NewGuid().ToString("N"));
        Data.NoLinks(directory); Directory.CreateDirectory(directory);
        var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
        Data.Write(Path.Combine(directory, "owner.json"), new { Application = "CodexUpdateCenter.Public", State = Data.Id(stateRoot) });
        return directory;
    }
}
