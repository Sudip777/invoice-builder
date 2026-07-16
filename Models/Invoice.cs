using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvoiceBuilder.Models;

public class Invoice
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string InvoiceNumber { get; set; } = "";

    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today).AddDays(14);

    [Required, StringLength(10)]
    public string Currency { get; set; } = "USD";

    [Range(0, 100)]
    public decimal TaxRate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; } = "Thank you for your business!";

    [Range(1, int.MaxValue, ErrorMessage = "Pick a sender")]
    public int SenderId { get; set; }
    public Sender Sender { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Pick a customer")]
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public List<InvoiceLineItem> LineItems { get; set; } = new();

    [NotMapped]
    public decimal Subtotal => LineItems.Sum(li => li.LineTotal);

    [NotMapped]
    public decimal TaxAmount => Math.Round(Subtotal * (TaxRate / 100m), 2);

    [NotMapped]
    public decimal Total => Subtotal + TaxAmount;
}
