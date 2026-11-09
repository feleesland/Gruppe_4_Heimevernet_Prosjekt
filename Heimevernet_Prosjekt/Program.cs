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
builder.Services.AddScoped<IResourceNeedRepository, EfResourceNeedRepository>();
builder.Services.AddScoped<IResourceTypeRepository, EfResourceTypeRepository>();
builder.Services.AddScoped<IResourceReportRepository, EfResourceReportRepository>();
builder.Services.AddScoped<IResourceRequestRepository, EfResourceRequestRepository>();
builder.Services.AddHostedService<ResourceAvailabilityService>();

// Kartverket search (places and addresses) with a typed HttpClient - Docs: Make HTTP requests using IHttpClientFactory in ASP.NET Core
// https://learn.microsoft.com/aspnet/core/fundamentals/http-requests
builder.Services.AddHttpClient<KartverketService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Heimevernet_Prosjekt");
})
// IPv6 first, IPv4 if IPv6 doesn't answer (see KartverketService.ConnectAsync)
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    ConnectCallback = KartverketService.ConnectAsync
});

// Development: emails go to the log; otherwise real email with SendGrid (help with AI) - Docs: Account confirmation and password recovery
// https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IEmailSender, ConsoleEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, EmailSender>();
    builder.Services.Configure<AuthenticationMessageSenderOptions>(options =>
        options.SendGridKey = builder.Configuration["SendGridKey"]);
}

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password rules (simpler while testing), email confirmation, unique email and lockout - Docs: Introduction to Identity on ASP.NET Core
    // https://learn.microsoft.com/aspnet/core/security/authentication/identity
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 10;
})
.AddEntityFrameworkStores<HeimevernetDbContext>()
.AddDefaultTokenProviders();

// Login and access denied pages - Docs: Introduction to Identity on ASP.NET Core
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Handler that checks who can read, edit and delete a resource - Docs: Resource-based authorization
// https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
builder.Services.AddSingleton<IAuthorizationHandler, ResourceAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, ResourceNeedAuthorizationHandler>();

var app = builder.Build();

// Migrations, roles and test data - Teacher: Program.cs
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HeimevernetDbContext>();
    db.Database.Migrate();
    
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentitySeeder.SeedRolesAsync(roleManager);

    // Resource types are needed everywhere (not only in development)
    ResourceDbSeeder.SeedResourceTypes(db);

    // Test accounts and test resources only when running locally - Docs: Use multiple environments in ASP.NET Core
    // https://learn.microsoft.com/aspnet/core/fundamentals/environments
    if (app.Environment.IsDevelopment())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await IdentitySeeder.SeedDevelopmentAdminAsync(userManager);
        await IdentitySeeder.SeedDevelopmentUserAsync(userManager);

        ResourceDbSeeder.SeedTestResources(db);
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