using System.ComponentModel.DataAnnotations;

namespace InvoiceBuilder.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(200)]
    public string? ContactPerson { get; set; }

    [Required, StringLength(400)]
    public string Address { get; set; } = "";

    [StringLength(200), EmailAddress]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? VatId { get; set; }

    public List<Invoice> Invoices { get; set; } = new();
}
