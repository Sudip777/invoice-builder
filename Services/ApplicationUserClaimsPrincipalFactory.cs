using System.Security.Claims;
using InvoiceBuilder.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Services;

/// <summary>
/// Adds a stable "display_name" claim so the UI can show a friendly name
/// without re-querying the store on every render.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("display_name", user.DisplayName ?? user.Email ?? user.UserName ?? "User"));
        return identity;
    }
}
