using InvoiceBuilder.Components;
using InvoiceBuilder.Data;
using InvoiceBuilder.Models;
using InvoiceBuilder.Modules.Customers;
using InvoiceBuilder.Modules.Dashboard;
using InvoiceBuilder.Modules.Invoices;
using InvoiceBuilder.Modules.Senders;
using InvoiceBuilder.Modules.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using QuestPDF.Fluent;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add database context (SQLite for local/dev — swap UseSqlite -> UseSqlServer to move providers)
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<SenderService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<DashboardService>();

// External OAuth only (no local passwords); accounts auto-provision on first sign-in for any Google account with an email.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException("Missing configuration: Authentication:Google:ClientId. Set it via user-secrets or environment variables.");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
            ?? throw new InvalidOperationException("Missing configuration: Authentication:Google:ClientSecret. Set it via user-secrets or environment variables.");
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();

builder.Services.AddAuthorization(options =>
{
    // Everything requires a signed-in user unless it opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    // Bottom-right so toasts don't cover the list pages' top-right "+ New" buttons.
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

var app = builder.Build();

// Apply pending migrations and seed sample data on startup.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.Migrate();

    if (!db.Senders.Any())
    {
        var sender = new Sender
        {
            Name = "My Company LLC",
            ContactPerson = "Alice Smith",
            Address = "456 Market St, City",
            VatId = "TAX-987654",
            Iban = "XX00 0000 0000 0000 00",
        };
        var customer = new Customer
        {
            Name = "ACME Corp",
            ContactPerson = "John Doe",
            Address = "123 Main St, City, 12345",
            Email = "john.doe@example.com",
            VatId = "VAT-123456",
        };
        db.Senders.Add(sender);
        db.Customers.Add(customer);
        db.SaveChanges();

        db.Invoices.Add(new Invoice
        {
            InvoiceNumber = "INV-2026-001",
            SenderId = sender.Id,
            CustomerId = customer.Id,
            TaxRate = 8.5m,
            LineItems = new List<InvoiceLineItem>
            {
                new() { Description = ".NET Book", Quantity = 1, UnitPrice = 29.99m },
                new() { Description = ".NET Course", Quantity = 1, UnitPrice = 89.99m },
            },
        });
        db.SaveChanges();
    }

    // Dev-only test account for exercising the login-gated UI without real OAuth credentials.
    if (app.Environment.IsDevelopment())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(DevAuthEndpoints.TestUserEmail) is null)
        {
            await userManager.CreateAsync(new ApplicationUser
            {
                UserName = DevAuthEndpoints.TestUserEmail,
                Email = DevAuthEndpoints.TestUserEmail,
                EmailConfirmed = true,
                DisplayName = "Test User",
            });
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Anonymous endpoints; per-page auth is enforced by AuthorizeRouteView (Components/Routes.razor).
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();
app.MapAccountEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapDevAuthEndpoints();
}

app.MapGet("/invoices/{id:int}/pdf", async (int id, InvoiceService invoices) =>
{
    var invoice = await invoices.GetAsync(id);
    if (invoice is null) return Results.NotFound();

    var bytes = new InvoiceDocument(invoice).GeneratePdf();
    return Results.File(bytes, "application/pdf", $"invoice-{invoice.InvoiceNumber}.pdf");
});

app.Run();
