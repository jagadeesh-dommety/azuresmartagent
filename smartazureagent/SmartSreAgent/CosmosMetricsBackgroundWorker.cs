using System;
using System.ComponentModel;

namespace SmartSreAgent;

public class CosmosMetricsBackgroundWorker : BackgroundService
{
    private readonly CosmosMetricsCollector CosmosMetricsCollector;
    private readonly CosmosConfigCollector CosmosConfigCollector;
    public CosmosMetricsBackgroundWorker(CosmosMetricsCollector cosmosMetricsCollector, CosmosConfigCollector cosmosConfigCollector)
    {
        this.CosmosMetricsCollector = cosmosMetricsCollector;
        this.CosmosConfigCollector = cosmosConfigCollector;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"CosmosMetricsBackgroundWorker is running. {DateTimeOffset.Now}");
            await this.CosmosMetricsCollector.CollectCosmosDbMetrics();
            await this.CosmosConfigCollector.CollectCosmosDbConfig();
            await this.CosmosConfigCollector.GetAdvisorRecommendations();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
