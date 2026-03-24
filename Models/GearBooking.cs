namespace KiefProductions.Models;

public class GearBooking
{
    public int Id { get; set; }

    public int? GearId { get; set; }
    public Gear? Gear { get; set; }

    public int? RentalGearId { get; set; }
    public RentalGear? RentalGear { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public DateTime BookingDate { get; set; }
    public DateTime? BookingDateEnd { get; set; }
}
