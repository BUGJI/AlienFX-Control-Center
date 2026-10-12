using System.Globalization;
using System.Text.RegularExpressions;
using AlienFXControlCenter.Models;

namespace AlienFXControlCenter.Services;

public sealed partial class AlienFxClient(string exePath)
{
    private const int MaxCommandsPerInvocation = 60;

    public string ExePath { get; } = exePath;

    public async Task<List<LightDevice>> GetStatusAsync(CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(ExePath, new[] { "status" }, ct).ConfigureAwait(false);
        return ParseStatus(result.StandardOutput);
    }

    public Task<ProcessResult> SetAllAsync(byte r, byte g, byte b, CancellationToken ct = default)
        => ProcessRunner.RunAsync(ExePath, new[] { $"setall={r},{g},{b}" }, ct);

    public async Task<ProcessResult> SetLightsAsync(IEnumerable<(int Device, int Light)> targets, byte r, byte g, byte b, CancellationToken ct = default)
    {
        var commands = targets
            .Select(t => $"setone={t.Device},{t.Light},{r},{g},{b}")
            .ToList();

        if (commands.Count == 0)
            return new ProcessResult(0, string.Empty, string.Empty);

        ProcessResult last = new(0, string.Empty, string.Empty);
        for (var offset = 0; offset < commands.Count; offset += MaxCommandsPerInvocation)
        {
            var chunk = commands.GetRange(offset, Math.Min(MaxCommandsPerInvocation, commands.Count - offset));
            last = await ProcessRunner.RunAsync(ExePath, chunk, ct).ConfigureAwait(false);
        }

        return last;
    }

    public Task<ProcessResult> ProbeAsync(CancellationToken ct = default)
        => ProcessRunner.RunAsync(ExePath, new[] { "probe" }, ct);

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(ExePath, Array.Empty<string>(), ct).ConfigureAwait(false);
            var match = VersionRegex().Match($"{result.StandardOutput}\n{result.StandardError}");
            return match.Success ? match.Value : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static List<LightDevice> ParseStatus(string output)
    {
        var devices = new List<LightDevice>();
        LightDevice? current = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            var deviceMatch = DeviceRegex().Match(line);
            if (deviceMatch.Success)
            {
                current = new LightDevice
                {
                    Id = int.Parse(deviceMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    Name = deviceMatch.Groups[2].Value.Trim(),
                    VendorId = int.Parse(deviceMatch.Groups[3].Value, CultureInfo.InvariantCulture),
                    ProductId = int.Parse(deviceMatch.Groups[4].Value, CultureInfo.InvariantCulture),
                    ApiVersion = int.Parse(deviceMatch.Groups[5].Value, CultureInfo.InvariantCulture),
                    LightCount = int.Parse(deviceMatch.Groups[6].Value, CultureInfo.InvariantCulture),
                };
                devices.Add(current);
                continue;
            }

            var lightMatch = LightRegex().Match(line);
            if (lightMatch.Success && current is not null)
            {
                current.Lights.Add(new Light
                {
                    Id = int.Parse(lightMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    Name = lightMatch.Groups[2].Value.Trim(),
                });
            }
        }

        return devices;
    }

    [GeneratedRegex(@"^Device #(\d+) - (.+?), VID#(\d+), PID#(\d+), APIv(\d+), (\d+) lights")]
    private static partial Regex DeviceRegex();

    [GeneratedRegex(@"^\s*Light ID#(\d+) - (.+)$")]
    private static partial Regex LightRegex();

    [GeneratedRegex(@"alienfx-cli\s+v?[\d.]+", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();
}
