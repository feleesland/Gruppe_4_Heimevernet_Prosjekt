using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Heimevernet_Prosjekt.Models.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllersWithViews();
//builder.Services.AddScoped<IResourceRepository, ResourceRepository>();

// Database, repository, email and login (Identity) - Teacher: Program.cs
var connectionString = builder.Configuration.GetConnectionString("heimevernetdb");
builder.Services.AddDbContext<HeimevernetDbContext>(options =>
    options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 8))));

builder.Services.AddScoped<IResourceRepository, EfResourceRepository>();
builder.Services.AddTransient<IEmailSender, ConsoleEmailSender>();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Simpler password rules while testing - Docs: Identity on ASP.NET Core
    // https://learn.microsoft.com/aspnet/core/security/authentication/identity
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 10;
})
.AddEntityFrameworkStores<HeimevernetDbContext>()
.AddDefaultTokenProviders();

// Login and access denied pages - Docs: Identity on ASP.NET Core 
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Handler that checks who can read, edit and delete a resource - Docs: Resource-based authorization
// https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
builder.Services.AddSingleton<IAuthorizationHandler, ResourceAuthorizationHandler>();

var app = builder.Build();

// Migrations, roles and test users - Teacher: Program.cs
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HeimevernetDbContext>();
    db.Database.Migrate();

    // Test resources if the table is empty (help with AI) - Docs: Data Seeding
    // https://learn.microsoft.com/ef/core/modeling/data-seeding
    if (!db.Resources.Any())
    {
        db.Resources.AddRange(
            new Resource { Name = "Name 1", Description = " Description 1", Type = "Type 1" },
            new Resource { Name = "Name 2", Description = "Description 2", Type = "Type 2" },
            new Resource { Name = "Name 3", Description = "Description 3", Type = "Type 3" }
        );
        db.SaveChanges();
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentitySeeder.SeedRolesAsync(roleManager);

    // Test accounts only when running locally - Docs: Use multiple environments in ASP.NET Core
    // https://learn.microsoft.com/aspnet/core/fundamentals/environments
    if (app.Environment.IsDevelopment())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await IdentitySeeder.SeedDevelopmentAdminAsync(userManager);
        await IdentitySeeder.SeedDevelopmentUserAsync(userManager);
    }

}

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Norwegian, but with dot as decimal separator for map coordinates (help with AI) - Docs: Globalization and localization
// https://learn.microsoft.com/aspnet/core/fundamentals/localization
var culture = new CultureInfo("nb-NO");
culture.NumberFormat.NumberDecimalSeparator = ".";
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture),
    SupportedCultures = new List<CultureInfo> { culture },
    SupportedUICultures = new List<CultureInfo> { culture }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();