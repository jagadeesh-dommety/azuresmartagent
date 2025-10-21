using System;
using System.ComponentModel;

namespace SmartSreAgent;

public class CosmosMetricsBackgroundWorker : BackgroundService
{
    private readonly CosmosMetricsCollector CosmosMetricsCollector;
    public CosmosMetricsBackgroundWorker(CosmosMetricsCollector cosmosMetricsCollector)
    {
        this.CosmosMetricsCollector = cosmosMetricsCollector;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"CosmosMetricsBackgroundWorker is running. {DateTimeOffset.Now}");
            this.CosmosMetricsCollector.CollectCosmosDbMetrics();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
