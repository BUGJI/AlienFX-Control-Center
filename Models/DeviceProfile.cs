namespace AlienFXControlCenter.Models;

public sealed class GridCell
{
    public int GridId { get; set; }
    public int Position { get; set; }
}

public sealed class ProfileGrid
{
    public int Id { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ProfileLight
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<GridCell> Cells { get; set; } = new();
}

public sealed class ProfileDevice
{
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ProfileLight> Lights { get; set; } = new();

    public string Key => $"{VendorId}-{ProductId}";
}

public sealed class DeviceProfile
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public List<ProfileGrid> Grids { get; set; } = new();
    public List<ProfileDevice> Devices { get; set; } = new();
    public Dictionary<string, string> FanNames { get; set; } = new();
    public Dictionary<string, string> SensorNames { get; set; } = new();

    public ProfileDevice? FindDevice(int vendorId, int productId)
        => Devices.FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId);

    public override string ToString() => Name;
}
