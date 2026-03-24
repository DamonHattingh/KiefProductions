using KiefProductions.Data;
using KiefProductions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class ClientsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ClientsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Clients.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.FullName.Contains(search) ||
                                     c.Email.Contains(search) ||
                                     (c.BusinessName != null && c.BusinessName.Contains(search)));

        ViewBag.Search = search;
        return View(await query.OrderBy(c => c.FullName).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var client = await _context.Clients
            .Include(c => c.Quotes).ThenInclude(q => q.Event)
            .Include(c => c.Invoices)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null) return NotFound();
        return View(client);
    }

    public IActionResult Create() => View(new Client());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Client client)
    {
        if (!ModelState.IsValid) return View(client);

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Client '{client.FullName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null) return NotFound();
        return View(client);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Client client)
    {
        if (id != client.Id) return NotFound();
        if (!ModelState.IsValid) return View(client);

        _context.Update(client);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Client '{client.FullName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await _context.Clients
            .Include(c => c.Quotes)
            .Include(c => c.Invoices)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null) return NotFound();

        // Check if client has any quotes or invoices
        if (client.Quotes.Any() || client.Invoices.Any())
        {
            var reasons = new List<string>();
            if (client.Quotes.Any())
                reasons.Add($"has {client.Quotes.Count} active quote(s)");
            if (client.Invoices.Any())
                reasons.Add($"has {client.Invoices.Count} invoice(s)");

            TempData["Error"] = $"Cannot deactivate client '{client.FullName}' because they {string.Join(" and ", reasons)}. Please resolve these records first.";
            return RedirectToAction(nameof(Index));
        }

        client.IsActive = false;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Client '{client.FullName}' deactivated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
