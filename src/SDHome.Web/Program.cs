using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SDHome.Web.Data;
using SDHome.Web.Models;
using SDHome.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Load all config sections as options
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Signals:Mqtt"));
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection("Signals:Postgres"));
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection("Signals:Webhooks"));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("Logging"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection("Metrics"));


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var postgresOptions = builder.Configuration
    .GetSection("Signals:Postgres")
    .Get<PostgresOptions>()
    ?? throw new InvalidOperationException("Signals Postgres config missing.");

var connectionString =
    postgresOptions.ConnectionString
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Signals connection string not configured");

builder.Services.AddSingleton<ISignalEventsRepository>(_ => new PostgresSignalEventsRepository(connectionString));
builder.Services.AddScoped<ISignalQueryService, SignalQueryService>();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
