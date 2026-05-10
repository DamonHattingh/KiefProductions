using KiefProductions.Data;
using KiefProductions.Models;
using KiefProductions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace KiefProductions.Controllers;

[Authorize]
public class QuotesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PuppeteerPdfService _pdfService;
    private readonly RazorViewRenderer _viewRenderer;

    public QuotesController(ApplicationDbContext context, PuppeteerPdfService pdfService, RazorViewRenderer viewRenderer)
    {
        _context = context;
        _pdfService = pdfService;
        _viewRenderer = viewRenderer;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Event)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(q => q.Client.FullName.Contains(search) || q.QuoteNumber.Contains(search));

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<QuoteStatus>(status, out var statusEnum))
            query = query.Where(q => q.Status == statusEnum);

        ViewBag.Search = search;
        ViewBag.Status = status;

        return View(await query.OrderByDescending(q => q.DateIssued).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Event)
            .Include(q => q.ProfitSummary)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        if (quote.QuoteType == QuoteType.FreeText)
        {
            quote.FreeTextSections = await _context.FreeTextQuoteSections
                .Where(s => s.QuoteId == id)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();
        }
        else
        {
            quote.LineItems = await _context.QuoteLineItems
                .Where(li => li.QuoteId == id)
                .Include(li => li.Category)
                .Include(li => li.ProductType)
                .Include(li => li.Package)
                    .ThenInclude(p => p.PackageItems)
                    .ThenInclude(pi => pi.Gear)
                    .ThenInclude(g => g.ProductType)
                .ToListAsync();
        }

        return View(quote);
    }

    public async Task<IActionResult> Create(int? clientId)
    {
        await PopulateViewBag();
        var quote = new Quote { DateIssued = DateTime.Now, DueDate = DateTime.Now.AddDays(30) };
        if (clientId.HasValue) quote.ClientId = clientId.Value;
        return View(quote);
    }

    [HttpGet]
    public async Task<IActionResult> CreateFreeText(int? clientId)
    {
        await PopulateViewBag();
        var quote = new Quote { DateIssued = DateTime.Now, DueDate = DateTime.Now.AddDays(30), QuoteType = QuoteType.FreeText };
        if (clientId.HasValue) quote.ClientId = clientId.Value;
        return View(quote);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFreeText([FromForm] FreeTextQuoteFormModel form)
    {
        if (form.ClientId <= 0)
        {
            TempData["Error"] = "Please select a client.";
            await PopulateViewBag();
            return View(new Quote { ClientId = form.ClientId, QuoteType = QuoteType.FreeText });
        }

        var quote = new Quote
        {
            ClientId = form.ClientId,
            Discount = form.Discount,
            QuoteType = QuoteType.FreeText,
            Status = form.IsDraft ? QuoteStatus.Draft : QuoteStatus.Pending,
            DateIssued = DateTime.Now,
            DueDate = DateTime.Now.AddDays(30),
            LastSaved = DateTime.Now
        };

        if (!form.IsDraft)
        {
            int count = await _context.Quotes.CountAsync(q => q.Status != QuoteStatus.Draft);
            quote.QuoteNumber = $"QUO-{(count + 142):0000}";
        }

        var sections = string.IsNullOrEmpty(form.SectionsJson)
            ? new List<FreeTextSectionDto>()
            : JsonSerializer.Deserialize<List<FreeTextSectionDto>>(form.SectionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        quote.Subtotal = sections.Sum(s => s.Qty * s.UnitPrice);
        quote.Total = quote.Subtotal - quote.Discount;

        _context.Quotes.Add(quote);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(form.EventName))
        {
            _context.Events.Add(new Event
            {
                EventName = form.EventName,
                EventDate = form.EventDate ?? DateTime.Today,
                EventEndDate = form.EventEndDate,
                Venue = form.Venue,
                StartTime = form.StartTime,
                EndTime = form.EndTime,
                Status = EventStatus.Quoted,
                QuoteId = quote.Id
            });
        }

        foreach (var (s, idx) in sections.Select((s, i) => (s, i)))
        {
            _context.FreeTextQuoteSections.Add(new FreeTextQuoteSection
            {
                QuoteId = quote.Id,
                CategoryName = s.CategoryName,
                DescriptionHtml = s.DescriptionHtml,
                Qty = s.Qty,
                UnitPrice = s.UnitPrice,
                Cost = s.Cost,
                SortOrder = idx
            });
        }

        var revenue = sections.Sum(s => s.Qty * s.UnitPrice);
        var costs = sections.Sum(s => s.Cost);
        var net = revenue - costs;

        _context.ProfitSummaries.Add(new ProfitSummary
        {
            QuoteId = quote.Id,
            TotalRevenue = revenue,
            TotalCost = costs,
            TotalExpenses = 0,
            NetProfit = net,
            ProfitMargin = revenue > 0 ? (net / revenue * 100) : 0
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = form.IsDraft ? "Draft saved." : $"Quote {quote.QuoteNumber} created.";
        return RedirectToAction(nameof(Details), new { id = quote.Id });
    }

    [HttpGet]
    public async Task<IActionResult> EditFreeText(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Event)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        quote.FreeTextSections = await _context.FreeTextQuoteSections
            .Where(s => s.QuoteId == id)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        await PopulateViewBag();
        return View(quote);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditFreeText(int id, [FromForm] FreeTextQuoteFormModel form)
    {
        var quote = await _context.Quotes
            .Include(q => q.Event)
            .Include(q => q.FreeTextSections)
            .Include(q => q.ProfitSummary)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        quote.ClientId = form.ClientId;
        quote.Discount = form.Discount;
        quote.LastSaved = DateTime.Now;

        if (!form.IsDraft && quote.Status == QuoteStatus.Draft)
        {
            int count = await _context.Quotes.CountAsync(q => q.Status != QuoteStatus.Draft && q.Id != id);
            quote.QuoteNumber ??= $"QUO-{(count + 142):0000}";
            quote.Status = QuoteStatus.Pending;
        }

        if (!string.IsNullOrEmpty(form.EventName))
        {
            if (quote.Event != null)
            {
                quote.Event.EventName = form.EventName;
                quote.Event.EventDate = form.EventDate ?? DateTime.Today;
                quote.Event.EventEndDate = form.EventEndDate;
                quote.Event.Venue = form.Venue;
                quote.Event.StartTime = form.StartTime;
                quote.Event.EndTime = form.EndTime;
            }
            else
            {
                _context.Events.Add(new Event
                {
                    EventName = form.EventName,
                    EventDate = form.EventDate ?? DateTime.Today,
                    EventEndDate = form.EventEndDate,
                    Venue = form.Venue,
                    StartTime = form.StartTime,
                    EndTime = form.EndTime,
                    Status = EventStatus.Quoted,
                    QuoteId = quote.Id
                });
            }
        }

        _context.FreeTextQuoteSections.RemoveRange(quote.FreeTextSections);

        var sections = string.IsNullOrEmpty(form.SectionsJson)
            ? new List<FreeTextSectionDto>()
            : JsonSerializer.Deserialize<List<FreeTextSectionDto>>(form.SectionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        foreach (var (s, idx) in sections.Select((s, i) => (s, i)))
        {
            _context.FreeTextQuoteSections.Add(new FreeTextQuoteSection
            {
                QuoteId = quote.Id,
                CategoryName = s.CategoryName,
                DescriptionHtml = s.DescriptionHtml,
                Qty = s.Qty,
                UnitPrice = s.UnitPrice,
                Cost = s.Cost,
                SortOrder = idx
            });
        }

        quote.Subtotal = sections.Sum(s => s.Qty * s.UnitPrice);
        quote.Total = quote.Subtotal - quote.Discount;

        var revenue = sections.Sum(s => s.Qty * s.UnitPrice);
        var costs = sections.Sum(s => s.Cost);
        var net = revenue - costs;

        if (quote.ProfitSummary != null)
        {
            quote.ProfitSummary.TotalRevenue = revenue;
            quote.ProfitSummary.TotalCost = costs;
            quote.ProfitSummary.TotalExpenses = 0;
            quote.ProfitSummary.NetProfit = net;
            quote.ProfitSummary.ProfitMargin = revenue > 0 ? (net / revenue * 100) : 0;
        }
        else
        {
            _context.ProfitSummaries.Add(new ProfitSummary
            {
                QuoteId = quote.Id,
                TotalRevenue = revenue,
                TotalCost = costs,
                TotalExpenses = 0,
                NetProfit = net,
                ProfitMargin = revenue > 0 ? (net / revenue * 100) : 0
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = form.IsDraft ? "Draft saved." : "Quote updated.";
        return RedirectToAction(nameof(Details), new { id = quote.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] QuoteFormModel form)
    {
        if (form.ClientId <= 0)
        {
            TempData["Error"] = "Please select a client.";
            await PopulateViewBag();
            return View(new Quote { ClientId = form.ClientId });
        }

        var quote = new Quote
        {
            ClientId = form.ClientId,
            Discount = form.Discount,
            Status = form.IsDraft ? QuoteStatus.Draft : QuoteStatus.Pending,
            DateIssued = DateTime.Now,
            DueDate = DateTime.Now.AddDays(30),
            LastSaved = DateTime.Now
        };

        if (!form.IsDraft)
        {
            int count = await _context.Quotes.CountAsync(q => q.Status != QuoteStatus.Draft);
            quote.QuoteNumber = $"QUO-{(count + 142):0000}";
        }

        var lineItems = string.IsNullOrEmpty(form.LineItemsJson)
            ? new List<QuoteLineItemDto>()
            : JsonSerializer.Deserialize<List<QuoteLineItemDto>>(form.LineItemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        quote.Subtotal = lineItems.Where(li => li.ItemType != "Expense").Sum(li => li.Quantity * li.UnitPrice);
        quote.Total = quote.Subtotal - quote.Discount;

        _context.Quotes.Add(quote);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(form.EventName))
        {
            _context.Events.Add(new Event
            {
                EventName = form.EventName,
                EventDate = form.EventDate ?? DateTime.Today,
                EventEndDate = form.EventEndDate,
                Venue = form.Venue,
                StartTime = form.StartTime,
                EndTime = form.EndTime,
                Status = EventStatus.Quoted,
                QuoteId = quote.Id
            });
        }

        foreach (var li in lineItems)
        {
            _context.QuoteLineItems.Add(new QuoteLineItem
            {
                QuoteId = quote.Id,
                CategoryId = li.CategoryId > 0 ? li.CategoryId : null,
                ProductTypeId = li.ProductTypeId > 0 ? li.ProductTypeId : null,
                PackageId = li.PackageId > 0 ? li.PackageId : null,
                ProductName = li.ProductName ?? string.Empty,
                ProductDescription = li.ProductDescription,
                UnitPrice = li.UnitPrice,
                CostPrice = li.CostPrice,
                Quantity = li.Quantity,
                IsRental = li.ItemType == "Rental",
                IsExpense = li.ItemType == "Expense",
                ItemType = li.ItemType switch
                {
                    "Rental" => LineItemType.Rental,
                    "Custom" => LineItemType.Custom,
                    "Expense" => LineItemType.Expense,
                    "Package" => LineItemType.Package,
                    _ => LineItemType.Gear
                }
            });
        }

        var revenue = lineItems.Where(li => li.ItemType != "Expense").Sum(li => li.Quantity * li.UnitPrice);
        var costs = lineItems.Where(li => li.ItemType == "Rental" || li.ItemType == "Custom").Sum(li => li.Quantity * (li.CostPrice ?? 0));
        var expenses = lineItems.Where(li => li.ItemType == "Expense").Sum(li => li.Quantity * li.UnitPrice);
        var net = revenue - costs - expenses;

        _context.ProfitSummaries.Add(new ProfitSummary
        {
            QuoteId = quote.Id,
            TotalRevenue = revenue,
            TotalCost = costs,
            TotalExpenses = expenses,
            NetProfit = net,
            ProfitMargin = revenue > 0 ? (net / revenue * 100) : 0
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = form.IsDraft ? "Draft saved." : $"Quote {quote.QuoteNumber} created.";
        return RedirectToAction(nameof(Details), new { id = quote.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Event)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        // Separate query prevents Cartesian explosion from multiple ThenIncludes
        quote.LineItems = await _context.QuoteLineItems
            .Where(li => li.QuoteId == id)
            .Include(li => li.Category)
            .Include(li => li.ProductType)
            .ToListAsync();

        await PopulateViewBag();
        return View(quote);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromForm] QuoteFormModel form)
    {
        var quote = await _context.Quotes
            .Include(q => q.Event)
            .Include(q => q.LineItems)
            .Include(q => q.ProfitSummary)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        quote.ClientId = form.ClientId;
        quote.Discount = form.Discount;
        quote.LastSaved = DateTime.Now;

        if (!form.IsDraft && quote.Status == QuoteStatus.Draft)
        {
            int count = await _context.Quotes.CountAsync(q => q.Status != QuoteStatus.Draft && q.Id != id);
            quote.QuoteNumber ??= $"QUO-{(count + 142):0000}";
            quote.Status = QuoteStatus.Pending;
        }

        if (!string.IsNullOrEmpty(form.EventName))
        {
            if (quote.Event != null)
            {
                quote.Event.EventName = form.EventName;
                quote.Event.EventDate = form.EventDate ?? DateTime.Today;
                quote.Event.EventEndDate = form.EventEndDate;
                quote.Event.Venue = form.Venue;
                quote.Event.StartTime = form.StartTime;
                quote.Event.EndTime = form.EndTime;
            }
            else
            {
                _context.Events.Add(new Event
                {
                    EventName = form.EventName,
                    EventDate = form.EventDate ?? DateTime.Today,
                    EventEndDate = form.EventEndDate,
                    Venue = form.Venue,
                    StartTime = form.StartTime,
                    EndTime = form.EndTime,
                    Status = EventStatus.Quoted,
                    QuoteId = quote.Id
                });
            }
        }

        _context.QuoteLineItems.RemoveRange(quote.LineItems);

        var lineItems = string.IsNullOrEmpty(form.LineItemsJson)
            ? new List<QuoteLineItemDto>()
            : JsonSerializer.Deserialize<List<QuoteLineItemDto>>(form.LineItemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        foreach (var li in lineItems)
        {
            _context.QuoteLineItems.Add(new QuoteLineItem
            {
                QuoteId = quote.Id,
                CategoryId = li.CategoryId > 0 ? li.CategoryId : null,
                ProductTypeId = li.ProductTypeId > 0 ? li.ProductTypeId : null,
                PackageId = li.PackageId > 0 ? li.PackageId : null,
                ProductName = li.ProductName ?? string.Empty,
                ProductDescription = li.ProductDescription,
                UnitPrice = li.UnitPrice,
                CostPrice = li.CostPrice,
                Quantity = li.Quantity,
                IsRental = li.ItemType == "Rental",
                IsExpense = li.ItemType == "Expense",
                ItemType = li.ItemType switch
                {
                    "Rental" => LineItemType.Rental,
                    "Custom" => LineItemType.Custom,
                    "Expense" => LineItemType.Expense,
                    "Package" => LineItemType.Package,
                    _ => LineItemType.Gear
                }
            });
        }

        quote.Subtotal = lineItems.Where(li => li.ItemType != "Expense").Sum(li => li.Quantity * li.UnitPrice);
        quote.Total = quote.Subtotal - quote.Discount;

        var revenue = lineItems.Where(li => li.ItemType != "Expense").Sum(li => li.Quantity * li.UnitPrice);
        var costs = lineItems.Where(li => li.ItemType is "Rental" or "Custom").Sum(li => li.Quantity * (li.CostPrice ?? 0));
        var expenses = lineItems.Where(li => li.ItemType == "Expense").Sum(li => li.Quantity * li.UnitPrice);
        var net = revenue - costs - expenses;

        if (quote.ProfitSummary != null)
        {
            quote.ProfitSummary.TotalRevenue = revenue;
            quote.ProfitSummary.TotalCost = costs;
            quote.ProfitSummary.TotalExpenses = expenses;
            quote.ProfitSummary.NetProfit = net;
            quote.ProfitSummary.ProfitMargin = revenue > 0 ? (net / revenue * 100) : 0;
        }
        else
        {
            _context.ProfitSummaries.Add(new ProfitSummary
            {
                QuoteId = quote.Id,
                TotalRevenue = revenue,
                TotalCost = costs,
                TotalExpenses = expenses,
                NetProfit = net,
                ProfitMargin = revenue > 0 ? (net / revenue * 100) : 0
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = form.IsDraft ? "Draft saved." : "Quote updated.";
        return RedirectToAction(nameof(Details), new { id = quote.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, QuoteStatus status)
    {
        var quote = await _context.Quotes.FindAsync(id);
        if (quote == null) return NotFound();
        quote.Status = status;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Status updated to {status}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertToInvoice(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Event)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        int count = await _context.Invoices.CountAsync();
        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{(count + 131):0000}",
            ClientId = quote.ClientId,
            QuoteId = quote.Id,
            Subtotal = quote.Subtotal,
            Discount = quote.Discount,
            Total = quote.Total,
            Status = InvoiceStatus.Draft,
            DateIssued = DateTime.Now,
            DueDate = DateTime.Now.AddDays(30)
        };

        if (quote.QuoteType == QuoteType.FreeText)
        {
            var sections = await _context.FreeTextQuoteSections
                .Where(s => s.QuoteId == id)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            invoice.LineItems = sections.Select(s => new InvoiceLineItem
            {
                ProductName = s.CategoryName,
                ProductDescription = s.DescriptionHtml,
                UnitPrice = s.Qty * s.UnitPrice,
                CostPrice = s.Cost,
                Quantity = 1,
                IsRental = false,
                ItemType = LineItemType.Custom
            }).ToList();
        }
        else
        {
            var lineItems = await _context.QuoteLineItems
                .Where(li => li.QuoteId == id)
                .ToListAsync();

            invoice.LineItems = lineItems
                .Where(li => !li.IsExpense)
                .Select(li => new InvoiceLineItem
                {
                    CategoryId = li.CategoryId,
                    ProductTypeId = li.ProductTypeId,
                    ProductName = li.ProductName,
                    ProductDescription = li.ProductDescription,
                    UnitPrice = li.UnitPrice,
                    CostPrice = li.CostPrice,
                    Quantity = li.Quantity,
                    IsRental = li.IsRental,
                    ItemType = li.ItemType
                }).ToList();
        }

        quote.Status = QuoteStatus.Converted;
        if (quote.Event != null) quote.Event.Status = EventStatus.Confirmed;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Invoice {invoice.InvoiceNumber} created.";
        return RedirectToAction("Details", "Invoices", new { id = invoice.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        // AsNoTracking prevents EF from re-saving source items when we save the new quote
        var source = await _context.Quotes
            .AsNoTracking()
            .Include(q => q.Event)
            .Include(q => q.ProfitSummary)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (source == null) return NotFound();

        var newQuote = new Quote
        {
            ClientId = source.ClientId,
            Discount = source.Discount,
            QuoteType = source.QuoteType,
            Status = QuoteStatus.Draft,
            DateIssued = DateTime.Now,
            DueDate = DateTime.Now.AddDays(30),
            LastSaved = DateTime.Now,
            Subtotal = source.Subtotal,
            Total = source.Total
        };

        _context.Quotes.Add(newQuote);
        await _context.SaveChangesAsync();

        if (source.Event != null)
        {
            _context.Events.Add(new Event
            {
                EventName = source.Event.EventName,
                EventDate = source.Event.EventDate,
                EventEndDate = source.Event.EventEndDate,
                Venue = source.Event.Venue,
                StartTime = source.Event.StartTime,
                EndTime = source.Event.EndTime,
                Notes = source.Event.Notes,
                Status = EventStatus.Quoted,
                QuoteId = newQuote.Id
            });
        }

        if (source.QuoteType == QuoteType.FreeText)
        {
            var sourceSections = await _context.FreeTextQuoteSections
                .AsNoTracking()
                .Where(s => s.QuoteId == id)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            foreach (var s in sourceSections)
            {
                _context.FreeTextQuoteSections.Add(new FreeTextQuoteSection
                {
                    QuoteId = newQuote.Id,
                    CategoryName = s.CategoryName,
                    DescriptionHtml = s.DescriptionHtml,
                    Qty = s.Qty,
                    UnitPrice = s.UnitPrice,
                    Cost = s.Cost,
                    SortOrder = s.SortOrder
                });
            }
        }
        else
        {
            var sourceLineItems = await _context.QuoteLineItems
                .AsNoTracking()
                .Where(li => li.QuoteId == id)
                .ToListAsync();

            foreach (var li in sourceLineItems)
            {
                _context.QuoteLineItems.Add(new QuoteLineItem
                {
                    QuoteId = newQuote.Id,
                    CategoryId = li.CategoryId,
                    ProductTypeId = li.ProductTypeId,
                    PackageId = li.PackageId,
                    ProductName = li.ProductName,
                    ProductDescription = li.ProductDescription,
                    UnitPrice = li.UnitPrice,
                    CostPrice = li.CostPrice,
                    Quantity = li.Quantity,
                    IsRental = li.IsRental,
                    IsExpense = li.IsExpense,
                    ItemType = li.ItemType
                });
            }
        }

        if (source.ProfitSummary != null)
        {
            _context.ProfitSummaries.Add(new ProfitSummary
            {
                QuoteId = newQuote.Id,
                TotalRevenue = source.ProfitSummary.TotalRevenue,
                TotalCost = source.ProfitSummary.TotalCost,
                TotalExpenses = source.ProfitSummary.TotalExpenses,
                NetProfit = source.ProfitSummary.NetProfit,
                ProfitMargin = source.ProfitSummary.ProfitMargin
            });
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Quote duplicated as draft. Update the details and save.";
        var editAction = newQuote.QuoteType == QuoteType.FreeText ? nameof(EditFreeText) : nameof(Edit);
        return RedirectToAction(editAction, new { id = newQuote.Id });
    }

    [HttpGet]
    public async Task<IActionResult> CanDelete(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Event)
            .Include(q => q.Invoice)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null)
        {
            return NotFound();
        }

        var canDelete = true;
        var reasons = new List<string>();

        if (quote.Event != null)
        {
            canDelete = false;
            reasons.Add("� An event is associated with this quote");
        }

        if (quote.Invoice != null)
        {
            canDelete = false;
            reasons.Add("� An invoice has been created from this quote");
        }

        // Add more conditions if needed
        if (quote.Status == QuoteStatus.Converted)
        {
            canDelete = false;
            reasons.Add("� This quote has been converted to an invoice");
        }

        return Json(new
        {
            canDelete = canDelete,
            reason = string.Join("<br>", reasons)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Event)
            .Include(q => q.Invoice)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null)
        {
            return NotFound();
        }

        // Check if quote can be deleted
        if (quote.Event != null)
        {
            TempData["Error"] = "Cannot delete quote because it has an associated event.";
            return RedirectToAction(nameof(Index));
        }

        if (quote.Invoice != null)
        {
            TempData["Error"] = "Cannot delete quote because an invoice has been created from it.";
            return RedirectToAction(nameof(Index));
        }

        if (quote.Status == QuoteStatus.Converted)
        {
            TempData["Error"] = "Cannot delete quote because it has been converted to an invoice.";
            return RedirectToAction(nameof(Index));
        }

        _context.Quotes.Remove(quote);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GeneratePdf(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Event)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        quote.LineItems = await _context.QuoteLineItems
            .Where(li => li.QuoteId == id)
            .Include(li => li.Category)
            .Include(li => li.ProductType)
            .Include(li => li.Package)
                .ThenInclude(p => p.PackageItems)
                .ThenInclude(pi => pi.Gear)
                .ThenInclude(g => g.ProductType)
            .ToListAsync();

        if (quote.QuoteType == QuoteType.FreeText)
        {
            quote.FreeTextSections = await _context.FreeTextQuoteSections
                .Where(s => s.QuoteId == id)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();
        }
        else
        {
            quote.LineItems = await _context.QuoteLineItems
                .Where(li => li.QuoteId == id)
                .Include(li => li.Category)
                .Include(li => li.ProductType)
                .Include(li => li.Package)
                    .ThenInclude(p => p.PackageItems)
                    .ThenInclude(pi => pi.Gear)
                    .ThenInclude(g => g.ProductType)
                .ToListAsync();
        }

        var viewBag = new Dictionary<string, object?>
        {
            ["LogoBase64"] = _pdfService.GetLogoBase64(),
            ["BankLogoBase64"] = _pdfService.GetBankLogoBase64()
        };

        var viewName = quote.QuoteType == QuoteType.FreeText
            ? "~/Views/Pdf/FreeTextQuotePdf.cshtml"
            : "~/Views/Pdf/QuotePdf.cshtml";

        var html = await _viewRenderer.RenderViewToStringAsync(viewName, quote, viewBag);
        var pdf = await _pdfService.GeneratePdfFromHtmlAsync(html);
        var fileName = $"{quote.QuoteNumber ?? "Draft"} - {quote.Client?.FullName}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> GetCatalog()
    {
        var categories = await _context.Categories
            .Include(c => c.ProductTypes).ThenInclude(pt => pt.Gears)
            .Include(c => c.ProductTypes).ThenInclude(pt => pt.RentalGears)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var packages = await _context.Packages
            .Where(p => p.IsActive)
            .Include(p => p.PackageItems).ThenInclude(pi => pi.Gear).ThenInclude(g => g.ProductType)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var result = new
        {
            categories = categories.Select(c => new
            {
                c.Id,
                c.Name,
                productTypes = c.ProductTypes.Select(pt => new
                {
                    pt.Id,
                    pt.Name,
                    gears = pt.Gears.Select(g => new { g.Id, g.Name, g.Description, g.MarketPrice }),
                    rentalGears = pt.RentalGears.Select(rg => new { rg.Id, rg.Name, rg.Description, rg.MarketPrice, rg.OurCost, rg.Supplier })
                })
            }),
            packages = packages.Select(p => new
            {
                p.Id,
                p.Name,
                p.BasePrice,
                items = p.PackageItems.Select(pi => new
                {
                    pi.Id,
                    pi.Quantity,
                    pi.CustomDescription,
                    gearName = pi.Gear?.Name,
                    typeName = pi.Gear?.ProductType?.Name
                })
            })
        };

        return Json(result);
    }

    private async Task PopulateViewBag()
    {
        ViewBag.Clients = await _context.Clients.Where(c => c.IsActive).OrderBy(c => c.FullName).ToListAsync();
    }

    [HttpGet]
    public async Task<IActionResult> PreviewPdf(int id)
    {
        var quote = await _context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Event)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null) return NotFound();

        if (quote.QuoteType == QuoteType.FreeText)
        {
            quote.FreeTextSections = await _context.FreeTextQuoteSections
                .Where(s => s.QuoteId == id)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();
        }
        else
        {
            quote.LineItems = await _context.QuoteLineItems
                .Where(li => li.QuoteId == id)
                .Include(li => li.Category)
                .Include(li => li.ProductType)
                .Include(li => li.Package)
                    .ThenInclude(p => p.PackageItems)
                    .ThenInclude(pi => pi.Gear)
                    .ThenInclude(g => g.ProductType)
                .ToListAsync();
        }

        ViewBag.LogoBase64 = _pdfService.GetLogoBase64();
        ViewBag.BankLogoBase64 = _pdfService.GetBankLogoBase64();

        var viewName = quote.QuoteType == QuoteType.FreeText
            ? "~/Views/Pdf/FreeTextQuotePdf.cshtml"
            : "~/Views/Pdf/QuotePdf.cshtml";

        return View(viewName, quote);
    }
}

public class QuoteFormModel
{
    public int ClientId { get; set; }
    public decimal Discount { get; set; }
    public bool IsDraft { get; set; }
    public string? EventName { get; set; }
    public DateTime? EventDate { get; set; }
    public DateTime? EventEndDate { get; set; }
    public string? Venue { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? LineItemsJson { get; set; }
}

public class QuoteLineItemDto
{
    public int CategoryId { get; set; }
    public int ProductTypeId { get; set; }
    public int PackageId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public string ItemType { get; set; } = "Gear";
}

public class FreeTextQuoteFormModel
{
    public int ClientId { get; set; }
    public decimal Discount { get; set; }
    public bool IsDraft { get; set; }
    public string? EventName { get; set; }
    public DateTime? EventDate { get; set; }
    public DateTime? EventEndDate { get; set; }
    public string? Venue { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? SectionsJson { get; set; }
}

public class FreeTextSectionDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public decimal Qty { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Cost { get; set; }
}