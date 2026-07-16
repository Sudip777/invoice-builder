using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvoiceBuilder.Models;

public class InvoiceLineItem
{
    public int Id { get; set; }

    [Required, StringLength(300)]
    public string Description { get; set; } = "";

    [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; } = 1;

    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
    public decimal UnitPrice { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
}
