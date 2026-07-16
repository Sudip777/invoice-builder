using InvoiceBuilder.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBuilder.Modules.Users;

/// <summary>
/// Development-only shortcut that signs in the seeded test user without a
/// real Microsoft OAuth round-trip, so the auth-gated UI can be exercised
/// locally before a real Entra app registration exists. The caller must only
/// map this when <c>IWebHostEnvironment.IsDevelopment()</c> is true.
/// </summary>
public static class DevAuthEndpoints
{
    public const string TestUserEmail = "test@test.com";

    public static IEndpointRouteBuilder MapDevAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/Account/DevSignIn", async (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByEmailAsync(TestUserEmail);
            if (user is null)
            {
                return Results.NotFound("Seeded test user not found.");
            }

            await signInManager.SignInAsync(user, isPersistent: true);
            return Results.Redirect("/");
        }).AllowAnonymous();

        return endpoints;
    }
}
