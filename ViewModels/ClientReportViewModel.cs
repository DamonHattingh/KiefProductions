using KiefProductions.Models;

namespace KiefProductions.ViewModels;

public class ClientReportViewModel
{
    public int ClientId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<Client> AllClients { get; set; } = new();
    public bool HasData { get; set; }

    // Client info
    public Client? Client { get; set; }
    public DateTime? RelationshipSince { get; set; }

    // Period summary
    public int TotalEventsInPeriod { get; set; }
    public int TotalEventsAllTime { get; set; }
    public decimal TotalInvoicedInPeriod { get; set; }
    public decimal TotalPaidInPeriod { get; set; }
    public decimal TotalOutstandingInPeriod { get; set; }
    public decimal LifetimeValue { get; set; }

    // Discounts
    public decimal TotalDiscountsGiven { get; set; }
    public decimal TotalGrossValue { get; set; }
    public decimal DiscountPercentage { get; set; }

    // Detail lines
    public List<InvoiceReportLine> Invoices { get; set; } = new();
    public List<EventReportLine> Events { get; set; } = new();
    public List<QuoteReportLine> Quotes { get; set; } = new();

    // Payment behaviour
    public decimal AverageDaysToPayment { get; set; }
    public int InvoicesPaidOnTime { get; set; }
    public int InvoicesPaidLate { get; set; }
    public decimal AverageSpendPerEvent { get; set; }
}

public class InvoiceReportLine
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime DateIssued { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Status { get; set; } = "";
    public List<Payment> Payments { get; set; } = new();
}

public class EventReportLine
{
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string? Venue { get; set; }
    public string Status { get; set; } = "";
    public decimal Revenue { get; set; }
}

public class QuoteReportLine
{
    public int Id { get; set; }
    public string QuoteNumber { get; set; } = "";
    public DateTime DateIssued { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "";
}
