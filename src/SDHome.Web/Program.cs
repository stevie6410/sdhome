using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SDHome.Web.Data;
using SDHome.Web.Mappers;
using SDHome.Web.Models;
using SDHome.Web.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Optional: wire Serilog similarly to your old console app
builder.Host.UseSerilog((ctx, cfg) =>
{
    var seqUrl = ctx.Configuration["Logging:SeqUrl"] ?? "http://localhost:5341";

    cfg.MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.Seq(seqUrl);
});

// Bind config to options
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Signals:Mqtt"));
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection("Signals:Postgres"));
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection("Signals:Webhooks"));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("Logging"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection("Metrics"));

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// 🔌 Repository (write + read)
var postgresOptions = builder.Configuration
    .GetSection("Signals:Postgres")
    .Get<PostgresOptions>()
    ?? throw new InvalidOperationException("Signals Postgres config missing.");

var connectionString =
    postgresOptions.ConnectionString
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Signals connection string not configured");

builder.Services.AddSingleton<ISignalEventsRepository>(
    _ => new PostgeSqlSignalEventsRepository(connectionString));

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
