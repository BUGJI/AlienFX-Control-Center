using System.Globalization;
using System.Text.RegularExpressions;
using AlienFXControlCenter.Models;

namespace AlienFXControlCenter.Services;

public sealed partial class AlienFanClient(string exePath)
{
    public string ExePath { get; } = exePath;

    public async Task<FanSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var result = await ProcessRunner
            .RunAsync(ExePath, new[] { "rpm", "maxrpm", "percent", "getfans", "temp" }, ct)
            .ConfigureAwait(false);

        var snapshot = Parse(result.StandardOutput);

        if (!string.IsNullOrWhiteSpace(result.StandardError))
            snapshot.Raw = $"{result.StandardOutput}{Environment.NewLine}[stderr]{Environment.NewLine}{result.StandardError}";

        return snapshot;
    }

    public Task<ProcessResult> UnlockAsync(CancellationToken ct = default)
        => ProcessRunner.RunAsync(ExePath, new[] { "unlock" }, ct);

    public Task<ProcessResult> SetFansAsync(IReadOnlyList<int> percents, CancellationToken ct = default)
        => ProcessRunner.RunAsync(ExePath, new[] { $"setfans={string.Join(',', percents)}" }, ct);

    public static FanSnapshot Parse(string output)
    {
        var snapshot = new FanSnapshot { Raw = output };
        var fans = new Dictionary<int, FanReading>();
        var sensorId = 0;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var hardware = HardwareRegex().Match(line);
            if (hardware.Success)
            {
                snapshot.SystemId = int.Parse(hardware.Groups[1].Value, CultureInfo.InvariantCulture);
                snapshot.FanCount = int.Parse(hardware.Groups[2].Value, CultureInfo.InvariantCulture);
                snapshot.SensorCount = int.Parse(hardware.Groups[3].Value, CultureInfo.InvariantCulture);
                snapshot.PowerCount = int.Parse(hardware.Groups[4].Value, CultureInfo.InvariantCulture);
                snapshot.IsGmode = line.Contains("G-Mode");
                snapshot.IsTcc = line.Contains(", TCC");
                snapshot.IsXmp = line.Contains(", XMP");
                snapshot.HasLights = line.Contains(", Lights");
                continue;
            }

            var fan = FanRegex().Match(line);
            if (fan.Success)
            {
                var id = int.Parse(fan.Groups[2].Value, CultureInfo.InvariantCulture);
                if (!fans.TryGetValue(id, out var reading))
                {
                    reading = new FanReading { Id = id, Type = fan.Groups[1].Value };
                    fans[id] = reading;
                }

                ApplyFanValue(reading, fan.Groups[3].Value, int.Parse(fan.Groups[4].Value, CultureInfo.InvariantCulture));
                continue;
            }

            var sensor = SensorRegex().Match(line);
            if (sensor.Success)
            {
                snapshot.Sensors.Add(new SensorReading
                {
                    Id = sensorId++,
                    Name = sensor.Groups[1].Value,
                    Temperature = int.Parse(sensor.Groups[2].Value, CultureInfo.InvariantCulture),
                });
            }
        }

        snapshot.Fans.AddRange(fans.Values.OrderBy(f => f.Id));
        return snapshot;
    }

    private static void ApplyFanValue(FanReading reading, string label, int value)
    {
        switch (label)
        {
            case "RPM":
                reading.Rpm = value;
                break;
            case "Max. RPM":
                reading.MaxRpm = value;
                break;
            case "Percent":
                reading.Percent = value;
                break;
            case "boost":
                reading.Boost = value;
                break;
        }
    }

    [GeneratedRegex(@"^Fan\s+(CPU|GPU)?\s*(\d+)\s+(.+?):\s*(-?\d+)\s*$")]
    private static partial Regex FanRegex();

    [GeneratedRegex(@"^Supported hardware \((\d+)\) detected, (\d+) fans, (\d+) sensors, (\d+) power states")]
    private static partial Regex HardwareRegex();

    [GeneratedRegex(@"^(.+?):\s*(-?\d+)$")]
    private static partial Regex SensorRegex();
}
