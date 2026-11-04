using Azure.Monitor.OpenTelemetry.AspNetCore;
using FileManager.Api.Options;
using FileManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Application Insights only when a connection string is provided (set as an App Service setting in Azure).
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// Fail at startup (not on the first request) if the storage connection string is missing.
builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
        "Storage:ConnectionString is not configured. Locally: dotnet user-secrets set Storage:ConnectionString <value>. In Azure: app setting Storage__ConnectionString.")
    .ValidateOnStart();
builder.Services.AddSingleton<IFileStorageService, BlobFileStorageService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
