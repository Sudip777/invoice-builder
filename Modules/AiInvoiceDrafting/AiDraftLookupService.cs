using Microsoft.EntityFrameworkCore;
using InvoiceBuilder.Data;
using InvoiceBuilder.Models;

namespace InvoiceBuilder.Modules.AiInvoiceDrafting;

public class AiDraftLookupService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<string>> GetCustomerNamesAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.Customers.AsNoTracking().Select(c => c.Name).ToListAsync();
    }

    // Exact (case-insensitive) match only — the AI is asked to resolve loose variants
    // ("ACME Corp" vs "Acme Corporation") to one of GetCustomerNamesAsync()'s exact
    // strings itself, so this lookup doesn't need its own fuzzy logic.
    public async Task<Customer?> FindCustomerByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        await using var db = factory.CreateDbContext();
        return await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
    }
}
