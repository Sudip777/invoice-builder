using System.Security.Claims;
using InvoiceBuilder.Models;
using InvoiceBuilder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBuilder.Components.Account;

/// <summary>
/// External-login-only auth endpoints. Kept as plain minimal APIs (rather
/// than Razor components) because completing sign-in/sign-out requires
/// writing the auth cookie, which must happen outside an interactive
/// circuit's response.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // These endpoints must stay reachable by a signed-out visitor — the
        // whole point is to establish the auth cookie they don't have yet.
        var accountGroup = endpoints.MapGroup("/Account").AllowAnonymous();

        accountGroup.MapPost("/PerformExternalLogin", (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider,
            [FromForm] string? returnUrl) =>
        {
            var safeReturnUrl = IsLocalUrl(returnUrl) ? returnUrl : "/";
            var redirectUrl = $"/Account/ExternalLoginCallback?returnUrl={Uri.EscapeDataString(safeReturnUrl!)}";
            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return TypedResults.Challenge(properties, [provider]);
        });

        accountGroup.MapGet("/ExternalLoginCallback", async (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] AllowedUserPolicy allowedUsers,
            [FromServices] ILoggerFactory loggerFactory,
            string? returnUrl,
            string? remoteError) =>
        {
            var logger = loggerFactory.CreateLogger("ExternalLogin");
            returnUrl = IsLocalUrl(returnUrl) ? returnUrl! : "/";

            if (remoteError is not null)
            {
                logger.LogWarning("External login provider returned an error: {Error}", remoteError);
                return Results.Redirect("/access-denied");
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                return Results.Redirect("/login");
            }

            // Already-linked account: sign straight in.
            var signInResult = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                return Results.Redirect(returnUrl);
            }

            if (signInResult.IsLockedOut)
            {
                return Results.Redirect("/access-denied?reason=locked-out");
            }

            // First-time sign-in: only auto-provision an account for allow-listed emails.
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (!allowedUsers.IsAllowed(email))
            {
                logger.LogWarning("Rejected external login for {Email}: not on the allow-list.", email);
                return Results.Redirect("/access-denied");
            }

            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email!;
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                logger.LogError("Failed to provision account for {Email}: {Errors}", email,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return Results.Redirect("/access-denied");
            }

            var addLoginResult = await userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                return Results.Redirect("/access-denied");
            }

            await signInManager.SignInAsync(user, isPersistent: true);
            logger.LogInformation("Provisioned new account for {Email} via {Provider}.", email, info.LoginProvider);
            return Results.Redirect(returnUrl);
        });

        accountGroup.MapPost("/Logout", async (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl! : "/login");
        });

        return endpoints;
    }

    /// <summary>
    /// Guards against open-redirect attacks via a crafted ReturnUrl — mirrors
    /// the well-known ASP.NET Core IsLocalUrl check (single leading slash, or
    /// "~/", never "//" or a URL with a scheme/host).
    /// </summary>
    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        ((url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))) ||
         (url.Length > 1 && url[0] == '~' && url[1] == '/'));
}
