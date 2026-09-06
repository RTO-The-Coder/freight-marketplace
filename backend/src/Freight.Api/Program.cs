using System.Text.Json.Serialization;
using Freight.Application.Client;
using Freight.Application.Fleet;
using Freight.Application.Routing;
using Freight.Application.Simulation;
using Freight.Application.Tracking;
using Freight.Domain.Common;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.Tracking.Services;
using Freight.Infrastructure.Persistence;
using Freight.Infrastructure.Routing;
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

const string WebAppCorsPolicy = "WebApp";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(WebAppCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddScoped<AddTruckHandler>();
builder.Services.AddScoped<AddDriverHandler>();
builder.Services.AddScoped<AssignDriversHandler>();
builder.Services.AddScoped<RemoveDriversHandler>();
builder.Services.AddScoped<SetTruckActivationHandler>();
builder.Services.AddScoped<GetFleetTreeHandler>();
builder.Services.AddScoped<GetTruckingCompaniesHandler>();
builder.Services.AddScoped<GetTruckingCompanyByIdHandler>();
builder.Services.AddScoped<GetTrucksHandler>();
builder.Services.AddScoped<GetDriversHandler>();
builder.Services.AddScoped<GetTruckForDriverHandler>();
builder.Services.AddScoped<GetTruckDetailHandler>();
builder.Services.AddScoped<GetTruckEtasHandler>();
builder.Services.AddScoped<GetTruckPositionHandler>();
builder.Services.AddScoped<GetRouteLegHandler>();
builder.Services.AddScoped<GetRouteGeometryHandler>();
builder.Services.AddScoped<GetDriverDetailHandler>();
builder.Services.AddScoped<SetTruckCompanyHandler>();
builder.Services.AddScoped<BookShipmentHandler>();
builder.Services.AddScoped<UpdatePickupWindowHandler>();
builder.Services.AddScoped<GetShippersHandler>();
builder.Services.AddScoped<GetShipmentsByShipperHandler>();
builder.Services.AddScoped<GetPendingShipmentsHandler>();
builder.Services.AddScoped<AssignShipmentToTruckHandler>();
builder.Services.AddScoped<RescheduleTripHandler>();
builder.Services.AddScoped<CheckDriverEligibilityHandler>();
builder.Services.AddScoped<SimulationClockHandler>();
builder.Services.AddScoped<SimulationAdvanceHandler>();

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

public partial class Program;
