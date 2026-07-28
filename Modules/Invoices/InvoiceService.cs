using Microsoft.EntityFrameworkCore;
using InvoiceBuilder.Data;
using InvoiceBuilder.Models;

namespace InvoiceBuilder.Modules.Invoices;

public class InvoiceService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<Invoice>> GetAllAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Sender)
            .Include(i => i.LineItems)
            .AsNoTracking()
            .OrderByDescending(i => i.Id)
            .ToListAsync();
    }

    public async Task<Invoice?> GetAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        return await db.Invoices
            .Include(i => i.Sender)
            .Include(i => i.Customer)
            .Include(i => i.LineItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    // Dropdown lookups for the invoice editor; owned by this slice so it
    // doesn't depend on the Customers/Senders modules.
    public async Task<List<Customer>> GetCustomersAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<List<Sender>> GetSendersAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.Senders.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
    }

    // Next sequential number in the "INV-{year}-{seq}" format used by seed data and existing
    // invoices — scoped per current year so numbering restarts each year rather than growing forever.
    public async Task<string> GetNextInvoiceNumberAsync()
    {
        await using var db = factory.CreateDbContext();
        var year = DateTime.Today.Year;
        var prefix = $"INV-{year}-";

        var maxSeq = await db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .AsNoTracking()
            .ToListAsync();

        var nextSeq = maxSeq
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var seq) ? seq : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{nextSeq:D3}";
    }

    public async Task<int> CreateAsync(Invoice invoice)
    {
        await using var db = factory.CreateDbContext();
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    // Reconciling a disconnected line-item collection: load the tracked graph and diff it,
    // because a naive db.Update(invoice) would not delete rows the user removed.
    public async Task UpdateAsync(Invoice invoice)
    {
        await using var db = factory.CreateDbContext();
        var existing = await db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        if (existing is null) return;

        db.Entry(existing).CurrentValues.SetValues(invoice);

        foreach (var row in existing.LineItems.ToList())
        {
            if (invoice.LineItems.All(x => x.Id != row.Id))
            {
                db.InvoiceLineItems.Remove(row);
            }
        }

        foreach (var incoming in invoice.LineItems)
        {
            var match = incoming.Id != 0
                ? existing.LineItems.FirstOrDefault(x => x.Id == incoming.Id)
                : null;

            if (match is null)
            {
                existing.LineItems.Add(new InvoiceLineItem
                {
                    Description = incoming.Description,
                    Quantity = incoming.Quantity,
                    UnitPrice = incoming.UnitPrice,
                });
            }
            else
            {
                db.Entry(match).CurrentValues.SetValues(incoming);
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        var invoice = await db.Invoices.FindAsync(id);
        if (invoice is not null)
        {
            db.Invoices.Remove(invoice);
            await db.SaveChangesAsync();
        }
    }
}
