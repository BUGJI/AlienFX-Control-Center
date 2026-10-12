using System.Collections.ObjectModel;
using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI;

namespace AlienFXControlCenter.ViewModels;

public sealed partial class LightsViewModel : ObservableObject
{
    private static readonly HashSet<string> WasdKeys = new(StringComparer.OrdinalIgnoreCase) { "W", "A", "S", "D" };
    private static readonly HashSet<string> ArrowKeys = new(StringComparer.OrdinalIgnoreCase) { "Up", "Down", "Left", "Right" };

    public ObservableCollection<DeviceOption> Targets { get; } = new();

    public bool ShowDebug => SettingsService.Current.Value.ShowDebugPanel;

    [ObservableProperty]
    private DeviceOption? selectedTarget;

    [ObservableProperty]
    private string status = Localizer.Get("Status_Ready");

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasDevices;

    [ObservableProperty]
    private string lastCommand = string.Empty;

    public async Task LoadAsync()
    {
        Targets.Clear();
        LastCommand = string.Empty;

        var client = AppServices.Fx;
        if (client is null)
        {
            Status = Localizer.Format("Status_ToolsMissing", "alienfx-cli");
            HasDevices = false;
            IsBusy = false;
            return;
        }

        IsBusy = true;
        try
        {
            Targets.Add(new DeviceOption { Label = Localizer.Get("Lights_WholeSystem") });

            var devices = await client.GetStatusAsync();
            foreach (var device in devices)
                Targets.Add(new DeviceOption { Label = Localizer.Format("Lights_DeviceLabel", device.DisplayName, device.LightCount), Device = device });

            SelectedTarget = Targets
                .Where(t => t.Device is not null)
                .OrderByDescending(t => t.Device!.LightCount)
                .FirstOrDefault() ?? Targets[0];
            HasDevices = devices.Count > 0;
            Status = devices.Count == 0
                ? Localizer.Get("Status_NoDevices")
                : Localizer.Format("Status_Detected", devices.Count, devices.Sum(d => d.LightCount));

            ProfileStore.Current = ProfileStore.Match(
                devices.Select(d => new DeviceIdentity(d.VendorId, d.ProductId, d.Name)));
            Log.Write($"Lights loaded: {devices.Count} device(s), profile='{ProfileStore.Current?.Name ?? "none"}'");
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Status_StatusFailed", ex.Message);
            HasDevices = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplyAsync(Color color, LightScope scope, CancellationToken ct = default)
    {
        var client = AppServices.Fx;
        if (client is null)
        {
            Status = Localizer.Format("Status_ToolsMissing", "alienfx-cli");
            return;
        }

        if (SelectedTarget is null)
            return;

        var r = color.R;
        var g = color.G;
        var b = color.B;
        var hex = $"#{r:X2}{g:X2}{b:X2}";

        try
        {
            if (SelectedTarget.IsWholeSystem || SelectedTarget.Device is null)
            {
                var result = await client.SetAllAsync(r, g, b, ct);
                LastCommand = result.Command;
                Status = result.Success
                    ? Localizer.Format("Status_AppliedWhole", hex)
                    : Localizer.Format("Status_ApplyFailed", FirstLine(result.StandardError));
                return;
            }

            var device = SelectedTarget.Device;
            var lights = FilterLights(device, scope);
            if (lights.Count == 0)
            {
                Status = Localizer.Get("Status_NoMatchingLights");
                return;
            }

            var targets = lights.Select(light => (Device: device.Id, Light: light.Id));
            var outcome = await client.SetLightsAsync(targets, r, g, b, ct);
            LastCommand = outcome.Command;
            Status = outcome.Success
                ? Localizer.Format("Status_AppliedScope", hex, lights.Count, device.DisplayName)
                : Localizer.Format("Status_ApplyFailed", FirstLine(outcome.StandardError));
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Status_ApplyFailed", ex.Message);
        }
    }

    public async Task ApplySelectionAsync(IReadOnlyList<Light> lights, Color color, CancellationToken ct = default)
    {
        var client = AppServices.Fx;
        if (client is null)
        {
            Status = Localizer.Format("Status_ToolsMissing", "alienfx-cli");
            return;
        }

        if (SelectedTarget?.Device is not LightDevice device)
        {
            Status = Localizer.Get("Status_SelectDeviceForKey");
            return;
        }

        if (lights.Count == 0)
            return;

        var r = color.R;
        var g = color.G;
        var b = color.B;
        var hex = $"#{r:X2}{g:X2}{b:X2}";

        try
        {
            var targets = lights.Select(light => (Device: device.Id, Light: light.Id));
            var result = await client.SetLightsAsync(targets, r, g, b, ct);
            LastCommand = result.Command;
            Status = result.Success
                ? Localizer.Format("Status_AppliedKeys", hex, lights.Count, device.DisplayName)
                : Localizer.Format("Status_ApplyFailed", FirstLine(result.StandardError));
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Status_ApplyFailed", ex.Message);
        }
    }

    private static List<Light> FilterLights(LightDevice device, LightScope scope)
    {
        return scope switch
        {
            LightScope.Wasd => device.Lights.Where(l => WasdKeys.Contains(l.Name)).ToList(),
            LightScope.Arrows => device.Lights.Where(l => ArrowKeys.Contains(l.Name)).ToList(),
            LightScope.FunctionKeys => device.Lights.Where(IsFunctionKey).ToList(),
            _ => device.Lights.ToList(),
        };
    }

    private static bool IsFunctionKey(Light light)
    {
        var name = light.Name;
        if (name.Length is < 2 or > 3 || (name[0] != 'F' && name[0] != 'f'))
            return false;
        return int.TryParse(name.AsSpan(1), out var number) && number is >= 1 and <= 12;
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Localizer.Get("Status_UnknownError");

        var line = text.Split('\n')[0].Trim();
        return line.Length == 0 ? Localizer.Get("Status_UnknownError") : line;
    }
}
