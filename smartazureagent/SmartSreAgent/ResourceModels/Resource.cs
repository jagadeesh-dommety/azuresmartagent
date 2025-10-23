public class Resource
{
    public Resource(string resourceId, string resourceName, string resourceType, string subscriptionId, string resourceGroupName)
    {
        ResourceId = resourceId;
        ResourceName = resourceName;
        ResourceType = resourceType;
        SubscriptionId = subscriptionId;
        ResourceGroupName = resourceGroupName;
    }
    public string ResourceId { get; set; }
    public string ResourceName { get; set; }
    public string ResourceType { get; set; }
    public string SubscriptionId { get; set; }
    public string ResourceGroupName { get; set; }
}