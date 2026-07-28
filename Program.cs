using InvoiceBuilder.Components;
using InvoiceBuilder.Data;
using InvoiceBuilder.Models;
using InvoiceBuilder.Modules.AiInvoiceDrafting;
using InvoiceBuilder.Modules.Customers;
using InvoiceBuilder.Modules.Dashboard;
using InvoiceBuilder.Modules.Invoices;
using InvoiceBuilder.Modules.Senders;
using InvoiceBuilder.Modules.Users;
using InvoiceBuilder.Services;
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

// Modules/AiInvoiceDrafting — calls the Gemini API (free tier) to turn a plain-English
// request into an editable invoice draft; DraftInvoiceHandoff carries the result into the
// existing invoice editor without the two slices referencing each other's services.
builder.Services.AddHttpClient<GeminiInvoiceDraftService>();
builder.Services.AddScoped<AiDraftLookupService>();
builder.Services.AddScoped<DraftInvoiceHandoff>();

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
            Name = "Northwind Digital Studio",
            ContactPerson = "Alice Reinholt",
            Address = "88 Birch Avenue, Suite 200, Portland, OR 97204",
            VatId = "US-91-2837465",
            Iban = "US64 SVBK 1000 0001 2345 6789",
        };
        db.Senders.Add(sender);

        var customers = new List<Customer>
        {
            new()
            {
                Name = "Acme Corporation",
                ContactPerson = "John Doe",
                Address = "123 Main St, Springfield, IL 62701",
                Email = "john.doe@acmecorp.com",
                VatId = "US-12-3456789",
            },
            new()
            {
                Name = "Bluewave Logistics",
                ContactPerson = "Maria Chen",
                Address = "47 Harbor Rd, Seattle, WA 98101",
                Email = "maria.chen@bluewavelogistics.com",
                VatId = "US-45-6789012",
            },
            new()
            {
                Name = "Terra Nova Landscaping",
                ContactPerson = "Diego Alvarez",
                Address = "910 Meadow Ln, Austin, TX 78701",
                Email = "diego@terranovalandscaping.com",
                VatId = "US-78-9012345",
            },
            new()
            {
                Name = "Silverline Consulting Group",
                ContactPerson = "Priya Natarajan",
                Address = "22 Fifth Ave, Floor 14, New York, NY 10010",
                Email = "priya.natarajan@silverlineconsulting.com",
                VatId = "US-33-4455667",
            },
        };
        db.Customers.AddRange(customers);
        db.SaveChanges();

        var today = DateOnly.FromDateTime(DateTime.Today);

        var invoices = new List<Invoice>
        {
            new()
            {
                InvoiceNumber = "INV-2026-014",
                SenderId = sender.Id,
                CustomerId = customers[0].Id,
                InvoiceDate = today.AddDays(-3),
                DueDate = today.AddDays(11),
                TaxRate = 8.5m,
                Notes = "Thank you for your business!",
                LineItems =
                [
                    new() { Description = "Website redesign — homepage & product pages", Quantity = 1, UnitPrice = 4200m },
                    new() { Description = "Mobile responsive QA pass", Quantity = 8, UnitPrice = 95m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-013",
                SenderId = sender.Id,
                CustomerId = customers[1].Id,
                InvoiceDate = today.AddDays(-9),
                DueDate = today.AddDays(5),
                TaxRate = 0m,
                Notes = "Net 14. Wire transfer preferred.",
                LineItems =
                [
                    new() { Description = "Fleet tracking dashboard — Phase 2", Quantity = 1, UnitPrice = 6800m },
                    new() { Description = "API integration with carrier partners", Quantity = 20, UnitPrice = 110m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-012",
                SenderId = sender.Id,
                CustomerId = customers[2].Id,
                InvoiceDate = today.AddDays(-20),
                DueDate = today.AddDays(-6),
                TaxRate = 6.25m,
                Notes = "Past due — please remit payment at your earliest convenience.",
                LineItems =
                [
                    new() { Description = "Seasonal landscaping crew — March", Quantity = 40, UnitPrice = 65m },
                    new() { Description = "Irrigation system repair", Quantity = 1, UnitPrice = 380m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-011",
                SenderId = sender.Id,
                CustomerId = customers[3].Id,
                InvoiceDate = today.AddDays(-35),
                DueDate = today.AddDays(-21),
                TaxRate = 8.5m,
                Notes = "Thank you for your business!",
                LineItems =
                [
                    new() { Description = "Change management workshop (2 days)", Quantity = 2, UnitPrice = 1500m },
                    new() { Description = "Executive coaching session", Quantity = 3, UnitPrice = 450m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-010",
                SenderId = sender.Id,
                CustomerId = customers[0].Id,
                InvoiceDate = today.AddDays(-52),
                DueDate = today.AddDays(-38),
                TaxRate = 8.5m,
                Notes = "Thank you for your business!",
                LineItems =
                [
                    new() { Description = "Quarterly maintenance retainer", Quantity = 1, UnitPrice = 1200m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-009",
                SenderId = sender.Id,
                CustomerId = customers[1].Id,
                InvoiceDate = today.AddDays(-68),
                DueDate = today.AddDays(-54),
                TaxRate = 0m,
                Notes = "Net 14. Wire transfer preferred.",
                LineItems =
                [
                    new() { Description = "Warehouse inventory sync tool", Quantity = 1, UnitPrice = 5400m },
                    new() { Description = "On-site training (1 day)", Quantity = 1, UnitPrice = 900m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-008",
                SenderId = sender.Id,
                CustomerId = customers[2].Id,
                InvoiceDate = today.AddDays(-95),
                DueDate = today.AddDays(-81),
                TaxRate = 6.25m,
                Notes = "Thank you for your business!",
                LineItems =
                [
                    new() { Description = "Spring cleanup & mulching", Quantity = 1, UnitPrice = 640m },
                    new() { Description = "Tree trimming (5 trees)", Quantity = 5, UnitPrice = 85m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-007",
                SenderId = sender.Id,
                CustomerId = customers[3].Id,
                InvoiceDate = today.AddDays(-120),
                DueDate = today.AddDays(-106),
                TaxRate = 8.5m,
                Notes = "Thank you for your business!",
                LineItems =
                [
                    new() { Description = "Org design assessment", Quantity = 1, UnitPrice = 3200m },
                ],
            },
            new()
            {
                InvoiceNumber = "INV-2026-006",
                SenderId = sender.Id,
                CustomerId = customers[0].Id,
                InvoiceDate = today.AddDays(-140),
                DueDate = today.AddDays(-126),
                TaxRate = 8.5m,
                Notes = "Thank you for your business!",
                LineItems =
                [
                    new() { Description = "Checkout flow redesign", Quantity = 1, UnitPrice = 2950m },
                    new() { Description = "A/B testing setup", Quantity = 6, UnitPrice = 95m },
                ],
            },
        };

        db.Invoices.AddRange(invoices);
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
