using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace AlienFXControlCenter.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, string Command = "")
{
    public bool Success => ExitCode == 0;
}

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string exePath, IEnumerable<string> arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        var commandLine = ToCommandLine(exePath, psi.ArgumentList);

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new ProcessResult(process.ExitCode, stdout, stderr, commandLine);
        }
        catch (Win32Exception ex)
        {
            return new ProcessResult(ex.NativeErrorCode, string.Empty,
                $"Failed to start \"{exePath}\": {ex.Message} (elevation required?)", commandLine);
        }
    }

    public static string ToCommandLine(string exePath, IEnumerable<string> arguments)
    {
        var sb = new StringBuilder(exePath);
        foreach (var argument in arguments)
            sb.Append(' ').Append(argument);
        return sb.ToString();
    }
}
