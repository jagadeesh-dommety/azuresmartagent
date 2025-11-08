using SmartSreAgent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<CosmosMetricsCollector>();
builder.Services.AddSingleton<CosmosConfigCollector>();
builder.Services.AddHostedService<CosmosMetricsBackgroundWorker>(provider =>
{
    var collector = provider.GetRequiredService<CosmosMetricsCollector>();
    var configCollector = provider.GetRequiredService<CosmosConfigCollector>();
    return new CosmosMetricsBackgroundWorker(collector, configCollector);
});
builder.Services.AddSingleton<AzureDataLakeParquetWriter>(provider =>
{
var storageAccountName = "smartmetrics";
var fileSystemName = "metrics";
var directoryPath = "resources";
return new AzureDataLakeParquetWriter(storageAccountName, fileSystemName, directoryPath,
    provider.GetRequiredService<RAGCosmos>());
});
builder.Services.AddSingleton<RAGCosmos>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<RAGCosmos>>();
    var cosmosConfig = builder.Configuration.GetSection("RAGCosmosDB").Get<CosmosDBConfig>();
    return new RAGCosmos(cosmosConfig, logger);
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
