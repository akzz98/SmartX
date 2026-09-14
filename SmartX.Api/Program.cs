using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using SmartX.Shared;

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
builder.Services.AddSingleton<SmartX.Api.Storage.AttachmentFileStore>();
builder.Services.Configure<FormOptions>(options =>
{
    // Keep multipart bodies at the diagnostic-file cap so a huge upload cannot stall ingest.
    options.MultipartBodyLengthLimit = AttachmentValidator.MaxBytes + 32_768;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(DashboardCorsPolicy);
app.UseAuthorization();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BadHttpRequestException) when (IsAttachmentUpload(context.Request))
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new UploadAttachmentResponse
            {
                Succeeded = false,
                Errors =
                [
                    "The upload is too large or the multipart body is invalid. Attachments must be 2 MB or smaller."
                ]
            });
        }
    }
});
app.MapControllers();

app.Run();

static bool IsAttachmentUpload(HttpRequest request)
{
    return HttpMethods.IsPost(request.Method)
        && request.Path.StartsWithSegments("/api/sensors")
        && request.Path.Value is { } path
        && path.Contains("/attachments", StringComparison.OrdinalIgnoreCase);
}
