using KiefProductions.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<StaffMember> StaffMembers { get; set; }
    public DbSet<EventStaff> EventStaff { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<ProductType> ProductTypes { get; set; }
    public DbSet<Gear> Gears { get; set; }
    public DbSet<RentalGear> RentalGears { get; set; }
    public DbSet<GearBooking> GearBookings { get; set; }
    public DbSet<Package> Packages { get; set; }
    public DbSet<PackageItem> PackageItems { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<QuoteLineItem> QuoteLineItems { get; set; }
    public DbSet<FreeTextQuoteSection> FreeTextQuoteSections { get; set; }
    public DbSet<ProfitSummary> ProfitSummaries { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<VendorDocument> VendorDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Client
        builder.Entity<Client>(e =>
        {
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.BusinessName).HasMaxLength(200);
        });

        // Event
        builder.Entity<Event>(e =>
        {
            e.Property(x => x.EventName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Venue).HasMaxLength(300);
            e.HasOne(x => x.Quote)
             .WithOne(x => x.Event)
             .HasForeignKey<Event>(x => x.QuoteId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // EventStaff
        builder.Entity<EventStaff>(e =>
        {
            e.HasOne(x => x.Event)
             .WithMany(x => x.EventStaff)
             .HasForeignKey(x => x.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.StaffMember)
             .WithMany(x => x.EventStaff)
             .HasForeignKey(x => x.StaffMemberId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.FlatRateApplied).HasColumnType("decimal(18,2)");
        });

        // StaffMember
        builder.Entity<StaffMember>(e =>
        {
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            e.Property(x => x.FlatRate).HasColumnType("decimal(18,2)");
        });

        // Category
        builder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        // ProductType
        builder.Entity<ProductType>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasOne(x => x.Category)
             .WithMany(x => x.ProductTypes)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Gear
        builder.Entity<Gear>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.MarketPrice).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.ProductType)
             .WithMany(x => x.Gears)
             .HasForeignKey(x => x.ProductTypeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // RentalGear
        builder.Entity<RentalGear>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.MarketPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.OurCost).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.ProductType)
             .WithMany(x => x.RentalGears)
             .HasForeignKey(x => x.ProductTypeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // GearBooking
        builder.Entity<GearBooking>(e =>
        {
            e.HasOne(x => x.Gear)
             .WithMany(x => x.GearBookings)
             .HasForeignKey(x => x.GearId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.RentalGear)
             .WithMany(x => x.GearBookings)
             .HasForeignKey(x => x.RentalGearId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Event)
             .WithMany(x => x.GearBookings)
             .HasForeignKey(x => x.EventId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Package
        builder.Entity<Package>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.BasePrice).HasColumnType("decimal(18,2)");
        });

        // PackageItem
        builder.Entity<PackageItem>(e =>
        {
            e.HasOne(x => x.Package)
             .WithMany(x => x.PackageItems)
             .HasForeignKey(x => x.PackageId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Gear)
             .WithMany(x => x.PackageItems)
             .HasForeignKey(x => x.GearId)
             .OnDelete(DeleteBehavior.NoAction);
        });

        // FreeTextQuoteSection
        builder.Entity<FreeTextQuoteSection>(e =>
        {
            e.Property(x => x.CategoryName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Qty).HasColumnType("decimal(18,2)");
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.Cost).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Quote)
             .WithMany(x => x.FreeTextSections)
             .HasForeignKey(x => x.QuoteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Quote
        builder.Entity<Quote>(e =>
        {
            e.Property(x => x.QuoteNumber).HasMaxLength(20);
            e.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.Discount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Client)
             .WithMany(x => x.Quotes)
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // QuoteLineItem
        builder.Entity<QuoteLineItem>(e =>
        {
            e.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Quote)
             .WithMany(x => x.LineItems)
             .HasForeignKey(x => x.QuoteId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Category)
             .WithMany()
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.ProductType)
             .WithMany()
             .HasForeignKey(x => x.ProductTypeId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Package)
             .WithMany()
             .HasForeignKey(x => x.PackageId)
             .OnDelete(DeleteBehavior.NoAction);
        });

        // ProfitSummary
        builder.Entity<ProfitSummary>(e =>
        {
            e.Property(x => x.TotalRevenue).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalCost).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalExpenses).HasColumnType("decimal(18,2)");
            e.Property(x => x.NetProfit).HasColumnType("decimal(18,2)");
            e.Property(x => x.ProfitMargin).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Quote)
             .WithOne(x => x.ProfitSummary)
             .HasForeignKey<ProfitSummary>(x => x.QuoteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice
        builder.Entity<Invoice>(e =>
        {
            e.Property(x => x.InvoiceNumber).HasMaxLength(20);
            e.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.Discount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");
            e.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            e.Ignore(x => x.BalanceDue);

            e.HasOne(x => x.Client)
             .WithMany(x => x.Invoices)
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Quote)
             .WithOne(x => x.Invoice)
             .HasForeignKey<Invoice>(x => x.QuoteId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // InvoiceLineItem
        builder.Entity<InvoiceLineItem>(e =>
        {
            e.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Invoice)
             .WithMany(x => x.LineItems)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Category)
             .WithMany()
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.ProductType)
             .WithMany()
             .HasForeignKey(x => x.ProductTypeId)
             .OnDelete(DeleteBehavior.NoAction);
        });

        // Payment
        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Method).HasMaxLength(100);

            e.HasOne(x => x.Invoice)
             .WithMany(x => x.Payments)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // VendorDocument
        builder.Entity<VendorDocument>(e =>
        {
            e.Property(x => x.VendorName).IsRequired().HasMaxLength(200);
            e.Property(x => x.DocumentNumber).HasMaxLength(50);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(300);
            e.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(300);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Event)
             .WithMany(x => x.VendorDocuments)
             .HasForeignKey(x => x.EventId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}