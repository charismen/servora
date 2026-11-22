using System.Text;
using System.Text.Json.Serialization;
using Servora.Api.Features.Identity.Auth;
using Servora.Api.Features.Activity;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Dispatch;
using Servora.Api.Features.Maintenance;
using Servora.Api.Features.Payments;
using Servora.Api.Features.Organizations;
using Servora.Api.Features.ServiceStatuses;
using Servora.Api.Features.ServiceTemplates;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.Inventory;
using Servora.Api.Features.Reports;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Shops;
using Servora.Api.Features.Attendance;
using Servora.Api.Features.Payroll;
using Servora.Api.Features.Payroll.Bonuses;
using Servora.Api.Features.Assets;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.Data.Interceptors;
using Servora.Api.Infrastructure.Data.Seed;
using Servora.Api.Infrastructure.Exceptions;
using Servora.Api.Infrastructure.Security;
using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using System.Threading.RateLimiting;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

var configuredTimeZone = builder.Configuration["TimeZone:Default"]
    ?? builder.Configuration["Timezone:Default"]
    ?? builder.Configuration["Operations:DefaultTimeZone"]
    ?? builder.Configuration["DefaultTimeZone"];

if (!string.IsNullOrWhiteSpace(configuredTimeZone))
{
    ShopTimeZoneHelper.DefaultTimeZoneId = configuredTimeZone;
    ShopProfileSettingKeys.SetDefaultTimeZone(configuredTimeZone);
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Servora.Api.Infrastructure.ShopContext.IShopContext, Servora.Api.Infrastructure.ShopContext.ShopContext>();
builder.Services.AddScoped<Servora.Api.Infrastructure.Activity.IActivityLogger, Servora.Api.Infrastructure.Activity.ActivityLogger>();
builder.Services.AddScoped<AuditableEntityInterceptor>();
builder.Services.AddScoped<Servora.Api.Infrastructure.Data.Interceptors.AuditLogInterceptor>();
builder.Services.AddScoped<Servora.Api.Infrastructure.Notifications.INotificationService, Servora.Api.Infrastructure.Notifications.InAppNotificationService>();
builder.Services.AddScoped<IBonusEvaluator, MaintenanceCompletionBonusEvaluator>();
builder.Services.AddScoped<IBonusEvaluator, CustomerCancellationBonusEvaluator>();
builder.Services.AddScoped<IBonusEvaluator, TaskItemizedBonusEvaluator>();
builder.Services.AddScoped<PayrollCalculationService>();
builder.Services.AddScoped<BlueprintProvisioningService>();
builder.Services.AddScoped<PreventiveMaintenanceJob>();

var connectionString = DatabaseSettings.BuildConnectionString(builder.Configuration);
ServerVersion? cachedServerVersion = null;
var serverVersionLock = new object();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var serverVersion = cachedServerVersion;
    if (serverVersion is null)
    {
        lock (serverVersionLock)
        {
            serverVersion ??= ServerVersion.AutoDetect(connectionString);
            cachedServerVersion = serverVersion;
        }
    }

    options.UseMySql(connectionString, serverVersion)
           .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddIdentityCore<Servora.Api.Features.Identity.Users.ApplicationUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddRoles<Servora.Api.Features.Identity.Roles.ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Servora.Api.Infrastructure.Auth.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Servora.Api.Infrastructure.Auth.PermissionAuthorizationHandler>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "super-secret-key-that-should-be-very-long-in-production"))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenService>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Servora API",
        Version = "v1",
        Description = "Multi-industry service, repair, and maintenance operations platform API."
    });
    options.CustomSchemaIds(type => (type.FullName ?? type.Name)
        .Replace("Servora.Api.Features.", string.Empty)
        .Replace('+', '.'));

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste JWT token only. Swagger UI will send it as Authorization: Bearer {token}."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document, null),
            []
        }
    });
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSignalR();

builder.Services.AddHttpClient("ExternalApi")
    .AddStandardResilienceHandler();
builder.Services.AddHttpClient(XenditGatewayAdapter.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.xendit.co/");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient(StripeGatewayAdapter.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.stripe.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
builder.Services.AddScoped<IPaymentGateway, XenditGatewayAdapter>();
builder.Services.AddScoped<IPaymentGateway, StripeGatewayAdapter>();
builder.Services.AddScoped<IPaymentGatewayRegistry, PaymentGatewayRegistry>();
builder.Services.AddScoped<PaymentCheckoutService>();
builder.Services.AddScoped<PaymentIntentExpiryJob>();
builder.Services.AddScoped<DispatchSchedulingService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
    options.AddFixedWindowLimiter("Global", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.AddPolicy("PublicTrackingVerify", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "public-verify",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var hangfireEnabled = builder.Configuration.GetValue("Hangfire:Enabled", true);
if (hangfireEnabled)
{
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseStorage(new Hangfire.MySql.MySqlStorage(connectionString, new Hangfire.MySql.MySqlStorageOptions
        {
            TablesPrefix = "hangfire_",
            PrepareSchemaIfNecessary = true
        }))
    );
    builder.Services.AddHangfireServer();
}

var app = builder.Build();

await app.SeedDatabaseAsync();

if (hangfireEnabled)
{
    using var scope = app.Services.CreateScope();
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<PreventiveMaintenanceJob>(
        "preventive-maintenance-scan",
        job => job.ProcessDueSchedulesAsync(CancellationToken.None, null),
        Cron.Hourly);
    recurringJobManager.AddOrUpdate<PaymentIntentExpiryJob>(
        "payment-intent-expiry-sweep",
        job => job.ExpirePastDueIntentsAsync(CancellationToken.None),
        "*/15 * * * *");
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Servora API v1");
    });
    if (hangfireEnabled)
    {
        app.UseHangfireDashboard("/hangfire");
    }
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseRateLimiter();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

var versionedGroup = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

Login.MapEndpoint(versionedGroup);
CurrentUser.MapEndpoint(versionedGroup);
OrganizationWorkspaceEndpoints.MapEndpoint(versionedGroup);
BlueprintEndpoints.MapEndpoint(versionedGroup);
GetShopProfileSettings.MapEndpoint(versionedGroup);
UpdateShopProfileSettings.MapEndpoint(versionedGroup);
PrintTemplateSettingsEndpoints.MapEndpoint(versionedGroup);
PaymentGatewayEndpoints.MapEndpoint(versionedGroup);
if (!app.Environment.IsProduction())
{
    Servora.Api.Features.Identity.Auth.DemoAccounts.MapEndpoint(versionedGroup);
}
CreateCustomer.MapEndpoint(versionedGroup);
GetCustomers.MapEndpoint(versionedGroup);
GetCustomer.MapEndpoint(versionedGroup);
UpdateCustomer.MapEndpoint(versionedGroup);
ToggleBan.MapEndpoint(versionedGroup);
ToggleStar.MapEndpoint(versionedGroup);
DeleteCustomer.MapEndpoint(versionedGroup);
CustomerLocationEndpoints.MapEndpoint(versionedGroup);
ServiceStatusEndpoints.MapEndpoint(versionedGroup);
DeviceEndpoints.MapEndpoint(versionedGroup);
ServiceTicketEndpoints.MapEndpoint(versionedGroup);
ServiceTemplateEndpoints.MapEndpoint(versionedGroup);
MaintenanceScheduleEndpoints.MapEndpoint(versionedGroup);
DispatchEndpoints.MapEndpoint(versionedGroup);
NotificationTemplateEndpoints.MapEndpoint(versionedGroup);
InventoryEndpoints.MapEndpoint(versionedGroup);
InventoryImportEndpoints.MapEndpoint(versionedGroup);
ActivityEndpoints.MapEndpoint(versionedGroup);
ReportsEndpoints.MapEndpoint(versionedGroup);
ShopUserEndpoints.MapEndpoint(versionedGroup);
AttendanceEndpoints.MapEndpoint(versionedGroup);
PayrollEndpoints.MapEndpoint(versionedGroup);
AssetEndpoints.MapEndpoint(versionedGroup);

app.MapHub<Servora.Api.Infrastructure.Notifications.NotificationHub>("/hubs/notifications");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Servora.Api" }))
    .WithName("HealthCheck")
    .WithOpenApi();

app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { status = "Healthy", database = "Connected" })
        : Results.Problem("Database connection failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
})
    .WithName("ReadinessCheck")
    .WithOpenApi();

app.Run();

public partial class Program { }
