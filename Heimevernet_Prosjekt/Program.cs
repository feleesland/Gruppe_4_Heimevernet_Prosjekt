using Heimevernet_Prosjekt.DataAccess;
using Microsoft.EntityFrameworkCore;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Heimevernet_Prosjekt.Services;
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllersWithViews();
//builder.Services.AddScoped<IResourceRepository, ResourceRepository>();

var connectionString = builder.Configuration.GetConnectionString("heimevernetdb");
builder.Services.AddDbContext<HeimevernetDbContext>(options =>
    options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 8))));

builder.Services.AddScoped<IResourceRepository, EfResourceRepository>();

// "Sends" emails by writing them to the log (development only)
builder.Services.AddTransient<IEmailSender, ConsoleEmailSender>();

// Login system (Identity) - help with AI
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    // From the teacher's code
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 10;
})
.AddEntityFrameworkStores<HeimevernetDbContext>()
.AddDefaultTokenProviders();

// Where to send people who aren't logged in / don't have access
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Create the database table and add test data (help with AI)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HeimevernetDbContext>();

    // Creates the Resources table if it doesn't exist
    db.Database.EnsureCreated();

    // Add some test resources if the table is empty
    if (!db.Resources.Any())
    {
        db.Resources.AddRange(
            new Resource { Name = "Name 1", Description = " Description 1", Type = "Type 1" },
            new Resource { Name = "Name 2", Description = "Description 2", Type = "Type 2" },
            new Resource { Name = "Name 3", Description = "Description 3", Type = "Type 3" }
        );
        db.SaveChanges();
    }

    // Create roles (always) - help with AI
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentitySeeder.SeedRolesAsync(roleManager);

    // Create test accounts (only when running locally)
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
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
