using Newtonsoft.Json;

namespace SmartSreAgent
{
    public record CosmosItem
    {
        [JsonProperty(PropertyName = "id")]
        public virtual string id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the Entity tag used for optimistic concurrency control
        /// </summary>
        [JsonProperty(PropertyName = "_etag")]
        public string? ETag { get; }

        /// <summary>
        /// Last updated timestamp
        /// </summary>
        [JsonProperty(PropertyName = "_ts")]
        public long LastModifiedTimeStamp { get; set; }

        [JsonProperty(PropertyName = "isDeleted")]
        public bool IsDeleted { get; set; } = false;

    }
}