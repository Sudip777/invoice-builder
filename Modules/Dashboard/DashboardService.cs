using Microsoft.EntityFrameworkCore;
using InvoiceBuilder.Data;

namespace InvoiceBuilder.Modules.Dashboard;

public class DashboardService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<DashboardData> GetAsync()
    {
        await using var db = factory.CreateDbContext();

        // Invoice.Total/Subtotal are [NotMapped] computed properties, so the
        // aggregation below runs in memory over the materialized set rather
        // than as a SQL projection.
        var invoices = await db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.LineItems)
            .AsNoTracking()
            .OrderByDescending(i => i.Id)
            .ToListAsync();

        var customerCount = await db.Customers.AsNoTracking().CountAsync();
        var senderCount = await db.Senders.AsNoTracking().CountAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var dueSoonCutoff = today.AddDays(7);

        var data = new DashboardData
        {
            InvoiceCount = invoices.Count,
            CustomerCount = customerCount,
            SenderCount = senderCount,
            TotalOutstanding = invoices.Sum(i => i.Total),
            OverdueCount = invoices.Count(i => i.DueDate < today),
            DueSoonCount = invoices.Count(i => i.DueDate >= today && i.DueDate <= dueSoonCutoff),
            RecentInvoices = invoices.Take(5).ToList(),
        };
        data.UpcomingCount = data.InvoiceCount - data.OverdueCount - data.DueSoonCount;

        var monthStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        data.MonthlyRevenue = Enumerable.Range(0, 6)
            .Select(offset =>
            {
                var month = monthStart.AddMonths(offset);
                var total = invoices
                    .Where(i => i.InvoiceDate.Year == month.Year && i.InvoiceDate.Month == month.Month)
                    .Sum(i => i.Total);
                return (Label: month.ToString("MMM yyyy"), Total: total);
            })
            .ToList();

        return data;
    }
}
