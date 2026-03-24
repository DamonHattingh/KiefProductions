using KiefProductions.Data;
using KiefProductions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;
    public EventsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = _context.Events
            .Include(e => e.Quote).ThenInclude(q => q.Client)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(e => e.EventName.Contains(search) || (e.Venue != null && e.Venue.Contains(search)));

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<EventStatus>(status, out var statusEnum))
            query = query.Where(e => e.Status == statusEnum);

        ViewBag.Search = search;
        ViewBag.Status = status;

        return View(await query.OrderByDescending(e => e.EventDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await _context.Events
            .Include(e => e.Quote).ThenInclude(q => q.Client)
            .Include(e => e.EventStaff).ThenInclude(es => es.StaffMember)
            .Include(e => e.GearBookings).ThenInclude(gb => gb.Gear).ThenInclude(g => g.ProductType)
            .Include(e => e.GearBookings).ThenInclude(gb => gb.RentalGear).ThenInclude(rg => rg.ProductType)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null) return NotFound();
        ViewData["AllStaff"] = await _context.StaffMembers.Where(s => s.IsActive).OrderBy(s => s.FullName).ToListAsync();
        return View(ev);
    }

    public IActionResult Create()
    {
        return View(new Event { EventDate = DateTime.Today.AddDays(7) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Event ev)
    {
        ModelState.Remove("Quote");
        ModelState.Remove("EventStaff");
        ModelState.Remove("GearBookings");

        if (!ModelState.IsValid) return View(ev);

        _context.Events.Add(ev);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Event '{ev.EventName}' created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev == null) return NotFound();
        return View(ev);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Event ev)
    {
        if (id != ev.Id) return NotFound();
        ModelState.Remove("Quote");
        ModelState.Remove("EventStaff");
        ModelState.Remove("GearBookings");

        if (!ModelState.IsValid) return View(ev);

        _context.Update(ev);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, EventStatus status)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev == null) return NotFound();
        ev.Status = status;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Status updated to {status}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _context.Events
            .Include(e => e.EventStaff)
            .Include(e => e.GearBookings)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null) return NotFound();

        // Check if event has a quote
        if (ev.QuoteId.HasValue)
        {
            TempData["Error"] = $"Cannot delete event '{ev.EventName}' because it is linked to a quote. Please delete the quote first or unlink it from the event.";
            return RedirectToAction(nameof(Index));
        }

        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Event '{ev.EventName}' deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Staff Assignment ──────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignStaff(int eventId, int staffMemberId)
    {
        var staff = await _context.StaffMembers.FindAsync(staffMemberId);
        var ev = await _context.Events.FindAsync(eventId);
        if (staff == null || ev == null) return NotFound();

        var existing = await _context.EventStaff
            .AnyAsync(es => es.EventId == eventId && es.StaffMemberId == staffMemberId);

        if (!existing)
        {
            _context.EventStaff.Add(new EventStaff
            {
                EventId = eventId,
                StaffMemberId = staffMemberId,
                RoleOnEvent = staff.Role,
                FlatRateApplied = staff.FlatRate
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{staff.FullName} assigned to event.";
        }
        else
        {
            TempData["Error"] = "This staff member is already assigned.";
        }

        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStaff(int eventStaffId, int eventId)
    {
        var es = await _context.EventStaff.FindAsync(eventStaffId);
        if (es != null) { _context.EventStaff.Remove(es); await _context.SaveChangesAsync(); }
        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    // ── JSON for calendar ────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetCalendarEvents()
    {
        var events = await _context.Events
            .Include(e => e.Quote).ThenInclude(q => q.Client)
            .ToListAsync();

        var result = events.Select(e => new
        {
            id = e.Id,
            title = e.EventName,
            start = e.EventDate.ToString("yyyy-MM-dd"),
            end = e.EventEndDate?.AddDays(1).ToString("yyyy-MM-dd") ?? e.EventDate.AddDays(1).ToString("yyyy-MM-dd"),
            color = e.Status switch
            {
                EventStatus.Confirmed  => "#22c55e",
                EventStatus.Cancelled  => "#ef4444",
                EventStatus.Completed  => "#a855f7",
                EventStatus.InProgress => "#3b82f6",
                _                      => "#FF9000"
            },
            url = $"/Events/Details/{e.Id}",
            extendedProps = new { status = e.Status.ToString(), venue = e.Venue, client = e.Quote?.Client?.FullName }
        });

        return Json(result);
    }
}
