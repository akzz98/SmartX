using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Shared DTOs and TelemetryPacket<T> live in SmartX.Shared (project reference).
// Controllers will bind those types directly; JSON must keep enums readable and
// generic T as a struct, not object.

const string DashboardCorsPolicy = "SmartXDashboard";

builder.Services.AddCors(options =>
{
    // Blazor WASM runs on a different origin than this API (see Client launchSettings).
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? [];

    options.AddPolicy(DashboardCorsPolicy, policy =>
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // HealthState, SensorCategory, etc. serialize as "stale" not 2 — easier for the dashboard.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddOpenApi();
// One in-memory fleet for the simulation so registration (and later ingest) share the same devices.
builder.Services.AddSingleton<SmartX.Api.Storage.GatewayStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(DashboardCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
