namespace Servora.Api.Infrastructure.Data.Seed;

public static class DatabaseSeederExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        var runOnStartup = app.Configuration.GetValue<bool>("SeedData:RunOnStartup", !app.Environment.IsProduction());
        if (!runOnStartup)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }
}
