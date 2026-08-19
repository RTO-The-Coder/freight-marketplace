using System.Text.Json.Serialization;
using Freight.Application.Fleet;
using Freight.Application.Shipments;
using Freight.Domain.Common;
using Freight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddScoped<ActivateTruckHandler>();
builder.Services.AddScoped<DeactivateTruckHandler>();
builder.Services.AddScoped<GetFleetTreeHandler>();
builder.Services.AddScoped<GetTruckingCompaniesHandler>();
builder.Services.AddScoped<GetTrucksHandler>();
builder.Services.AddScoped<GetDriversHandler>();
builder.Services.AddScoped<GetTruckForDriverHandler>();
builder.Services.AddScoped<GetTruckDetailHandler>();
builder.Services.AddScoped<GetDriverDetailHandler>();
builder.Services.AddScoped<AssignTruckToCompanyHandler>();
builder.Services.AddScoped<UnassignTruckFromCompanyHandler>();
builder.Services.AddScoped<BookShipmentHandler>();
builder.Services.AddScoped<UpdatePickupWindowHandler>();
builder.Services.AddScoped<GetShippersHandler>();
builder.Services.AddScoped<GetShipmentsByShipperHandler>();

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
