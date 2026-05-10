using KiefProductions.Data;
using KiefProductions.Models;
using KiefProductions.Services;
using KiefProductions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PuppeteerPdfService _pdf;
    private readonly RazorViewRenderer _renderer;

    public ReportsController(ApplicationDbContext context, PuppeteerPdfService pdf, RazorViewRenderer renderer)
    {
        _context = context;
        _pdf = pdf;
        _renderer = renderer;
    }

    // ── Index ────────────────────────────────────────────────────────────────

    public IActionResult Index()
    {
        ViewData["Title"] = "Reports";
        return View();
    }

    // ── Business Dashboard ───────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> BusinessDashboard(DateTime? startDate, DateTime? endDate, bool exportPdf = false)
    {
        var start = startDate ?? DateTime.Today.AddMonths(-6);
        var end = endDate ?? DateTime.Today;

        var vm = await BuildBusinessDashboard(start, end);

        if (exportPdf)
        {
            var html = await _renderer.RenderViewToStringAsync("~/Views/Pdf/BusinessDashboardPdf.cshtml", vm);
            var bytes = await _pdf.GeneratePdfFromHtmlAsync(html);
            return File(bytes, "application/pdf", $"Business-Report-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
        }

        ViewData["Title"] = "Business Dashboard";
        return View(vm);
    }

    private async Task<BusinessDashboardViewModel> BuildBusinessDashboard(DateTime start, DateTime end)
    {
        var invoices = await _context.Invoices
            .Include(i => i.Payments)
            .Where(i => i.DateIssued >= start && i.DateIssued <= end)
            .ToListAsync();

        var quotes = await _context.Quotes
            .Include(q => q.ProfitSummary)
            .Where(q => q.DateIssued >= start && q.DateIssued <= end)
            .ToListAsync();

        var events = await _context.Events
            .Where(e => e.EventDate >= start && e.EventDate <= end)
            .ToListAsync();

        var paidInvoices = invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList();
        var overdueInvoices = invoices.Where(i => i.Status == InvoiceStatus.Overdue).ToList();
        var activeInvoices = invoices.Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Draft).ToList();

        var profitSummaries = quotes
            .Where(q => q.ProfitSummary != null)
            .Select(q => q.ProfitSummary!)
            .ToList();

        var totalRevenue = paidInvoices.Sum(i => i.Total);
        var totalCosts = profitSummaries.Sum(p => p.TotalCost + p.TotalExpenses);
        var netProfit = profitSummaries.Sum(p => p.NetProfit);

        // Monthly revenue grouped by month
        var revenueByMonth = paidInvoices
            .GroupBy(i => new { i.DateIssued.Year, i.DateIssued.Month })
            .Select(g => new MonthlyRevenueData
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Revenue = g.Sum(i => i.Total)
            })
            .OrderBy(m => m.Month)
            .ToList();

        var profitByMonth = quotes
            .Where(q => q.ProfitSummary != null)
            .GroupBy(q => new { q.DateIssued.Year, q.DateIssued.Month })
            .ToDictionary(
                g => new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                g => g.Sum(q => q.ProfitSummary!.NetProfit)
            );

        foreach (var m in revenueByMonth)
            m.Profit = profitByMonth.GetValueOrDefault(m.Month, 0);

        // Overdue aging
        var now = DateTime.Today;
        var overdue30 = overdueInvoices.Where(i => (now - i.DueDate).TotalDays <= 30).Sum(i => i.BalanceDue);
        var overdue60 = overdueInvoices.Where(i => (now - i.DueDate).TotalDays is > 30 and <= 60).Sum(i => i.BalanceDue);
        var overdue90 = overdueInvoices.Where(i => (now - i.DueDate).TotalDays > 60).Sum(i => i.BalanceDue);

        // Repeat clients
        var clientGroups = invoices.GroupBy(i => i.ClientId).ToList();
        var totalClientsServed = clientGroups.Count;
        var repeatClients = clientGroups.Count(g => g.Count() > 1);

        var vm = new BusinessDashboardViewModel
        {
            StartDate = start,
            EndDate = end,

            TotalRevenue = totalRevenue,
            TotalOutstanding = activeInvoices.Sum(i => i.BalanceDue),
            TotalOverdue = overdueInvoices.Sum(i => i.BalanceDue),
            NetProfit = netProfit,
            TotalCosts = totalCosts,
            ProfitMarginPercent = totalRevenue > 0 ? Math.Round(netProfit / totalRevenue * 100, 1) : 0,
            AverageInvoiceValue = paidInvoices.Any() ? Math.Round(paidInvoices.Average(i => i.Total), 2) : 0,
            LargestInvoiceValue = invoices.Any() ? invoices.Max(i => i.Total) : 0,

            InvoicesPaid = invoices.Count(i => i.Status == InvoiceStatus.Paid),
            InvoicesSent = invoices.Count(i => i.Status == InvoiceStatus.Sent),
            InvoicesPartiallyPaid = invoices.Count(i => i.Status == InvoiceStatus.PartiallyPaid),
            InvoicesOverdue = invoices.Count(i => i.Status == InvoiceStatus.Overdue),
            InvoicesDraft = invoices.Count(i => i.Status == InvoiceStatus.Draft),
            TotalInvoices = invoices.Count,

            MonthlyRevenue = revenueByMonth,

            TotalQuotes = quotes.Count,
            QuotesConverted = quotes.Count(q => q.Status == QuoteStatus.Converted),
            QuoteConversionRate = quotes.Any() ? Math.Round((decimal)quotes.Count(q => q.Status == QuoteStatus.Converted) / quotes.Count * 100, 1) : 0,
            AverageQuoteValue = quotes.Any() ? Math.Round(quotes.Average(q => q.Total), 2) : 0,
            TotalQuotedValue = quotes.Sum(q => q.Total),
            QuotesDraft = quotes.Count(q => q.Status == QuoteStatus.Draft),
            QuotesPending = quotes.Count(q => q.Status == QuoteStatus.Pending),
            QuotesApproved = quotes.Count(q => q.Status == QuoteStatus.Approved),
            QuotesRejected = quotes.Count(q => q.Status == QuoteStatus.Rejected),

            TotalEvents = events.Count,
            CompletedEvents = events.Count(e => e.Status == EventStatus.Completed),
            TotalClientsServed = totalClientsServed,
            RepeatClients = repeatClients,
            RepeatClientRate = totalClientsServed > 0 ? Math.Round((decimal)repeatClients / totalClientsServed * 100, 1) : 0,

            Overdue30Days = overdue30,
            Overdue60Days = overdue60,
            Overdue90PlusDays = overdue90,
        };

        if (revenueByMonth.Any())
        {
            var best = revenueByMonth.OrderByDescending(m => m.Revenue).First();
            vm.BestMonth = best.Month;
            vm.BestMonthRevenue = best.Revenue;
        }

        return vm;
    }

    // ── Client Report ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ClientReport(int? clientId, DateTime? startDate, DateTime? endDate, bool exportPdf = false)
    {
        var start = startDate ?? DateTime.Today.AddMonths(-12);
        var end = endDate ?? DateTime.Today;

        var allClients = await _context.Clients
            .Where(c => c.IsActive)
            .OrderBy(c => c.FullName)
            .ToListAsync();

        var vm = new ClientReportViewModel
        {
            ClientId = clientId ?? 0,
            StartDate = start,
            EndDate = end,
            AllClients = allClients,
        };

        if (clientId.HasValue && clientId.Value > 0)
        {
            vm = await BuildClientReport(clientId.Value, start, end, allClients);

            if (exportPdf && vm.HasData)
            {
                var html = await _renderer.RenderViewToStringAsync("~/Views/Pdf/ClientReportPdf.cshtml", vm);
                var bytes = await _pdf.GeneratePdfFromHtmlAsync(html);
                var name = vm.Client?.FullName.Replace(" ", "-") ?? "Client";
                return File(bytes, "application/pdf", $"Client-Report-{name}-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
            }
        }

        ViewData["Title"] = "Client Report";
        return View(vm);
    }

    private async Task<ClientReportViewModel> BuildClientReport(int clientId, DateTime start, DateTime end, List<Client> allClients)
    {
        var client = await _context.Clients.FindAsync(clientId);

        var invoices = await _context.Invoices
            .Include(i => i.Payments)
            .Where(i => i.ClientId == clientId && i.DateIssued >= start && i.DateIssued <= end)
            .OrderByDescending(i => i.DateIssued)
            .ToListAsync();

        var allClientInvoices = await _context.Invoices
            .Where(i => i.ClientId == clientId)
            .ToListAsync();

        var quotes = await _context.Quotes
            .Where(q => q.ClientId == clientId && q.DateIssued >= start && q.DateIssued <= end)
            .OrderByDescending(q => q.DateIssued)
            .ToListAsync();

        var events = await _context.Events
            .Include(e => e.Quote)
            .Where(e => e.Quote != null && e.Quote.ClientId == clientId
                        && e.EventDate >= start && e.EventDate <= end)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync();

        var allClientEvents = await _context.Events
            .Include(e => e.Quote)
            .Where(e => e.Quote != null && e.Quote.ClientId == clientId)
            .ToListAsync();

        // Revenue per event via linked invoice
        var eventInvoiceMap = await _context.Invoices
            .Include(i => i.Quote)
            .ThenInclude(q => q!.Event)
            .Where(i => i.ClientId == clientId)
            .ToListAsync();

        decimal EventRevenue(Models.Event e)
        {
            var inv = eventInvoiceMap.FirstOrDefault(i => i.Quote?.Event?.Id == e.Id);
            return inv?.Total ?? 0;
        }

        // Discount totals
        var totalDiscounts = invoices.Sum(i => i.Discount);
        var totalGross = invoices.Sum(i => i.Subtotal);

        // Payment timing
        var paidInvoices = invoices.Where(i => i.Payments.Any()).ToList();
        double avgDays = 0;
        int onTime = 0, late = 0;
        foreach (var inv in paidInvoices)
        {
            var lastPayment = inv.Payments.OrderByDescending(p => p.PaymentDate).First();
            var days = (lastPayment.PaymentDate - inv.DateIssued).TotalDays;
            avgDays += days;
            if (lastPayment.PaymentDate <= inv.DueDate) onTime++; else late++;
        }
        if (paidInvoices.Any()) avgDays /= paidInvoices.Count;

        // Build invoice lines
        var invoiceLines = invoices.Select(i => new InvoiceReportLine
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber ?? $"INV-{i.Id}",
            DateIssued = i.DateIssued,
            DueDate = i.DueDate,
            Subtotal = i.Subtotal,
            Discount = i.Discount,
            Total = i.Total,
            AmountPaid = i.AmountPaid,
            BalanceDue = i.BalanceDue,
            Status = i.Status.ToString(),
            Payments = i.Payments.OrderBy(p => p.PaymentDate).ToList()
        }).ToList();

        // Build event lines
        var eventLines = events.Select(e => new EventReportLine
        {
            EventName = e.EventName,
            EventDate = e.EventDate,
            Venue = e.Venue,
            Status = e.Status.ToString(),
            Revenue = EventRevenue(e)
        }).ToList();

        // Build quote lines
        var quoteLines = quotes.Select(q => new QuoteReportLine
        {
            Id = q.Id,
            QuoteNumber = q.QuoteNumber ?? $"QUO-{q.Id}",
            DateIssued = q.DateIssued,
            Subtotal = q.Subtotal,
            Discount = q.Discount,
            Total = q.Total,
            Status = q.Status.ToString()
        }).ToList();

        var totalPaid = invoices.Sum(i => i.AmountPaid);
        var totalInvoiced = invoices.Sum(i => i.Total);

        return new ClientReportViewModel
        {
            ClientId = clientId,
            StartDate = start,
            EndDate = end,
            AllClients = allClients,
            HasData = true,
            Client = client,
            RelationshipSince = allClientInvoices.Any()
                ? allClientInvoices.Min(i => i.DateIssued)
                : (DateTime?)null,

            TotalEventsInPeriod = events.Count,
            TotalEventsAllTime = allClientEvents.Count,
            TotalInvoicedInPeriod = totalInvoiced,
            TotalPaidInPeriod = totalPaid,
            TotalOutstandingInPeriod = invoices.Sum(i => i.BalanceDue),
            LifetimeValue = allClientInvoices.Sum(i => i.AmountPaid),

            TotalDiscountsGiven = totalDiscounts,
            TotalGrossValue = totalGross,
            DiscountPercentage = totalGross > 0 ? Math.Round(totalDiscounts / totalGross * 100, 1) : 0,

            Invoices = invoiceLines,
            Events = eventLines,
            Quotes = quoteLines,

            AverageDaysToPayment = Math.Round((decimal)avgDays, 1),
            InvoicesPaidOnTime = onTime,
            InvoicesPaidLate = late,
            AverageSpendPerEvent = events.Any() ? Math.Round(totalInvoiced / events.Count, 2) : 0,
        };
    }

    // ── Period Snapshot ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> PeriodSnapshot(DateTime? startDate, DateTime? endDate, bool exportPdf = false)
    {
        var start = startDate ?? DateTime.Today.AddMonths(-3);
        var end = endDate ?? DateTime.Today;

        var vm = await BuildPeriodSnapshot(start, end);

        if (exportPdf)
        {
            var html = await _renderer.RenderViewToStringAsync("~/Views/Pdf/PeriodSnapshotPdf.cshtml", vm);
            var bytes = await _pdf.GeneratePdfFromHtmlAsync(html);
            return File(bytes, "application/pdf", $"Period-Snapshot-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
        }

        ViewData["Title"] = "Period Snapshot";
        return View(vm);
    }

    private async Task<PeriodSnapshotViewModel> BuildPeriodSnapshot(DateTime start, DateTime end)
    {
        var invoices = await _context.Invoices
            .Include(i => i.Client)
            .Where(i => i.DateIssued >= start && i.DateIssued <= end)
            .ToListAsync();

        var quotes = await _context.Quotes
            .Include(q => q.ProfitSummary)
            .Where(q => q.DateIssued >= start && q.DateIssued <= end)
            .ToListAsync();

        var events = await _context.Events
            .Where(e => e.EventDate >= start && e.EventDate <= end)
            .ToListAsync();

        var profitSummaries = quotes
            .Where(q => q.ProfitSummary != null)
            .Select(q => q.ProfitSummary!)
            .ToList();

        var totalRevenue = invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Total);
        var totalCosts = profitSummaries.Sum(p => p.TotalCost + p.TotalExpenses);
        var netProfit = profitSummaries.Sum(p => p.NetProfit);

        // Top clients by revenue (paid invoices)
        var topClients = invoices
            .Where(i => i.Status == InvoiceStatus.Paid)
            .GroupBy(i => i.ClientId)
            .Select(g => new
            {
                ClientId = g.Key,
                Revenue = g.Sum(i => i.Total),
                InvoiceCount = g.Count(),
                Client = g.First().Client
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        // Event counts per client
        var clientEventCounts = await _context.Events
            .Include(e => e.Quote)
            .Where(e => e.EventDate >= start && e.EventDate <= end && e.Quote != null)
            .GroupBy(e => e.Quote!.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToListAsync();

        var topClientData = topClients.Select(tc => new TopClientData
        {
            Name = tc.Client?.FullName ?? "Unknown",
            BusinessName = tc.Client?.BusinessName,
            Revenue = tc.Revenue,
            InvoiceCount = tc.InvoiceCount,
            EventCount = clientEventCounts.FirstOrDefault(ec => ec.ClientId == tc.ClientId)?.Count ?? 0
        }).ToList();

        // Monthly breakdown
        var months = new List<MonthlyBreakdownData>();
        var current = new DateTime(start.Year, start.Month, 1);
        while (current <= end)
        {
            var mInvoices = invoices.Where(i => i.DateIssued.Year == current.Year && i.DateIssued.Month == current.Month).ToList();
            var mQuotes = quotes.Where(q => q.DateIssued.Year == current.Year && q.DateIssued.Month == current.Month).ToList();
            var mEvents = events.Where(e => e.EventDate.Year == current.Year && e.EventDate.Month == current.Month).ToList();
            var mProfit = mQuotes.Where(q => q.ProfitSummary != null).Sum(q => q.ProfitSummary!.NetProfit);
            var mCosts = mQuotes.Where(q => q.ProfitSummary != null).Sum(q => q.ProfitSummary!.TotalCost + q.ProfitSummary!.TotalExpenses);

            months.Add(new MonthlyBreakdownData
            {
                Month = current.ToString("MMM"),
                Year = current.Year,
                Revenue = mInvoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Total),
                Costs = mCosts,
                Profit = mProfit,
                EventCount = mEvents.Count,
                InvoiceCount = mInvoices.Count
            });

            current = current.AddMonths(1);
        }

        var overdueInvoices = invoices.Where(i => i.Status == InvoiceStatus.Overdue).ToList();
        var unpaidInvoices = invoices.Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Draft).ToList();

        return new PeriodSnapshotViewModel
        {
            StartDate = start,
            EndDate = end,

            TotalRevenue = totalRevenue,
            TotalCosts = totalCosts,
            NetProfit = netProfit,
            ProfitMarginPercent = totalRevenue > 0 ? Math.Round(netProfit / totalRevenue * 100, 1) : 0,

            TotalInvoiced = invoices.Sum(i => i.Total),
            TotalCollected = invoices.Sum(i => i.AmountPaid),
            OutstandingReceivables = unpaidInvoices.Sum(i => i.BalanceDue),
            OverdueCount = overdueInvoices.Count,
            OverdueAmount = overdueInvoices.Sum(i => i.BalanceDue),

            TotalEvents = events.Count,
            CompletedEvents = events.Count(e => e.Status == EventStatus.Completed),
            CancelledEvents = events.Count(e => e.Status == EventStatus.Cancelled),

            TotalQuotes = quotes.Count,
            ConvertedQuotes = quotes.Count(q => q.Status == QuoteStatus.Converted),
            TotalQuotedValue = quotes.Sum(q => q.Total),
            ConversionRate = quotes.Any() ? Math.Round((decimal)quotes.Count(q => q.Status == QuoteStatus.Converted) / quotes.Count * 100, 1) : 0,

            TopClients = topClientData,
            MonthlyBreakdown = months
        };
    }
}
