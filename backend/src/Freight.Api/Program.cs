using System.Text.Json.Serialization;
using Firebase = FirebaseAdmin;
using Freight.Application;
using Freight.Application.Evaluation;
using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Notifications.Abstractions;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.Tracking.Services;
using Freight.Infrastructure.Notifications;
using Freight.Infrastructure.Persistence;
using Freight.Infrastructure.Routing;
using Freight.Infrastructure.Security;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddDbContext<FreightDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FreightDb")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDriverRuleEngine, DriverRuleEngine>();
builder.Services.AddScoped<RouteEtaCalculator>();
builder.Services.AddScoped<IShipmentInsertionEvaluator, ShipmentInsertionEvaluator>();
builder.Services.AddScoped<ShipmentInsertionPlanner>();
builder.Services.AddScoped<ShipmentEvaluationEngine>();

// OSRM routing (ADR 0011): a typed HttpClient for the raw calls, wrapped by a
// process-wide throttle so several concurrent assignments don't burst past the public
// demo server's ~1 req/sec limit.
builder.Services.Configure<OsrmOptions>(builder.Configuration.GetSection(OsrmOptions.SectionName));
builder.Services.AddHttpClient<OsrmRoutingService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OsrmOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<IRoutingService>(serviceProvider => new ThrottlingRoutingService(
    serviceProvider.GetRequiredService<OsrmRoutingService>(),
    serviceProvider.GetRequiredService<IOptions<OsrmOptions>>()));

// In-process cache for road geometry - a trip/fleet map is many geometry calls, each
// spaced ~1.1s by the throttle, and suburb-centroid coordinates repeat across trips.
builder.Services.AddMemoryCache();

// Device token encryption (ADR 0007) - the key lives only in local config
// (appsettings.Development.json, gitignored), never in source.
builder.Services.Configure<DeviceTokenEncryptionOptions>(
    builder.Configuration.GetSection(DeviceTokenEncryptionOptions.SectionName));
builder.Services.AddSingleton<IDeviceTokenEncryptor, DeviceTokenEncryptor>();

// Shipment-booking notifications (ADR 0003/0007): real FCM push when a service-account
// key is configured, otherwise a log-based no-op so the app still runs without one.
builder.Services.Configure<FcmOptions>(builder.Configuration.GetSection(FcmOptions.SectionName));
var fcmOptions = builder.Configuration.GetSection(FcmOptions.SectionName).Get<FcmOptions>() ?? new FcmOptions();
if (!string.IsNullOrWhiteSpace(fcmOptions.ServiceAccountPath) && File.Exists(fcmOptions.ServiceAccountPath))
{
    // FirebaseApp's default instance is process-global: a host built more than once in one
    // process (WebApplicationFactory builds one per test class, in parallel) must reuse it,
    // or Create throws - hence check-then-create under a process-wide lock.
    Firebase.FirebaseApp firebaseApp;
    lock (FirebaseInitLock)
    {
        firebaseApp = Firebase.FirebaseApp.DefaultInstance ?? Firebase.FirebaseApp.Create(new Firebase.AppOptions
        {
            Credential = GoogleCredential.FromFile(fcmOptions.ServiceAccountPath),
        });
    }
    builder.Services.AddSingleton(firebaseApp);
    builder.Services.AddScoped<INotificationSender, FcmNotificationSender>();
}
else
{
    builder.Services.AddScoped<INotificationSender, LogNotificationSender>();
}

const string WebAppCorsPolicy = "WebApp";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(WebAppCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddApplication();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(WebAppCorsPolicy);

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (RoutingUnavailableException ex)
    {
        // The routing provider (OSRM) was unreachable or had no route - not the caller's
        // fault, and retryable, so 503 rather than 400.
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.MapGet("/health", async (FreightDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapControllers();

app.Run();

public partial class Program
{
    private static readonly Lock FirebaseInitLock = new();
}
