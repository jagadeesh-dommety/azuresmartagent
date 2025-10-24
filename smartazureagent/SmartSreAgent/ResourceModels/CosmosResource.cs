public class CosmosResource : Resource
{
    public CosmosResource(string resourceId, string resourceName, string resourceType, string subscriptionId, string resourceGroupName)
        : base(resourceId, resourceName, resourceType, subscriptionId, resourceGroupName)
    {
    }

    public DateTime Timestamp { get; set; }  // Added for per-metric timestamp

    public double AvgRequestCharge { get; set; }
    public int ThrottledRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AvgLatencyMs { get; set; }

    public override string ToString()
    {
        return $"ResourceId: {ResourceId}, ResourceName: {ResourceName}, ResourceType: {ResourceType}, SubscriptionId: {SubscriptionId}, ResourceGroupName: {ResourceGroupName}, Timestamp: {Timestamp}, AvgRequestCharge: {AvgRequestCharge}, ThrottledRequests: {ThrottledRequests}, FailedRequests: {FailedRequests}, AvgLatencyMs: {AvgLatencyMs}";
    }
}