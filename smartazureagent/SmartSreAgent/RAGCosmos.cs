using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace SmartSreAgent;

public class RAGCosmos : CosmosDbClientBase<EmbeddingDocument>
{
    public RAGCosmos(CosmosDBConfig? cosmosDBConfig, ILogger logger) : base(cosmosDBConfig, logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosDBConfig, nameof(cosmosDBConfig));
        var initialized = this.Initialize().Result;
    }

    public override PartitionKey GetPartionKeyFromDocument(EmbeddingDocument document)
    {
        return new PartitionKey(document.resourceid);
    }
}