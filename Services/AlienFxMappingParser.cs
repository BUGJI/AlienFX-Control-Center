using System.Globalization;
using System.Text;
using AlienFXControlCenter.Models;

namespace AlienFXControlCenter.Services;

public static class AlienFxMappingParser
{
    public static List<DeviceProfile> Parse(string csv)
    {
        var profiles = new List<DeviceProfile>();
        DeviceProfile? machine = null;
        ProfileDevice? device = null;

        foreach (var rawLine in csv.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').TrimStart('\uFEFF').Trim();
            if (line.Length == 0)
                continue;

            var fields = SplitFields(line);
            if (fields.Count == 0)
                continue;

            switch (fields[0])
            {
                case "3":
                    machine = new DeviceProfile { Name = fields.Count > 1 ? fields[1] : string.Empty };
                    profiles.Add(machine);
                    device = null;
                    break;

                case "2":
                    if (machine is not null && fields.Count >= 5)
                    {
                        machine.Grids.Add(new ProfileGrid
                        {
                            Id = ParseInt(fields[1]),
                            Columns = ParseInt(fields[2]),
                            Rows = ParseInt(fields[3]),
                            Name = fields[4],
                        });
                    }
                    break;

                case "0":
                    if (machine is not null && fields.Count >= 4)
                    {
                        device = new ProfileDevice
                        {
                            VendorId = ParseInt(fields[1]),
                            ProductId = ParseInt(fields[2]),
                            Name = fields[3],
                        };
                        machine.Devices.Add(device);
                    }
                    break;

                case "1":
                    if (device is not null && fields.Count >= 4)
                    {
                        var light = new ProfileLight
                        {
                            Id = ParseInt(fields[1]),
                            Name = fields[3],
                        };

                        for (var i = 4; i + 1 < fields.Count; i += 2)
                        {
                            light.Cells.Add(new GridCell
                            {
                                GridId = ParseInt(fields[i]),
                                Position = ParseInt(fields[i + 1]),
                            });
                        }

                        device.Lights.Add(light);
                    }
                    break;
            }
        }

        return profiles;
    }

    private static List<string> SplitFields(string line)
    {
        // Fields are wrapped in single quotes and separated by the exact
        // sequence `','`. A literal apostrophe is therefore written as `'''`.
        // Splitting on `','` (instead of toggling quote state) preserves both
        // the apostrophe key and names that legitimately contain commas.
        line = line.Trim();
        if (line.StartsWith('\''))
            line = line[1..];
        if (line.EndsWith('\''))
            line = line[..^1];

        return new List<string>(line.Split("','", StringSplitOptions.None));
    }

    private static int ParseInt(string value)
        => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
