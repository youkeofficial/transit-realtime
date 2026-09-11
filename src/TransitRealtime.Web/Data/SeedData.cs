using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TransitRealtime.Web.Data;

public static class SeedData
{
    public const string AdminRole = "Admin";

    public static async Task EnsureAdminSeededAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(AdminRole))
            await roleManager.CreateAsync(new IdentityRole(AdminRole));

        var email = app.Configuration["SeedAdmin:Email"];
        var password = app.Configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, AdminRole))
                await userManager.AddToRoleAsync(existing, AdminRole);
            return;
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, AdminRole);
        }
        else
        {
            var logger = services.GetRequiredService<ILogger<WebApplication>>();
            logger.LogWarning(
                "SeedAdmin account creation failed for {Email}: {Errors}",
                email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
