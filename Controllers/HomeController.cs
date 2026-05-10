using KiefProductions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KiefProductions.Models;

namespace KiefProductions.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalClients     = await _context.Clients.CountAsync(c => c.IsActive);
        ViewBag.TotalQuotes      = await _context.Quotes.CountAsync();
        ViewBag.TotalInvoices    = await _context.Invoices.CountAsync();
        ViewBag.PendingQuotes    = await _context.Quotes.CountAsync(q => q.Status == QuoteStatus.Pending);
        ViewBag.OverdueInvoices  = await _context.Invoices.CountAsync(i => i.Status == InvoiceStatus.Overdue);

        ViewBag.NetProfit = await _context.ProfitSummaries
            .Where(p => p.Quote.Invoice != null && p.Quote.Invoice.Status == InvoiceStatus.Paid)
            .SumAsync(p => (decimal?)p.NetProfit) ?? 0;

        ViewBag.TotalRevenue = await _context.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid)
            .SumAsync(i => (decimal?)i.Total) ?? 0;

        ViewBag.OutstandingBalance = await _context.Invoices
            .Where(i => i.Status != InvoiceStatus.Paid)
            .SumAsync(i => (decimal?)(i.Total - i.AmountPaid)) ?? 0;

        ViewBag.RecentQuotes = await _context.Quotes
            .Include(q => q.Client)
            .OrderByDescending(q => q.DateIssued)
            .Take(5)
            .ToListAsync();

        ViewBag.UpcomingEvents = await _context.Events
            .Where(e => e.EventDate >= DateTime.Today && e.Status != EventStatus.Cancelled)
            .OrderBy(e => e.EventDate)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentInvoices = await _context.Invoices
            .Include(i => i.Client)
            .OrderByDescending(i => i.DateIssued)
            .Take(5)
            .ToListAsync();

        return View();
    }
}
