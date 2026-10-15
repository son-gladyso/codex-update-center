using System.Diagnostics;
using System.Text;

namespace UpdateCenter;

public sealed class CommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(Command command, TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(timeout);
        var info = new ProcessStartInfo(command.Executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, StandardInputEncoding = new UTF8Encoding(false), WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System) };
        foreach (var argument in command.Arguments) info.ArgumentList.Add(argument);
        if (command.Environment != null) foreach (var pair in command.Environment) info.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(info) ?? throw new IOException("无法启动更新组件 / Cannot start update helper.");
        async Task<string> ReadAsync(StreamReader reader)
        {
            var buffer = new char[4096]; var result = new StringBuilder(); int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), deadline.Token)) > 0)
            {
                if (result.Length + read > 2 * 1024 * 1024) throw new IOException("组件输出超过限制 / Helper output limit exceeded.");
                result.Append(buffer, 0, read);
            }
            return result.ToString();
        }
        var stdout = ReadAsync(process.StandardOutput); var stderr = ReadAsync(process.StandardError);
        _ = stdout.ContinueWith(_ => deadline.Cancel(), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        _ = stderr.ContinueWith(_ => deadline.Cancel(), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        try
        {
            if (command.Input != null) await process.StandardInput.WriteAsync(command.Input.AsMemory(), deadline.Token);
            process.StandardInput.Close();
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(deadline.Token));
            return new(process.ExitCode, await stdout, await stderr);
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
    public static Command PowerShell(string script, object? input = null) => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
        ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); [Console]::InputEncoding=[Text.UTF8Encoding]::new($false); " + script))],
        input == null ? null : System.Text.Json.JsonSerializer.Serialize(input, Data.Json));
}
