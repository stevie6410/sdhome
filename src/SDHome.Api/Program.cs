using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SpaServices.Extensions;
using Microsoft.EntityFrameworkCore;
using SDHome.Api.HealthChecks;
using SDHome.Lib.Data;
using SDHome.Lib.Mappers;
using SDHome.Lib.Models;
using SDHome.Lib.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// NSwag OpenAPI generator
builder.Services.AddOpenApiDocument(options =>
{
    options.Title = "SDHome API";
    options.Version = "v1";
    options.DocumentName = "v1";  // <-- matches nswag.json documentName
});

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Signals:Mqtt"));
builder.Services.Configure<MsSQLOptions>(builder.Configuration.GetSection("Signals:MSSQL"));
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection("Signals:Webhooks"));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("Logging"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection("Metrics"));

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// NSwag OpenAPI generator
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApiDocument(options =>
{
    options.Title = "SDHome API";
    options.Version = "v1";
    options.DocumentName = "v1";  // <-- matches nswag.json documentName
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// EF Core DbContext
builder.Services.AddDbContext<SignalsDbContext>(options =>
    options.UseSqlServer(connectionString));

// Services
builder.Services.AddSingleton<ISignalEventMapper, SignalEventMapper>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<ISignalEventProjectionService, SignalEventProjectionService>();
builder.Services.AddScoped<ISignalsService, SignalsService>();
builder.Services.AddScoped<DatabaseSeeder>();

// HttpClient for webhook calls
builder.Services.AddHttpClient<ISignalsService, SignalsService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString!, name: "sqlserver", tags: ["db", "sql"])
    .AddCheck<MqttHealthCheck>("mqtt", tags: ["messaging"]);

// MQTT Client for DeviceService - only register if MQTT is enabled
var mqttOptions = builder.Configuration.GetSection("Signals:Mqtt").Get<MqttOptions>();
if (mqttOptions?.Enabled == true)
{
    builder.Services.AddSingleton(sp =>
    {
        var factory = new MQTTnet.MqttClientFactory();
        return factory.CreateMqttClient();
    });
}

// Only add background workers if not in NSwag environment
if (!builder.Environment.IsEnvironment("NSwag"))
{
    builder.Services.AddHostedService<SignalsMqttWorker>();
}

var app = builder.Build();

app.UseCors("DevCors");

// Ensure SQL Server table/indexes exist at startup
// Using EF Core migrations - apply pending migrations
// Only ensure DB in normal runtime, not during NSwag or design-time builds
if (!app.Environment.IsEnvironment("NSwag"))
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SignalsDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}

// Configure the HTTP request pipeline.
app.UseOpenApi();        // serves OpenAPI/Swagger document
app.UseSwaggerUi();      // serves Swagger UI at /swagger

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db")
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Just checks if the app is running
});

// Serve Angular SPA
app.UseStaticFiles();

if (!app.Environment.IsEnvironment("NSwag"))
{
    app.UseSpa(spa =>
    {
        spa.Options.SourcePath = "../ClientApp";

        if (app.Environment.IsDevelopment())
        {
            // Proxy to Angular dev server during development
            spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
        }
    });
}

app.Run();
