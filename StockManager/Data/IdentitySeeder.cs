using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace StockManager.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration configuration)
        {
            using var scope = services.CreateScope();

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

            if (await userManager.Users.AnyAsync())
            {
                return;
            }

            var email = configuration["Seed:AdminEmail"];
            var password = configuration["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "Es existiert kein Benutzer, und Seed:AdminEmail / Seed:AdminPassword sind nicht gesetzt. Anmeldung ist nicht möglich.");
                return;
            }

            var admin = new IdentityUser
            {
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, password);

            if (!result.Succeeded)
            {
                logger.LogError(
                    "Der Administrator konnte nicht angelegt werden: {Errors}",
                    string.Join(" | ", result.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Administrator {Email} wurde angelegt.", admin.Email);
        }
    }
}