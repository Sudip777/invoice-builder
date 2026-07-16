using InvoiceBuilder.Components;
using InvoiceBuilder.Data;
using InvoiceBuilder.Models;
using InvoiceBuilder.Modules.Customers;
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

// Module services (one per vertical slice — see Modules/)
builder.Services.AddScoped<SenderService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<InvoiceService>();

// Authentication: Microsoft-account OAuth only, no local passwords. New
// accounts are auto-provisioned on first sign-in but only for emails that
// pass AllowedUserPolicy (see appsettings "Authorization" section).
builder.Services.AddSingleton<AllowedUserPolicy>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthentication()
    .AddMicrosoftAccount(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"]
            ?? throw new InvalidOperationException("Missing configuration: Authentication:Microsoft:ClientId. Set it via user-secrets or environment variables.");
        options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"]
            ?? throw new InvalidOperationException("Missing configuration: Authentication:Microsoft:ClientSecret. Set it via user-secrets or environment variables.");
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
    // Secure by default: every page/endpoint requires a signed-in user
    // unless it explicitly opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    // Bottom-right so toasts never sit on top of a page's own "+ New ..."
    // button, which every list page places top-right.
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

    // Dev-only: seed a test account so the login-gated UI can be exercised
    // before a real Microsoft Entra app registration is configured.
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Anonymous: static assets, the Blazor render endpoint (per-page auth is
// enforced inside it by AuthorizeRouteView — see Components/Routes.razor),
// and the sign-in/sign-out endpoints that establish the auth cookie itself.
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
