using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class Event
{
    public int Id { get; set; }

    [Required]
    public string EventName { get; set; } = string.Empty;

    [Required]
    public DateTime EventDate { get; set; }

    public DateTime? EventEndDate { get; set; }
    public string? Venue { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? Notes { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Quoted;

    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public ICollection<EventStaff> EventStaff { get; set; } = new List<EventStaff>();
    public ICollection<GearBooking> GearBookings { get; set; } = new List<GearBooking>();
    public ICollection<VendorDocument> VendorDocuments { get; set; } = new List<VendorDocument>();
}

public enum EventStatus
{
    Quoted,
    Confirmed,
    InProgress,
    Completed,
    Cancelled
}
