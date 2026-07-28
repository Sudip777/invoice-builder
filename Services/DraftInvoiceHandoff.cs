namespace InvoiceBuilder.Services;

public class DraftInvoiceResult
{
    public string CustomerName { get; set; } = "";
    public DateOnly? InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal TaxRate { get; set; }
    public string? Notes { get; set; }
    public List<DraftLineItem> LineItems { get; set; } = new();
}

public class DraftLineItem
{
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

// Scoped, in-memory handoff from the AiInvoiceDrafting slice to the Invoices slice (same
// circuit), so neither slice needs to reference the other's services directly — the invoice
// editor just reads-and-clears a pending draft if one is waiting.
public class DraftInvoiceHandoff
{
    public DraftInvoiceResult? Pending { get; private set; }

    public void Set(DraftInvoiceResult draft) => Pending = draft;

    public DraftInvoiceResult? TakePending()
    {
        var draft = Pending;
        Pending = null;
        return draft;
    }
}
