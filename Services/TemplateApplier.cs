using System.Globalization;
using AlienFXControlCenter.Models;

namespace AlienFXControlCenter.Services;

/// <summary>
/// Result of applying a <see cref="LightingTemplate"/>. Carries structured data
/// only; callers are responsible for turning it into localized UI text.
/// </summary>
public sealed class TemplateApplyOutcome
{
    public bool FxMissing { get; set; }

    public bool FanMissing { get; set; }

    public int LightCount { get; set; }

    public int FanCount { get; set; }

    public string Commands { get; set; } = string.Empty;
}

/// <summary>
/// Applies a template's lighting and fan settings. Shared by the Templates page
/// and the key-binding dispatcher so both use identical logic.
/// </summary>
public static class TemplateApplier
{
    public static async Task<TemplateApplyOutcome> ApplyAsync(LightingTemplate template, CancellationToken ct = default)
    {
        var outcome = new TemplateApplyOutcome();
        var commands = new List<string>();

        if (template.HasLights)
        {
            var client = AppServices.Fx;
            if (client is null)
            {
                outcome.FxMissing = true;
                return outcome;
            }

            outcome.LightCount = await ApplyLightsAsync(client, template, commands, ct);
        }

        if (template.HasFans)
        {
            var client = AppServices.Fan;
            if (client is null)
            {
                outcome.FanMissing = true;
                return outcome;
            }

            outcome.FanCount = await ApplyFansAsync(client, template, commands, ct);
        }

        outcome.Commands = string.Join(Environment.NewLine, commands.Where(c => !string.IsNullOrEmpty(c)));
        return outcome;
    }

    /// <summary>Builds a localized status line describing an apply outcome.</summary>
    public static string FormatStatus(LightingTemplate template, TemplateApplyOutcome outcome)
    {
        if (outcome.FxMissing)
            return Localizer.Format("Status_ToolsMissing", "alienfx-cli");
        if (outcome.FanMissing)
            return Localizer.Format("Status_ToolsMissing", "alienfan-cli");

        var applied = new List<string>();
        if (template.HasLights)
            applied.Add(Localizer.Format("Templates_SummaryLights", outcome.LightCount));
        if (template.HasFans)
            applied.Add(Localizer.Format("Templates_SummaryFans", outcome.FanCount));

        return applied.Count == 0
            ? Localizer.Get("Templates_NothingToApply")
            : Localizer.Format("Templates_Applied", template.Name, string.Join(" · ", applied));
    }

    private static async Task<int> ApplyLightsAsync(
        AlienFxClient client,
        LightingTemplate template,
        List<string> commands,
        CancellationToken ct)
    {
        if (TryParseHex(template.WholeSystemColor, out var r, out var g, out var b))
        {
            var result = await client.SetAllAsync(r, g, b, ct);
            commands.Add(result.Command);
        }

        var devices = await client.GetStatusAsync(ct);
        var byKey = new Dictionary<(int, int), int>();
        foreach (var device in devices)
            byKey[(device.VendorId, device.ProductId)] = device.Id;

        var targets = new List<(int Device, int Light, byte R, byte G, byte B)>();
        foreach (var light in template.Lights)
        {
            if (byKey.TryGetValue((light.VendorId, light.ProductId), out var deviceId))
                targets.Add((deviceId, light.LightId, light.R, light.G, light.B));
        }

        if (targets.Count > 0)
        {
            var result = await client.SetLightColorsAsync(targets, ct);
            commands.Add(result.Command);
        }

        return targets.Count + (template.WholeSystemColor is null ? 0 : 1);
    }

    private static async Task<int> ApplyFansAsync(
        AlienFanClient client,
        LightingTemplate template,
        List<string> commands,
        CancellationToken ct)
    {
        var snapshot = await client.GetSnapshotAsync(ct);
        if (snapshot.Fans.Count == 0)
            return 0;

        var percents = new int[snapshot.Fans.Count];
        for (var i = 0; i < snapshot.Fans.Count; i++)
        {
            var fan = snapshot.Fans[i];
            var preset = template.Fans.FirstOrDefault(f => f.FanId == fan.Id);
            percents[i] = preset?.Percent ?? fan.Percent;
        }

        var result = await client.SetFansAsync(percents, ct);
        commands.Add(result.Command);
        return snapshot.Fans.Count;
    }

    private static bool TryParseHex(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var value = hex.TrimStart('#');
        if (value.Length != 6)
            return false;

        return byte.TryParse(value.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            && byte.TryParse(value.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            && byte.TryParse(value.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
    }
}
