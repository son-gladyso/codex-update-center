using System.IO;
using System.Text.Json;
using System.Windows;

namespace UpdateCenter.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            UpdateService.RequirePlatform();
            if (e.Args.Length > 0)
            {
                if (e.Args.Length == 2 && e.Args[0] == "--verify-signature") { WindowsTrust.Verify(e.Args[1]); Shutdown(0); return; }
                if (e.Args.Length == 2 && e.Args[0] == "--discover")
                { var result = await new WindowsDiscovery(new CommandRunner()).DiscoverAsync(CancellationToken.None); Data.Write(e.Args[1], result); Shutdown(0); return; }
                if (e.Args.Length == 3 && e.Args[0] == "--preview")
                { var preview = new MainWindow(e.Args[1], true); MainWindow = preview; preview.Show(); await preview.RenderPreviewAsync(e.Args[2]); preview.Close(); Shutdown(0); return; }
                throw new ArgumentException("Supported diagnostics: --discover <absolute-output.json>; --preview <isolated-data-root> <output.png>.");
            }
            MainWindow = new MainWindow(Data.DefaultRoot); MainWindow.Show();
        }
        catch (Exception error)
        {
            if (e.Args.Length == 0) MessageBox.Show(error.Message, "Codex Update Center", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    public static async Task VerifySignatureAsync(string path, CancellationToken ct)
    {
        var result = await new CommandRunner().RunAsync(new(Environment.ProcessPath!, ["--verify-signature", path]), TimeSpan.FromSeconds(120), ct);
        if (result.ExitCode != 0) throw new InvalidDataException("Windows 签名验证失败或超时。请检查系统时间、证书和网络。不会跳过验证。");
    }
}
