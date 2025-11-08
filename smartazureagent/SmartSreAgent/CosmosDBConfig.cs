namespace SmartSreAgent
{
    public class CosmosDBConfig
    {
        public string EndpointUrl { get; set; }

        public string PrimaryKey { get; set; }

        public string DatabaseId { get; set; }

        public string ContainerId { get; set; }

        public string PartitionKeyPath { get; set; }
    }
}
