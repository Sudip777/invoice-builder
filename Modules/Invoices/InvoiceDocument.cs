using InvoiceBuilder.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InvoiceBuilder.Modules.Invoices;

public class InvoiceDocument(Invoice invoice) : IDocument
{
    private static readonly string HeaderDark = Colors.Grey.Darken4;
    private static readonly string PanelBg = Colors.Blue.Lighten5;
    private static readonly string NotesBg = "#FEF9E7";
    private static readonly string AccentBlue = Colors.Blue.Medium;

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));
        page.Header().Element(ComposeHeader);
        page.Content().Element(ComposeContent);
        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Page ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    });

    private void ComposeHeader(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().Text("INVOICE").FontSize(22).Bold().FontColor(Colors.Grey.Darken4);
            col.Item().Text(invoice.InvoiceNumber).FontSize(11).FontColor(Colors.Grey.Medium);
        });

        row.ConstantItem(180).Column(col =>
        {
            col.Item().AlignRight().Text("Invoice Date").FontSize(8).FontColor(Colors.Grey.Medium);
            col.Item().AlignRight().Text($"{invoice.InvoiceDate:MMM dd, yyyy}").Bold();
            col.Item().PaddingTop(6).AlignRight().Text("Due Date").FontSize(8).FontColor(Colors.Grey.Medium);
            col.Item().AlignRight().Text($"{invoice.DueDate:MMM dd, yyyy}").Bold().FontColor(Colors.Red.Medium);
        });
    });

    private void ComposeContent(IContainer container) => container.PaddingVertical(25).Column(col =>
    {
        col.Spacing(18);

        col.Item().Row(row =>
        {
            row.RelativeItem().Background(PanelBg).Padding(12).Column(x =>
            {
                x.Item().Text("FROM").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                x.Item().PaddingTop(4).Text(invoice.Sender.Name).Bold();
                if (!string.IsNullOrWhiteSpace(invoice.Sender.ContactPerson))
                    x.Item().Text(invoice.Sender.ContactPerson!);
                x.Item().Text(invoice.Sender.Address);
                if (!string.IsNullOrWhiteSpace(invoice.Sender.VatId))
                    x.Item().PaddingTop(4).Text($"VAT/Tax ID: {invoice.Sender.VatId}").FontSize(9);
                if (!string.IsNullOrWhiteSpace(invoice.Sender.Iban))
                    x.Item().Text($"IBAN: {invoice.Sender.Iban}").FontSize(9);
            });

            row.ConstantItem(16);

            row.RelativeItem().Background(PanelBg).Padding(12).Column(x =>
            {
                x.Item().Text("BILL TO").FontSize(8).Bold().FontColor(AccentBlue);
                x.Item().PaddingTop(4).Text(invoice.Customer.Name).Bold();
                if (!string.IsNullOrWhiteSpace(invoice.Customer.ContactPerson))
                    x.Item().Text(invoice.Customer.ContactPerson!);
                x.Item().Text(invoice.Customer.Address);
                if (!string.IsNullOrWhiteSpace(invoice.Customer.Email))
                    x.Item().PaddingTop(4).Text($"Email: {invoice.Customer.Email}").FontSize(9);
                if (!string.IsNullOrWhiteSpace(invoice.Customer.VatId))
                    x.Item().Text($"VAT/Tax ID: {invoice.Customer.VatId}").FontSize(9);
            });
        });

        col.Item().Element(ComposeTable);

        col.Item().AlignRight().Width(220).Column(t =>
        {
            t.Spacing(4);
            t.Item().Row(r =>
            {
                r.RelativeItem().Text("Subtotal");
                r.ConstantItem(100).AlignRight().Text($"{invoice.Subtotal:N2} {invoice.Currency}");
            });
            t.Item().Row(r =>
            {
                r.RelativeItem().Text($"Tax ({invoice.TaxRate:N2}%)");
                r.ConstantItem(100).AlignRight().Text($"{invoice.TaxAmount:N2} {invoice.Currency}");
            });
            t.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(r =>
            {
                r.RelativeItem().Text("Total Amount").Bold();
                r.ConstantItem(100).AlignRight().Text($"{invoice.Total:N2} {invoice.Currency}").Bold().FontColor(AccentBlue).FontSize(12);
            });
        });

        if (!string.IsNullOrWhiteSpace(invoice.Notes))
        {
            col.Item().Background(NotesBg).BorderLeft(3).BorderColor(Colors.Yellow.Medium).Padding(10).Column(x =>
            {
                x.Item().Text("NOTES").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                x.Item().PaddingTop(2).Text(invoice.Notes!);
            });
        }
    });

    private void ComposeTable(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(cols =>
        {
            cols.RelativeColumn(4);
            cols.RelativeColumn(1);
            cols.RelativeColumn(2);
            cols.RelativeColumn(2);
        });

        table.Header(header =>
        {
            header.Cell().Background(HeaderDark).Padding(8).Text("Item Description").FontColor(Colors.White).Bold();
            header.Cell().Background(HeaderDark).Padding(8).AlignRight().Text("Quantity").FontColor(Colors.White).Bold();
            header.Cell().Background(HeaderDark).Padding(8).AlignRight().Text("Unit Price").FontColor(Colors.White).Bold();
            header.Cell().Background(HeaderDark).Padding(8).AlignRight().Text("Total").FontColor(Colors.White).Bold();
        });

        foreach (var item in invoice.LineItems)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(item.Description);
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text($"{item.Quantity:N2}");
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text($"{item.UnitPrice:N2} {invoice.Currency}");
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text($"{item.LineTotal:N2} {invoice.Currency}").Bold();
        }
    });
}
