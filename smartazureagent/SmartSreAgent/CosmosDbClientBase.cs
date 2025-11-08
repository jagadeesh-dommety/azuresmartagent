using Azure.Identity;
using Microsoft.Azure.Cosmos;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Net;

namespace SmartSreAgent
{
    public abstract class CosmosDbClientBase<T> where T : CosmosItem
    {
        protected Database database;
        protected Container container;
        protected CosmosClient cosmosClient;
        protected readonly ILogger logger;

        private readonly string cosmosDBPrimaryKey;
        private readonly string endpointUrl;
        private readonly string databaseId;
        private readonly string containerId;
        private readonly string partitionKeyPath;

        public CosmosDbClientBase(CosmosDBConfig cosmosDBConfig, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(cosmosDBConfig);

            cosmosDBPrimaryKey = cosmosDBConfig.PrimaryKey;
            endpointUrl = cosmosDBConfig.EndpointUrl;
            databaseId = cosmosDBConfig.DatabaseId;
            containerId = cosmosDBConfig.ContainerId;
            partitionKeyPath = cosmosDBConfig.PartitionKeyPath;

            this.logger = logger;
        }

        protected async Task<bool> Initialize()
        {
            if (container != null)
            {
                return true;
            }
            try
            {
                if (cosmosDBPrimaryKey != null)
                {
                    cosmosClient = new CosmosClient(endpointUrl, cosmosDBPrimaryKey);
                }else
                {
                    cosmosClient = new CosmosClient(endpointUrl, new DefaultAzureCredential());
                }
                
                database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
                container = await database.CreateContainerIfNotExistsAsync(containerId, partitionKeyPath);
            }
            catch (Exception ex)
            {
                throw new Exception("Not able to initialize CosmosDB client", ex);
            }

            return true;
        }

        public abstract PartitionKey GetPartionKeyFromDocument(T document);



        /// <summary>
        /// Create an item in CosmosDB.
        /// </summary>
        /// <param name="document">The item to create.</param>
        /// <param name="cancelToken">Cancellation token</param>
        /// <returns>Create operation status</returns>
        internal async Task<T> CreateItemAsync(T document, CancellationToken cancelToken = default)
        {
            var partitionKey = GetPartionKeyFromDocument(document);

            // For workloads that have heavy create payloads, set the EnableContentResponseOnWrite request option to false.
            // The service will no longer return the created or updated resource to the SDK.
            ItemRequestOptions requestOptions = new ItemRequestOptions() { EnableContentResponseOnWrite = false };
            ItemResponse<T> response = await container.CreateItemAsync<T>(document, partitionKey, requestOptions, cancelToken);
            HttpStatusCode statusCode = response.StatusCode;

            logger.LogDebug("CreateItemAsync RU used", document.id, response.RequestCharge);

            if (statusCode != HttpStatusCode.Created)
            {
                logger.LogError($"CreateItemAsync operation Failed: {response.StatusCode}", document.id, 0);
            }
            return response.Resource;
        }

        /// <summary>
        /// Upsert an item into CosmosDB.
        /// </summary>
        /// <param name="document">The item to upsert.</param>
        /// <param name="cancelToken">Cancellation token</param>
        /// <returns>Upsert status</returns>
        public async Task<bool> UpsertItemAsync(T document, CancellationToken cancelToken = default)
        {
            var partitionKey = GetPartionKeyFromDocument(document);
            // For workloads that have heavy create payloads, set the EnableContentResponseOnWrite request option to false.
            // The service will no longer return the created or updated resource to the SDK.
            ItemRequestOptions requestOptions = new ItemRequestOptions() { EnableContentResponseOnWrite = false };
            ItemResponse<T> response = await container.UpsertItemAsync(document, partitionKey, requestOptions, cancelToken);
            HttpStatusCode statusCode = response.StatusCode;

            logger.LogDebug("UpsertItemAsync RU used", document.id, response.RequestCharge);

            if ((statusCode != HttpStatusCode.OK) || (statusCode != HttpStatusCode.Created))
            {
                logger.LogError($"UpsertItemAsync operation Failed: {response.StatusCode}", document.id, 0);
            }
            return statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Created;
        }

        /// <summary>
        /// Upsert many items into CosmosDB. -> there is a issue for conflicts
        /// </summary>
        /// <param name="documents"></param>
        /// <param name="partitionKey"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public async Task<bool> UpsertItemsAsync(IEnumerable<T> documents, PartitionKey partitionKey, CancellationToken cancelToken = default)
        {
            if (documents == null || !documents.Any())
            {
                return false;
            }
            TransactionalBatch batch = container.CreateTransactionalBatch(partitionKey);
            foreach (var document in documents)
            {
                batch.UpsertItem(document, new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false });
            }
            TransactionalBatchResponse response = await batch.ExecuteAsync(cancelToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"UpsertItemsAsync operation failed with status code: {response.StatusCode}");
                return false;
            }
            logger.LogDebug("UpsertItemsAsync RU used", response.RequestCharge);
            return true;
        }

        /// <summary>
        /// Read an item from CosmosDB.
        /// </summary>
        /// <param name="documentId">The ID to use.</param>
        /// <param name="partitionKey">The partition key to use.</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The read item.</returns>
        internal async Task<T> ReadItemByDocumentIdAsync(string documentId, PartitionKey partitionKey, CancellationToken cancellation = default)
        {
            T resultDocument;

            try
            {
                ItemResponse<T> readItemResponse = await container.ReadItemAsync<T>(documentId, partitionKey, cancellationToken: cancellation);
                resultDocument = readItemResponse;
                logger.LogDebug("ReadItemByDocumentIdAsync RU used", documentId, readItemResponse.RequestCharge);
            }
            // cosmosdb SDK throws an exception when document not found for the id
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                resultDocument = default(T);
            }
            catch (Exception ex)
            {
                throw new Exception("ReadItemByDocumentIdAsync failed.", ex);
            }

            return resultDocument;
        }

        internal async Task<int> CountItemsInPartition(PartitionKey partitionKey)
        {
            try
            {
                // More efficient query that specifies cross-partition = false
                var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
                var queryOptions = new QueryRequestOptions
                {
                    PartitionKey = partitionKey,
                    MaxItemCount = 1,             // Since we only need one result
                    MaxConcurrency = 1,           // Single partition query
                    EnableScanInQuery = false     // Disable scan since we're counting
                };

                using (FeedIterator<int> queryResultSetIterator = container.GetItemQueryIterator<int>(
                    query,
                    requestOptions: queryOptions))
                {
                    FeedResponse<int> currentResultSet = await queryResultSetIterator.ReadNextAsync();

                    // Log the RU charge
                    logger.LogDebug($"Count operation consumed {currentResultSet.RequestCharge} RUs");

                    return currentResultSet.FirstOrDefault();
                }
            }
            catch (CosmosException cx)
            {
                logger.LogError(cx, $"Cosmos DB error occurred while counting items in partition. StatusCode: {cx.StatusCode}");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while counting items in partition.");
                throw;
            }
        }

        /// <summary>
        /// Delete an item from CosmosDB.
        /// </summary>
        /// <param name="documentId">The ID to use.</param>
        /// <param name="partitionKey">The partition key to use.</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The read item.</returns>
        protected async Task<bool> DeleteItemAsync(string documentId, PartitionKey partitionKey, CancellationToken cancellation = default)
        {
            T resultDocument;

            try
            {
                ItemResponse<T> readItemResponse = await container.DeleteItemAsync<T>(documentId, partitionKey, cancellationToken: cancellation);
                resultDocument = readItemResponse;
                logger.LogDebug("DeleteItemAsync RU used", documentId, readItemResponse.RequestCharge);
            }
            // cosmosdb SDK throws an exception when document not found for the id
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("DeleteItemAsync failed.", ex);
            }

            return true;
        }

        /// <summary>
        /// Read many items from CosmosDB.
        /// </summary>
        /// <param name="ids">The IDs to use.</param>
        /// <param name="partitionKey">The partition key to use.</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The read list of items.</returns>
        protected async Task<IList<T>> ReadManyItemsAsync(IList<string> ids, string partitionKey, CancellationToken cancellation = default)
        {
            IList<T> resultDocuments;

            try
            {
                var itemKeys = ids.Select(item => (item, new PartitionKey(partitionKey))).ToList();
                FeedResponse<T> feedResponse = await container.ReadManyItemsAsync<T>(itemKeys, cancellationToken: cancellation);
                resultDocuments = feedResponse.Resource.ToList();
                logger.LogDebug("ReadManyItemsAsync RU used", "", feedResponse.RequestCharge);

            }
            // cosmosdb SDK throws an exception when document not found for the id
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                resultDocuments = new List<T>();
            }
            catch (Exception ex)
            {
                throw new Exception("ReadManyItemsAsync failed.", ex);
            }

            return resultDocuments;
        }

        public T? QuerySingle<T>(QueryDefinition query, QueryRequestOptions requestoptions)
        {
            FeedIterator<T> feedIterator = container.GetItemQueryIterator<T>(query, requestOptions: requestoptions);
            if (feedIterator.HasMoreResults)
            {
                FeedResponse<T> response = feedIterator.ReadNextAsync().Result;
                return response.FirstOrDefault();
            }
            return default(T);
        }

        public async Task<bool> QueryLikedUser(QueryDefinition query)
        {
            using (var iterator = container.GetItemQueryIterator<bool>(query))
            {
                if (iterator.HasMoreResults)
                {
                    var result = await iterator.ReadNextAsync();
                    return result.FirstOrDefault();
                }
            }
            
            return false;
        }

        public List<T>? Query<T>(QueryDefinition query, QueryRequestOptions requestoptions)
        {
            var result = new List<T>();
            FeedIterator<T> feedIterator = container.GetItemQueryIterator<T>(query, requestOptions: requestoptions);
            while ((requestoptions.MaxItemCount <= 0 || result.Count < requestoptions.MaxItemCount) && feedIterator.HasMoreResults)
            {
                FeedResponse<T> response = feedIterator.ReadNextAsync().Result;
                foreach (var item in response)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Read many items from CosmosDB for single partition
        /// </summary>
        /// <param name="ids">The IDs to use.</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The read list of items.</returns>
        public async Task<Dictionary<string, T>> ReadManyByPartitionId(
    PartitionKey partitionKey, int count,
    CancellationToken cancellation = default)
        {
            var results = new Dictionary<string, T>();

            try
            {
                // Define the query
                var queryDefinition = new QueryDefinition("SELECT * FROM c");
                var queryResultSetIterator = container.GetItemQueryIterator<T>(queryDefinition,
                            requestOptions: new QueryRequestOptions()
                            {
                                MaxItemCount = count,
                                PartitionKey = partitionKey
                            });

                while (queryResultSetIterator.HasMoreResults && !cancellation.IsCancellationRequested)
                {
                    FeedResponse<T> currentResultSet = await queryResultSetIterator.ReadNextAsync(cancellation);
                    foreach (T result in currentResultSet)
                    {
                        results.Add(result.id, result);
                    }
                }
            }
            catch (CosmosException ex)
            {
                logger.LogError(ex, "Error occurred while reading items by partition key.");
                throw; // Optionally, rethrow the exception or handle it based on requirements
            }

            return results;
        }

        /// <summary>
        /// Upsert an item into CosmosDB with concurrency ensured.
        /// </summary>
        /// <param name="document">The item to upsert.</param>
        /// <param name="cancelToken">Cancellation token</param>
        /// <returns>Upsert status</returns>
        protected async Task<bool> UpsertItemWithConcurrencyControlAsync(T document, CancellationToken cancelToken = default)
        {
            var partitionKey = GetPartionKeyFromDocument(document);

            // For workloads that have heavy create payloads, set the EnableContentResponseOnWrite request option to false.
            // The service will no longer return the created or updated resource to the SDK.
            // 
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = document.ETag
            };

            ItemResponse<T> response = await container.UpsertItemAsync(document, partitionKey, requestOptions, cancelToken);
            HttpStatusCode statusCode = response.StatusCode;

            logger.LogDebug("UpsertItemWithConcurrencyControlAsync RU used", document.id, response.RequestCharge);

            if ((statusCode != HttpStatusCode.OK) || (statusCode != HttpStatusCode.Created))
            {
                logger.LogError($"UpsertItemWithConcurrencyControl operation Failed: {response.StatusCode}", document.id, 0);
            }
            return statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Created;
        }


    }
}
