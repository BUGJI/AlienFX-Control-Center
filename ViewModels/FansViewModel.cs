using System.Collections.ObjectModel;
using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlienFXControlCenter.ViewModels;

public sealed partial class FanItem : ObservableObject
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string MaxRpmText { get; init; } = string.Empty;

    [ObservableProperty]
    private string rpmText = string.Empty;

    [ObservableProperty]
    private double boost;
}

public sealed partial class FansViewModel : ObservableObject
{
    public ObservableCollection<FanItem> Fans { get; } = new();

    public ObservableCollection<SensorReading> Sensors { get; } = new();

    public bool ShowDebug => SettingsService.Current.Value.ShowDebugPanel;

    [ObservableProperty]
    private string status = Localizer.Get("Status_Ready");

    [ObservableProperty]
    private string hardwareSummary = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasHardware;

    [ObservableProperty]
    private string rawOutput = string.Empty;

    [ObservableProperty]
    private string lastCommand = string.Empty;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var client = AppServices.Fan;
        if (client is null)
        {
            Status = Localizer.Format("Status_ToolsMissing", "alienfan-cli");
            HasHardware = false;
            return;
        }

        IsBusy = true;
        try
        {
            var snapshot = await client.GetSnapshotAsync(ct);
            var profile = ProfileStore.Current;

            Fans.Clear();
            foreach (var fan in snapshot.Fans)
            {
                var name = fan.DisplayName;
                if (profile is not null && profile.FanNames.TryGetValue(fan.Id.ToString(), out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                    name = mapped;

                Fans.Add(new FanItem
                {
                    Id = fan.Id,
                    DisplayName = name,
                    RpmText = fan.RpmText,
                    MaxRpmText = fan.MaxRpm > 0 ? $"max {fan.MaxRpm} RPM" : string.Empty,
                    Boost = fan.Boost,
                });
            }

            Sensors.Clear();
            foreach (var sensor in snapshot.Sensors)
            {
                var name = sensor.Name;
                if (profile is not null && profile.SensorNames.TryGetValue(sensor.Name, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                    name = mapped;

                Sensors.Add(new SensorReading
                {
                    Id = sensor.Id,
                    Name = name,
                    Temperature = sensor.Temperature,
                });
            }

            RawOutput = snapshot.Raw;
            HasHardware = snapshot.FanCount > 0 || snapshot.SensorCount > 0;
            HardwareSummary = HasHardware
                ? Localizer.Format("Status_FanSummary", snapshot.FanCount, snapshot.SensorCount, snapshot.PowerCount)
                : Localizer.Get("Status_NoFanHardware");
            Status = snapshot.Fans.Count > 0 ? Localizer.Get("Status_FansLoaded") : HardwareSummary;
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Status_LoadFailed", ex.Message);
            HasHardware = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplyAsync(CancellationToken ct = default)
    {
        var client = AppServices.Fan;
        if (client is null)
        {
            Status = Localizer.Format("Status_ToolsMissing", "alienfan-cli");
            return;
        }

        if (Fans.Count == 0)
        {
            Status = Localizer.Get("Status_NoFans");
            return;
        }

        IsBusy = true;
        try
        {
            var percents = Fans.OrderBy(f => f.Id).Select(f => (int)Math.Round(f.Boost)).ToList();
            var result = await client.SetFansAsync(percents, ct);
            LastCommand = result.Command;
            Status = result.Success
                ? Localizer.Format("Status_FanBoostSet", string.Join(", ", percents))
                : Localizer.Format("Status_ApplyFailed", FirstLine(result.StandardError));
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Status_ApplyFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UnlockAsync(CancellationToken ct = default)
    {
        var client = AppServices.Fan;
        if (client is null)
        {
            Status = Localizer.Format("Status_ToolsMissing", "alienfan-cli");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await client.UnlockAsync(ct);
            LastCommand = result.Command;
            Status = result.Success
                ? Localizer.Format("Status_UnlockDone", FirstLine(result.StandardOutput))
                : Localizer.Format("Status_UnlockFailed", FirstLine(result.StandardError));
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Status_UnlockFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Localizer.Get("Status_UnknownError");

        var line = text.Split('\n')[0].Trim();
        return line.Length == 0 ? Localizer.Get("Status_UnknownError") : line;
    }
}
