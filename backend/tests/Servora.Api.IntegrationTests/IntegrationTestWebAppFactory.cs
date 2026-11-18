using System.Data.Common;
using Servora.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MySql;
using Xunit;

namespace Servora.Api.IntegrationTests;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MySqlContainer _mySqlContainer = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("ServoraTestDb")
        .WithUsername("root")
        .WithPassword("secret")
        .Build();
    private ServerVersion? _serverVersion;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hangfire:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // Remove existing AppDbContext, options, and EF Core options configurations from Program.cs
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(AppDbContext));
            for (var i = services.Count - 1; i >= 0; i--)
            {
                var serviceType = services[i].ServiceType;
                if (serviceType.IsGenericType
                    && serviceType.GenericTypeArguments.Length == 1
                    && serviceType.GenericTypeArguments[0] == typeof(AppDbContext)
                    && serviceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal))
                {
                    services.RemoveAt(i);
                }
            }

            // Add AppDbContext with Testcontainers MySQL (using once-detected server version)
            var connectionString = _mySqlContainer.GetConnectionString();
            var serverVersion = _serverVersion ??= ServerVersion.AutoDetect(connectionString);
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseMySql(connectionString, serverVersion)
                       .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Mock Authentication
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                o.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                o.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.AuthenticationScheme, options => { });
        });
    }

    public async Task InitializeAsync()
    {
        await _mySqlContainer.StartAsync();

        // Run migrations
        using var scope = Services.CreateScope();
        await Servora.Api.Infrastructure.Data.Seed.DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    public new async Task DisposeAsync()
    {
        await _mySqlContainer.StopAsync();
    }
}
