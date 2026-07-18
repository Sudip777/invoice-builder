using InvoiceBuilder.Models;

namespace InvoiceBuilder.Modules.Dashboard;

public class DashboardData
{
    public int InvoiceCount { get; set; }
    public int CustomerCount { get; set; }
    public int SenderCount { get; set; }
    public decimal TotalOutstanding { get; set; }

    // Date-derived buckets — the domain has no paid/status field (see the
    // Overdue/Due soon badges on the Invoices grid, which use the same rule).
    public int OverdueCount { get; set; }
    public int DueSoonCount { get; set; }
    public int UpcomingCount { get; set; }

    // Last 6 months, oldest first; label is e.g. "Feb 2026".
    public List<(string Label, decimal Total)> MonthlyRevenue { get; set; } = new();

    public List<Invoice> RecentInvoices { get; set; } = new();
}
