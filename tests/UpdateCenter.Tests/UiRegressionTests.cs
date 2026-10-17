using System.Reflection;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using UpdateCenter.App;
using Xunit;

namespace UpdateCenter.Tests;

public sealed class UiRegressionTests
{
    private static object? Invoke(MainWindow window, string name, params object?[] args) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);
    private static void Set(MainWindow window, string name, object? value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
    private static T Control<T>(MainWindow window, string name) => (T)window.FindName(name);
    private static Task Run(MainWindow window, Func<CancellationToken, Task> action) => (Task)Invoke(window, "RunAsync", action)!;

    // Each test owns a real STA dispatcher and an isolated data directory.
    private static Task OnWindow(Func<MainWindow, Fixture, Task> test, string cost = "Unrestricted", Func<HttpRequestMessage, HttpResponseMessage>? reply = null)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    using var fixture = new Fixture();
                    var window = new MainWindow(fixture.Root, true, () => cost, reply == null ? null : new OfficialNetwork(new Handler(reply)));
                    try { await test(window, fixture); }
                    finally { window.Close(); }
                    completion.SetResult();
                }
                catch (Exception error) { completion.SetException(error); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public Task PreparationFailureSurvivesReopenAndExplicitRetryQueriesFreshOffer()
    {
        var queries = 0;
        HttpResponseMessage Reply(HttpRequestMessage request)
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            queries++;
            return new(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(queries == 1 ? [] : new byte[] { 1, 2, 3 }) };
        }
        return OnWindow(async (window, fixture) =>
        {
            var target = new Installation("test", InstallKind.DesktopMsix, "Desktop", "1.0.0.0", fixture.Root, "test", "test");
            void Select(MainWindow value)
            {
                Control<ListBox>(value, "InstallList").ItemsSource = new[] { target };
                Control<ListBox>(value, "InstallList").SelectedIndex = 0;
            }
            Select(window);
            await Run(window, ct => (Task)Invoke(window, "PrepareAsync", ct)!);
            Assert.Equal("PreparationFailed", new UpdateState(fixture.Root).Read()!.Phase);
            Assert.Equal(1, queries);
            var oldPackage = fixture.File("old/package", [7, 8]);
            var prepPath = Path.Combine(fixture.Root, "preparation.json");
            Data.Write(prepPath, new Preparation(target, new(InstallKind.DesktopMsix, "9.0.0.0", OfficialNetwork.DesktopUrl, 3), oldPackage, "old-volume", DateTimeOffset.UtcNow));
            window.Close();
            var reopened = new MainWindow(fixture.Root, true, () => "Metered", new OfficialNetwork(new Handler(Reply)));
            try
            {
                Select(reopened);
                await Run(reopened, ct => (Task)Invoke(reopened, "ResumeAutomaticAsync", ct)!);
                Assert.Equal(1, queries);
                Assert.Equal("PreparationFailed", new UpdateState(fixture.Root).Read()!.Phase);
                await Run(reopened, ct => (Task)Invoke(reopened, "PrepareAsync", ct)!);
                Assert.Equal(2, queries);
                Assert.False(File.Exists(prepPath));
                Assert.Single(Directory.GetFiles(fixture.Root, "preparation.json.retained-*"));
                Assert.Equal(new byte[] { 7, 8 }, File.ReadAllBytes(oldPackage));
            }
            finally { reopened.Close(); }
        }, "Metered", Reply);
    }

    [Fact]
    public Task BusyOperationCannotHaveItsJournalOverwrittenByPreparationFailure() => OnWindow(async (window, fixture) =>
    {
        var target = new Installation("test", InstallKind.Npm, "CLI", "1.0.0", fixture.Root, "@openai/codex", "test");
        Control<ListBox>(window, "InstallList").ItemsSource = new[] { target };
        Control<ListBox>(window, "InstallList").SelectedIndex = 0;
        var state = new UpdateState(fixture.Root);
        state.Save("Installing", null, "Another operation owns this journal.");
        var original = File.ReadAllBytes(Path.Combine(fixture.Root, "job.json"));
        using var lease = state.Lock();
        await Run(window, ct => (Task)Invoke(window, "PrepareAsync", ct)!);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(fixture.Root, "job.json")));
        Assert.Contains("无法保存停止状态", Control<TextBlock>(window, "StatusText").Text);
    });

    [Fact]
    public Task PreferenceWriteFailureRestoresCheckboxWithoutCrashing() => OnWindow((window, fixture) =>
    {
        Directory.CreateDirectory(Path.Combine(fixture.Root, "preferences.json"));
        Control<CheckBox>(window, "AutoBox").IsChecked = false;
        Assert.True(Control<CheckBox>(window, "AutoBox").IsChecked);
        Assert.Contains("操作未完成", Control<TextBlock>(window, "StatusText").Text);
        return Task.CompletedTask;
    });

    [Fact]
    public Task JournalWriteFailureCannotPreventPause() => OnWindow(async (window, fixture) =>
    {
        Directory.CreateDirectory(Path.Combine(fixture.Root, "job.json"));
        CancellationToken observed = default;
        var running = Run(window, ct => { observed = ct; return Task.Delay(Timeout.Infinite, ct); });
        Invoke(window, "PauseClick", window, new System.Windows.RoutedEventArgs());
        await running;
        Assert.True(observed.IsCancellationRequested);
        Assert.Contains("暂停", Control<TextBlock>(window, "StatusText").Text);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TimeoutIsNotReportedAsUserPause(bool installing) => OnWindow(async (window, fixture) =>
    {
        var target = new Installation("test", InstallKind.Npm, "CLI", "1.0.0", fixture.Root, "@openai/codex", "test");
        Set(window, "prepared", new PreparedUpdate(target, new(InstallKind.Npm, "1.0.1", "https://registry.npmjs.org/test", 3), fixture.File("test.tgz"), "test", DateTimeOffset.UtcNow));
        await Run(window, _ => { Set(window, "installing", installing); return Task.FromException(new OperationCanceledException()); });
        var status = Control<TextBlock>(window, "StatusText").Text;
        Assert.Contains("超时", status);
        Assert.DoesNotContain("已暂停", status);
        if (installing)
        {
            Assert.Null(typeof(MainWindow).GetField("prepared", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));
            Assert.False(Control<Button>(window, "InstallButton").IsEnabled);
        }
    });

    [Theory]
    [InlineData("Metered")]
    [InlineData("Unknown")]
    public Task InstallationRechecksNetworkCostBeforeChangingState(string cost) => OnWindow(async (window, fixture) =>
    {
        var target = new Installation("test", InstallKind.Npm, "CLI", "1.0.0", fixture.Root, "@openai/codex", "test");
        var package = Path.Combine(fixture.Root, "missing-cache", "test.tgz");
        var candidate = new PreparedUpdate(target, new(InstallKind.Npm, "1.0.1", "https://registry.npmjs.org/test", 3), package, "test", DateTimeOffset.UtcNow);
        var state = new UpdateState(fixture.Root);
        state.Save("Prepared", candidate, "Verified before network changed.");
        var journal = File.ReadAllBytes(Path.Combine(fixture.Root, "job.json"));
        Control<ListBox>(window, "InstallList").ItemsSource = new[] { target };
        Control<ListBox>(window, "InstallList").SelectedIndex = 0;
        Set(window, "prepared", candidate);

        await Run(window, ct => (Task)Invoke(window, "InstallPreparedAsync", ct)!);

        Assert.Contains("网络计费状态", Control<TextBlock>(window, "StatusText").Text);
        Assert.DoesNotContain("安装失败", Control<TextBlock>(window, "StatusText").Text);
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(fixture.Root, "job.json")));
        Assert.Same(candidate, typeof(MainWindow).GetField("prepared", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));
        Assert.True(Control<Button>(window, "InstallButton").IsEnabled);
        Assert.False(Directory.Exists(Path.GetDirectoryName(package)));
    }, cost);

    [Theory]
    [InlineData("Metered")]
    [InlineData("Unknown")]
    public Task ExplicitNetworkPermissionAllowsGuardToProceed(string cost) => OnWindow(async (window, fixture) =>
    {
        Control<CheckBox>(window, "AllowMetered").IsChecked = true;
        await (Task)Invoke(window, "RequireDownloadPermissionAsync", CancellationToken.None)!;
        Assert.False(File.Exists(Path.Combine(fixture.Root, "job.json")));
    }, cost);

    [Theory]
    [InlineData("Metered")]
    [InlineData("Unknown")]
    public Task PreparedCandidateCannotBypassNetworkCostGuard(string cost) => OnWindow(async (window, fixture) =>
    {
        var target = new Installation("test", InstallKind.Npm, "CLI", "1.0.0", fixture.Root, "@openai/codex", "test");
        var package = Path.Combine(fixture.Root, "missing.tgz");
        var prepared = new PreparedUpdate(target, new(InstallKind.Npm, "1.0.1", "https://registry.npmjs.org/test", 3), package, "test", DateTimeOffset.UtcNow);
        new UpdateState(fixture.Root).Save("Prepared", prepared, "test");
        Control<ListBox>(window, "InstallList").ItemsSource = new[] { target };
        Control<ListBox>(window, "InstallList").SelectedIndex = 0;
        await Run(window, ct => (Task)Invoke(window, "PrepareAsync", ct)!);
        Assert.Contains("网络计费状态", Control<TextBlock>(window, "StatusText").Text);
        Assert.False(File.Exists(package));
        Assert.False(Control<Button>(window, "InstallButton").IsEnabled);
    }, cost);
}
