using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace UpdateCenter.App;

public sealed record Preferences(bool Automatic = true, string[]? ExtraPrefixes = null);
public sealed record Preparation(Installation Target, ReleaseOffer Offer, string Package, string VolumeId, DateTimeOffset At = default);
public partial class MainWindow : Window
{
    private readonly UpdateState state;
    private readonly CommandRunner runner = new();
    private readonly OfficialNetwork network;
    private readonly bool preview;
    private readonly FileStream windowLease;
    private readonly Func<string> readNetworkCost;
    private Preferences preferences;
    private CancellationTokenSource? operation;
    private Task? active;
    private PreparedUpdate? prepared;
    private bool installing, closing, initialized;
    private Installation? Selected => InstallList.SelectedItem as Installation;
    private WindowsDiscovery Discovery => new(runner, preferences.ExtraPrefixes);
    private UpdateService Service => new(state, Discovery, new WindowsPlatform(runner, state.Root), new PackageVerifier(network, App.VerifySignatureAsync));
    public MainWindow(string root, bool preview = false, Func<string>? readNetworkCost = null, OfficialNetwork? network = null)
    {
        state = new(root); this.preview = preview;
        this.readNetworkCost = readNetworkCost ?? NetworkCost.Read;
        this.network = network ?? new OfficialNetwork();
        windowLease = new FileStream(Path.Combine(state.Root, "window.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        preferences = Data.Read<Preferences>(Path.Combine(root, "preferences.json")) ?? new();
        InitializeComponent(); AutoBox.IsChecked = preferences.Automatic; initialized = true;
        VersionText.Text = "COMMUNITY EDITION  ·  Windows x64  ·  " + Data.ProductVersion;
        Loaded += async (_, _) => { if (!preview) await RunAsync(InitializeAsync); };
        Closing += OnClosing; Closed += (_, _) => { this.network.Dispose(); windowLease.Dispose(); };
    }
    private async Task InitializeAsync(CancellationToken ct)
    {
        await ScanAsync(ct);
        await ResumeAutomaticAsync(ct);
    }
    private async Task ResumeAutomaticAsync(CancellationToken ct)
    {
        var journal = state.Read();
        if (journal != null)
        {
            StatusText.Text = journal.Message;
            if (journal.Phase is "Installing" or "NeedsReview") { WarningText.Text += "\n上次安装结果尚未确认，请先重新扫描。程序不会自动继续安装。"; return; }
            if (journal.Phase == "Paused") { StatusText.Text = "上次已暂停；点击“检查并准备更新”继续。"; return; }
            if (journal.Phase == "PreparationFailed") { StatusText.Text = journal.Message; return; }
        }
        if (preferences.Automatic && Selected?.Supported == true) await PrepareAsync(ct);
    }
    private async Task ScanAsync(CancellationToken ct)
    {
        var id = Selected?.Id; StatusText.Text = "正在识别当前用户的 Codex 安装…";
        var result = await Discovery.DiscoverAsync(ct);
        InstallList.ItemsSource = result.Installations;
        InstallList.SelectedItem = result.Installations.FirstOrDefault(i => i.Id == id) ?? (result.Installations.Length == 1 ? result.Installations[0] : null);
        CountText.Text = $"检测到 {result.Installations.Length} 个安装";
        StatusText.Text = result.Installations.Length == 0 ? "未找到支持的安装。可打开官方安装说明，或添加 npm 全局安装目录。" : Selected == null ? "请选择要更新的安装。" : "安装识别完成。";
        WarningText.Text = string.Join("\n", result.Warnings);
        var cost = await Task.Run(readNetworkCost, ct); NetworkText.Text = "网络费用状态：" + (cost == "Unrestricted" ? "不限流量" : cost == "Metered" ? "计费网络" : "未知（自动下载暂停）");
        RefreshButtons();
    }
    private async Task PrepareAsync(CancellationToken ct)
    {
        try { await PrepareCoreAsync(ct); }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            prepared = null;
            // Persist the stop even for a transport failure: reopening must not
            // silently retry a failed candidate. Explicit preparation starts fresh.
            var message = "准备失败，已停止自动重试。点击“检查并准备更新”重新查询；旧文件保留。\n" + error.Message;
            try { using var lease = state.Lock(); state.Save("PreparationFailed", null, message); }
            catch (Exception saveError) { throw new IOException(message + "\n无法保存停止状态：" + saveError.Message, error); }
            throw new IOException(message, error);
        }
    }
    private async Task PrepareCoreAsync(CancellationToken ct)
    {
        var target = Selected ?? throw new InvalidOperationException("请先选择安装。");
        if (!target.Supported) throw new InvalidOperationException(target.Detail);
        using var lease = state.Lock(); prepared = null;
        var oldJournal = state.Read();
        if (oldJournal?.Phase == "PreparationFailed") RetainPreparation();
        var resume = Data.Read<Preparation>(Path.Combine(state.Root, "preparation.json"));
        if (resume?.Target != target || resume.At == default || DateTimeOffset.UtcNow - resume.At >= TimeSpan.FromHours(6)) resume = null;
        var already = oldJournal?.Prepared;
        if (already?.Target == target && oldJournal!.Phase is "Prepared" or "Completed" && DateTimeOffset.UtcNow - already.At < TimeSpan.FromHours(6))
        {
            StatusText.Text = "重新验证已准备的候选…";
            await RequireDownloadPermissionAsync(ct);
            await PrepareNativeAsync(already.Offer, already.Package, ct);
            prepared = await Service.VerifyPreparedAsync(target, already.Offer, already.Package, ct); ShowPrepared(); return;
        }
        StatusText.Text = "正在查询官方更新…";
        var offer = resume?.Offer ?? await network.CheckAsync(target.Kind, ct);
        if (target.Kind == InstallKind.Npm && !Data.Newer(offer.Version, target.Version)) { StatusText.Text = $"当前 CLI {target.Version} 已是相同或更新版本。"; state.Save("UpToDate", null, StatusText.Text); return; }
        if (!AllowMetered.IsChecked.GetValueOrDefault() && await Task.Run(readNetworkCost, ct) != "Unrestricted")
        { StatusText.Text = "已查询更新。网络计费状态未知或受限，暂停大包下载；可勾选仅本次允许后继续。"; return; }
        string package;
        if (resume != null)
        {
            package = resume.Package;
            if (CacheStorage.VolumeIdentity(package) != resume.VolumeId) throw new IOException("原缓存磁盘不在线或盘符已指向另一磁盘。请接回原磁盘，或保留旧文件后重新准备。");
            var savedBytes = File.Exists(package + ".partial") ? new FileInfo(package + ".partial").Length : 0;
            CacheStorage.RequireSpace(package, Math.Max(CacheStorage.GiB, offer.Size - savedBytes + CacheStorage.GiB));
        }
        else
        {
            // npm also downloads its native optional dependency during installation.
            var required = target.Kind == InstallKind.Npm ? 4 * CacheStorage.GiB : checked(offer.Size * 3 + CacheStorage.GiB);
            var volume = CacheStorage.Choose(await CacheStorage.ObserveAsync(runner, ct), required);
            var directory = CacheStorage.Create(volume, state.Root); package = Path.Combine(directory, target.Kind == InstallKind.Npm ? "codex.tgz" : "ChatGPT.msix");
            Data.Write(Path.Combine(state.Root, "preparation.json"), new Preparation(target, offer, package, CacheStorage.VolumeIdentity(package), DateTimeOffset.UtcNow));
        }
        DetailText.Text = $"所选安装：{target.Location}\n下载位置：{package}\n安装过程会再次检查版本、包签名和运行状态。";
        state.Save("Downloading", null, "正在下载；关闭窗口后保留断点，下次打开可继续。");
        await new PackageDownloader(network).DownloadAsync(offer, package, new Progress<string>(text => StatusText.Text = text), ct);
        StatusText.Text = "下载完成，正在验证签名和包内容…";
        await PrepareNativeAsync(offer, package, ct);
        prepared = await Service.VerifyPreparedAsync(target, offer, package, ct); ShowPrepared();
    }
    private void RetainPreparation()
    {
        var path = Path.Combine(state.Root, "preparation.json");
        if (File.Exists(path)) { Data.NoLinks(path); File.Move(path, path + ".retained-" + Guid.NewGuid().ToString("N")); }
    }
    private async Task PrepareNativeAsync(ReleaseOffer offer, string package, CancellationToken ct)
    {
        if (offer.Kind != InstallKind.Npm) return;
        await RequireDownloadPermissionAsync(ct);
        StatusText.Text = "正在分段准备并验签 Windows 原生组件，可能需要几分钟；可暂停后续传…";
        await new WindowsPlatform(runner, state.Root).PrepareNpmAsync(offer, package, ct, new Progress<string>(text => StatusText.Text = text));
    }
    private async Task RequireDownloadPermissionAsync(CancellationToken ct)
    {
        if (!AllowMetered.IsChecked.GetValueOrDefault() && await Task.Run(readNetworkCost, ct) != "Unrestricted")
            throw new IOException("网络计费状态未知或受限，已暂停下载；可勾选仅本次允许后继续。");
    }
    private void ShowPrepared()
    {
        if (prepared == null) return;
        StatusText.Text = Data.Newer(prepared.Offer.Version, prepared.Target.Version) ? $"候选 {prepared.Offer.Version} 已通过验证，等待确认安装。" : $"当前 {prepared.Target.Version} 已是相同或更新版本，不进行降级。";
        DetailText.Text = $"当前版本：{prepared.Target.Version}\n候选版本：{prepared.Offer.Version}\n所选目录：{prepared.Target.Location}\n安装包：{prepared.Package}\nSHA-256：{prepared.Sha256}";
    }
    private async Task RunAsync(Func<CancellationToken,Task> action)
    {
        if (operation != null) return;
        operation = new(); Activity.IsIndeterminate = true; RefreshButtons();
        try { active = action(operation.Token); await active; }
        catch (OperationCanceledException)
        {
            if (installing) { prepared = null; StatusText.Text = "安装超时或中断，结果尚未确认。请重新扫描并准备更新。"; }
            else StatusText.Text = operation.IsCancellationRequested ? "操作已暂停，已有下载断点保留。" : "操作超时，请重试；已有下载断点保留。";
        }
        catch (Exception error)
        {
            if (installing) prepared = null;
            StatusText.Text = installing ? "安装失败，结果尚未确认。请重新扫描并准备更新。\n" + error.Message : error.Message;
        }
        finally { active = null; operation.Dispose(); operation = null; installing = false; Activity.IsIndeterminate = false; RefreshButtons(); }
    }
    private void RefreshButtons()
    {
        var idle = operation == null;
        ScanButton.IsEnabled = AddButton.IsEnabled = InstallList.IsEnabled = idle;
        PrepareButton.IsEnabled = idle && Selected?.Supported == true;
        InstallButton.IsEnabled = idle && prepared?.Target.Id == Selected?.Id && prepared != null && Data.Newer(prepared.Offer.Version, prepared.Target.Version);
        PauseButton.IsEnabled = !idle && !installing;
        RestartButton.IsEnabled = idle && Selected?.Supported == true;
    }
    private async void ScanClick(object sender, RoutedEventArgs e) => await RunAsync(ScanAsync);
    private async void PrepareClick(object sender, RoutedEventArgs e) => await RunAsync(PrepareAsync);
    private async void InstallClick(object sender, RoutedEventArgs e)
    {
        if (prepared == null || MessageBox.Show(this, $"将 {prepared.Target.Name} 从 {prepared.Target.Version} 更新到 {prepared.Offer.Version}。\n\n目录：{prepared.Target.Location}\n\n请先保存工作并退出此安装的 Codex。安装时不能暂停；失败后需要重新扫描确认状态。\n\n继续？", "确认所选更新", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        await RunAsync(InstallPreparedAsync);
    }
    private async Task InstallPreparedAsync(CancellationToken ct)
    {
        var candidate = prepared ?? throw new InvalidOperationException("请先准备并验证更新。");
        // npm may refill its cache during installation. Recheck after confirmation,
        // before entering the non-pausable mutation phase, even for a prepared offer.
        if (candidate.Target.Kind == InstallKind.Npm) await RequireDownloadPermissionAsync(ct);
        ct.ThrowIfCancellationRequested();
        installing = true;
        RefreshButtons();
        StatusText.Text = "正在安装所选更新…";
        await Service.ApplyAsync(candidate, ct);
        prepared = null;
        await ScanAsync(ct);
        StatusText.Text = "更新完成，已重新检测并确认安装版本。";
    }
    private void PauseClick(object sender, RoutedEventArgs e)
    {
        if (installing) return;
        operation?.Cancel();
        TryUiAction(() => state.Save("Paused", prepared, "用户已暂停；点击准备更新继续。"));
    }
    private async void RestartClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(async ct =>
        {
            using (state.Lock())
            {
                RetainPreparation();
                prepared = null; state.Save("Idle", null, "保留旧文件，重新查询官方更新。");
            }
            await PrepareAsync(ct);
        });
    }
    private async void AddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择包含 node_modules\\@openai\\codex 的 npm 全局目录" };
        if (dialog.ShowDialog(this) != true) return;
        await RunAsync(async ct =>
        {
            var updated = preferences with { ExtraPrefixes = (preferences.ExtraPrefixes ?? []).Append(dialog.FolderName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
            SavePreferences(updated); preferences = updated;
            await ScanAsync(ct);
        });
    }
    private void AutoChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        var updated = preferences with { Automatic = AutoBox.IsChecked == true };
        if (TryUiAction(() => SavePreferences(updated))) preferences = updated;
        else { initialized = false; AutoBox.IsChecked = preferences.Automatic; initialized = true; }
    }
    private void SavePreferences(Preferences updated) => Data.Write(Path.Combine(state.Root, "preferences.json"), updated);
    private bool TryUiAction(Action action)
    {
        try { action(); return true; }
        catch (Exception error) { StatusText.Text = "操作未完成：" + error.Message; return false; }
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!initialized) return; if (prepared?.Target.Id != Selected?.Id) prepared = null; DetailText.Text = Selected == null ? "" : Selected.Location + "\n" + Selected.Detail; RefreshButtons(); }
    private void FolderClick(object sender, RoutedEventArgs e) => TryUiAction(() => Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "\"" + state.Root + "\"" }));
    private void HelpClick(object sender, RoutedEventArgs e) => TryUiAction(() => Process.Start(new ProcessStartInfo("https://learn.chatgpt.com/docs/windows/windows-app") { UseShellExecute = true }));
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing || operation == null) return;
        e.Cancel = true;
        if (installing) { StatusText.Text = "安装正在进行，请等待结果后关闭。"; return; }
        closing = true; operation.Cancel(); var pending = active;
        try { if (pending != null) await pending; } catch (Exception) { }
        await Dispatcher.InvokeAsync(Close, DispatcherPriority.Background);
    }
    public async Task RenderPreviewAsync(string output)
    {
        if (!preview) throw new InvalidOperationException();
        InstallList.ItemsSource = new[] { new Installation("sample", InstallKind.DesktopMsix, "Codex / ChatGPT 桌面版", "26.1002.7124.0", @"C:\Program Files\WindowsApps\OpenAI.Codex_…", "sample", "Windows 签名安装包 · 当前用户") }; InstallList.SelectedIndex = 0;
        CountText.Text = "检测到 1 个安装"; StatusText.Text = "候选已通过验证，等待确认安装。"; NetworkText.Text = "网络费用状态：不限流量";
        DetailText.Text = "当前版本：26.1002.7124.0\n候选版本：26.1007.9446.0\n下载位置：C 盘 · 当前用户的独立缓存\n\n签名和内容校验已完成。确认后只更新所选安装。";
        WarningText.Text = "界面预览使用合成数据，不代表真实安装验收结果。";
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); Data.NoLinks(output); Directory.CreateDirectory(Path.GetDirectoryName(output)!); using var stream = File.Create(output); encoder.Save(stream);
    }
}
