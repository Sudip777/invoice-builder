using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using InvoiceBuilder.Models;

namespace InvoiceBuilder.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sender> Senders => Set<Sender>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Invoice>()
            .HasMany(i => i.LineItems)
            .WithOne(li => li.Invoice)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Sender)
            .WithMany(s => s.Invoices)
            .HasForeignKey(i => i.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Customer)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        modelBuilder.Entity<Invoice>()
            .Property(i => i.TaxRate)
            .HasPrecision(5, 2);

        modelBuilder.Entity<InvoiceLineItem>()
            .Property(x => x.Quantity)
            .HasPrecision(18, 3);

        modelBuilder.Entity<InvoiceLineItem>()
            .Property(x => x.UnitPrice)
            .HasPrecision(18, 2);
    }
}
