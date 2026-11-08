using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.ModelBinders;
using StockManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// NEU: Identity (Benutzerkonten, Passwort-Hashing, Anmeldung)
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

// NEU: Einstellungen für das Anmelde-Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<ReorderService>();
builder.Services.AddScoped<DemoDataSeeder>();

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new StrictDecimalModelBinderProvider());

    // NEU: Alle Controller verlangen eine Anmeldung, außer [AllowAnonymous]
    var requireLogin = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(requireLogin));
});

var app = builder.Build();

// NEU: Legt den ersten Administrator an, falls noch kein Benutzer existiert
// Datenbank beim Start anlegen bzw. auf den neuesten Stand bringen (nur auf Wunsch, z. B. in Docker)
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationContext = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await migrationContext.Database.MigrateAsync();
}
await IdentitySeeder.SeedAdminAsync(app.Services, app.Configuration);

// Demodaten nur auf Wunsch und nur in einer Datenbank ohne Fachdaten
if (app.Configuration.GetValue<bool>("Seed:DemoData"))
{
    using var demoScope = app.Services.CreateScope();
    var demoSeeder = demoScope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    var demoResult = await demoSeeder.SeedAsync();
    app.Logger.LogInformation("Demodaten: {Message}", demoResult.Message);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Im Container gibt es kein HTTPS: Dort schaltet die Konfiguration die Umleitung ab.
if (!app.Configuration.GetValue<bool>("Hosting:DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthentication();   // NEU: muss VOR UseAuthorization stehen
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();