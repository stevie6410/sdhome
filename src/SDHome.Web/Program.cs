using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Options;
using SDHome.Web.Data;
using SDHome.Web.Mappers;
using SDHome.Web.Models;
using SDHome.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Bind config to options
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Signals:Mqtt"));
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection("Signals:Postgres"));
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection("Signals:Webhooks"));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("Logging"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection("Metrics"));

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// --- Connection string from config ---
var postgresOptions = builder.Configuration
    .GetSection("Signals:Postgres")
    .Get<PostgresOptions>()
    ?? throw new InvalidOperationException("Signals Postgres config missing.");

var connectionString =
    postgresOptions.ConnectionString
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Signals connection string not configured");

// 🔌 Repository (read + write)
builder.Services.AddSingleton<ISignalEventsRepository>(_ => new PostgresSignalEventsRepository(connectionString));
// ^^^ make sure this class name matches whatever is in Data/PostgeSqlSignalEventsRepository.cs

// 🔌 Mapper (MQTT payload → SignalEvent)
builder.Services.AddSingleton<ISignalEventMapper, SignalEventMapper>();

// 🔌 Query service for Blazor UI
builder.Services.AddScoped<ISignalQueryService, SignalQueryService>();

// 🔌 HttpClient for webhooks
builder.Services.AddHttpClient();

// 🔌 Core Signals service (metrics + DB + webhooks)
builder.Services.AddSingleton<ISignalsService, SignalsService>();

// 🔌 Background MQTT listener
builder.Services.AddHostedService<SignalsMqttWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
