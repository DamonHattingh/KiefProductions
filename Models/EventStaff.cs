namespace KiefProductions.Models;

public class EventStaff
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int StaffMemberId { get; set; }
    public StaffMember StaffMember { get; set; } = null!;

    public string? RoleOnEvent { get; set; }
    public decimal FlatRateApplied { get; set; }
}
