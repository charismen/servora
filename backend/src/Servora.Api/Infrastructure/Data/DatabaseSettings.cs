using MySqlConnector;

namespace Servora.Api.Infrastructure.Data;

public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    public string Host { get; init; } = "localhost";
    public uint Port { get; init; } = 3306;
    public string User { get; init; } = "root";
    public string Password { get; init; } = "password";
    public string Name { get; init; } = "servora";

    public string ToConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = Host,
            Port = Port,
            UserID = User,
            Password = Password,
            Database = Name
        };

        return builder.ConnectionString;
    }

    public static string BuildConnectionString(IConfiguration configuration)
    {
        var hasStructuredDatabaseConfig = configuration.GetSection(SectionName).Exists()
            || configuration["db_host"] is not null
            || configuration["db_port"] is not null
            || configuration["db_user"] is not null
            || configuration["db_password"] is not null
            || configuration["db_name"] is not null;

        if (!hasStructuredDatabaseConfig)
        {
            var legacyConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(legacyConnectionString))
            {
                return legacyConnectionString;
            }
        }

        return FromConfiguration(configuration).ToConnectionString();
    }

    public static DatabaseSettings FromConfiguration(IConfiguration configuration)
    {
        return new DatabaseSettings
        {
            Host = GetValue(configuration, "db_host", "localhost"),
            Port = uint.TryParse(GetValue(configuration, "db_port", "3306"), out var port) ? port : 3306,
            User = GetValue(configuration, "db_user", "root"),
            Password = GetValue(configuration, "db_password", "password"),
            Name = GetValue(configuration, "db_name", "servora")
        };
    }

    private static string GetValue(IConfiguration configuration, string key, string fallback)
    {
        var sectionValue = configuration[$"{SectionName}:{key}"];
        if (!string.IsNullOrWhiteSpace(sectionValue)) return sectionValue;

        var flatValue = configuration[key];
        return string.IsNullOrWhiteSpace(flatValue) ? fallback : flatValue;
    }
}
