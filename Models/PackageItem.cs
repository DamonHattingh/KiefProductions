namespace KiefProductions.Models;

public class PackageItem
{
    public int Id { get; set; }

    public int PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public int? GearId { get; set; }
    public Gear? Gear { get; set; }

    public int Quantity { get; set; } = 1;
    public string? CustomDescription { get; set; }
}
