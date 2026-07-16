using Microsoft.AspNetCore.Identity;

namespace InvoiceBuilder.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
