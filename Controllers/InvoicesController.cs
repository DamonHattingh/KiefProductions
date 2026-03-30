using KiefProductions.Data;
using KiefProductions.Models;
using KiefProductions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class InvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PuppeteerPdfService _pdfService;
    private readonly RazorViewRenderer _viewRenderer;

    public InvoicesController(ApplicationDbContext context, PuppeteerPdfService pdfService, RazorViewRenderer viewRenderer)
    {
        _context = context;
        _pdfService = pdfService;
        _viewRenderer = viewRenderer;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = _context.Invoices
            .Include(i => i.Client)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(i => i.Client.FullName.Contains(search) || i.InvoiceNumber.Contains(search));

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<InvoiceStatus>(status, out var statusEnum))
            query = query.Where(i => i.Status == statusEnum);

        var now = DateTime.Now;
        var overdueInvoices = await _context.Invoices
            .Where(i => i.DueDate < now && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Overdue)
            .ToListAsync();
        foreach (var inv in overdueInvoices) inv.Status = InvoiceStatus.Overdue;
        if (overdueInvoices.Any()) await _context.SaveChangesAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;

        return View(await query.OrderByDescending(i => i.DateIssued).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .Include(i => i.Quote)
                .ThenInclude(q => q.Event)
            .Include(i => i.Quote)
                .ThenInclude(q => q.FreeTextSections)
            .Include(i => i.LineItems)
                .ThenInclude(li => li.Category)
            .Include(i => i.LineItems)
                .ThenInclude(li => li.ProductType)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        return View(invoice);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, InvoiceStatus status)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();
        invoice.Status = status;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Status updated to {status}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int invoiceId, string amount, string? method, string? notes, DateTime paymentDate)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null) return NotFound();

        var cleanAmount = amount?.Replace(" ", "").Replace(",", ".") ?? "0";
        if (!decimal.TryParse(cleanAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsedAmount) || parsedAmount <= 0)
        {
            TempData["Error"] = "Payment amount must be greater than zero.";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        _context.Payments.Add(new Payment
        {
            InvoiceId = invoiceId,
            Amount = parsedAmount,
            Method = method,
            Notes = notes,
            PaymentDate = paymentDate == default ? DateTime.Now : paymentDate
        });

        invoice.AmountPaid += parsedAmount;

        if (invoice.AmountPaid >= invoice.Total)
            invoice.Status = InvoiceStatus.Paid;
        else if (invoice.AmountPaid > 0)
            invoice.Status = InvoiceStatus.PartiallyPaid;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Payment of R {parsedAmount:N2} recorded.";
        return RedirectToAction(nameof(Details), new { id = invoiceId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePayment(int paymentId, int invoiceId)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment != null)
        {
            var invoice = await _context.Invoices.FindAsync(invoiceId);
            if (invoice != null)
            {
                invoice.AmountPaid -= payment.Amount;
                if (invoice.AmountPaid <= 0) { invoice.AmountPaid = 0; invoice.Status = InvoiceStatus.Sent; }
                else if (invoice.AmountPaid < invoice.Total) invoice.Status = InvoiceStatus.PartiallyPaid;
            }
            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Payment removed.";
        }
        return RedirectToAction(nameof(Details), new { id = invoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> GeneratePdf(int id)
    {
        var invoice = await _context.Invoices
    .Include(i => i.Client)
    .Include(i => i.Quote).ThenInclude(q => q.Event)
    .Include(i => i.Quote).ThenInclude(q => q.FreeTextSections) // ?? add this
    .Include(i => i.LineItems).ThenInclude(li => li.Category)
    .Include(i => i.LineItems).ThenInclude(li => li.ProductType)
    .Include(i => i.Payments)
    .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();

        var viewBag = new Dictionary<string, object?>
        {
            ["LogoBase64"] = _pdfService.GetLogoBase64(),
            ["BankLogoBase64"] = _pdfService.GetBankLogoBase64()
        };

        var html = await _viewRenderer.RenderViewToStringAsync("~/Views/Pdf/InvoicePdf.cshtml", invoice, viewBag);
        var pdf = await _pdfService.GeneratePdfFromHtmlAsync(html);
        var fileName = $"{invoice.InvoiceNumber} - {invoice.Client?.FullName}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice != null)
        {
            _context.Payments.RemoveRange(invoice.Payments);
            _context.InvoiceLineItems.RemoveRange(invoice.LineItems);
            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Invoice deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}