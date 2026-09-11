using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Endpoints;
using TransitRealtime.Web.Hubs;
using TransitRealtime.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 12;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Services", "RequireAdmin");
    options.Conventions.AuthorizeFolder("/Admin", "RequireAdmin");
    // Public self-registration is disabled entirely; admin accounts are created from /Admin/Users/Create instead.
    options.Conventions.AuthorizeAreaPage("Identity", "/Account/Register", "RequireAdmin");
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<ConnectionTracker>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<WebhookDispatcher>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("webhook", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 262_144; // 256 KB webhook payload cap
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<TransitHub>("/hub");
app.MapWebhookEndpoints();

await SeedData.EnsureAdminSeededAsync(app);

app.Run();
