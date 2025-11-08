# High Level Design for Azure SRE AI Agent

This document presents the high-level architecture of the Azure SRE AI Agent, as described in `Design-doc.md`.

## 1. Admin UI for Resource Selection

- Hosted on Azure Static Web Apps.
- Provides authenticated access for administrators.
- Displays a list of Azure resources using read-only permissions.
- Enables admins to select, add, or modify resources for AI agent monitoring.
- Creates a managed identity with specific permissions for resource access.

## 2. Metrics and Configuration Collection

The system collects metrics and configurations at defined intervals to ensure efficient monitoring and analysis.

### a. Metrics Collection

- Collects metrics every minute using Azure Diagnostics Logs.
- Utilizes an Azure Log Analytics Workspace, provisioned via ARM templates.
- Employs managed identities to configure diagnostics and associate resources with the workspace.
- Retrieves metrics using the Azure Log Analytics client and Azure Resource SDK.

### b. Configuration and Recommendations

- Gathers resource configurations through Azure SDKs and NuGet packages.
- Performs configuration checks every six hours.
- Obtains advisory recommendations to enhance resource reliability and performance.

## 3. Metrics Storage in Azure Data Lake

- Stores per-minute resource metrics in Azure Data Lake as Parquet files.
- Creates a storage account with a hierarchical namespace and assigns the Storage Account Contributor role to the managed identity.
- Aggregates metrics into Parquet files every 10 minutes, embedding resource data for efficient processing.
- Uploads the Parquet files to Azure Data Lake for long-term storage and analytics.

## 4. Embedding Creation Using Azure OpenAI

- Converts resource metrics into text strings and generates embeddings using the `text-embedding-3-small` model from Azure OpenAI.
- For the proof of concept (POC), API authentication is used, while production will leverage Entra ID credentials.
- Transforms the embeddings (float arrays) into strings and stores them in the same Parquet files.

## 5. Data Storage in Azure Data Lake and Cosmos DB

### Azure Data Lake

- Serves as the primary storage for all metrics and historical data.
- Feeds data into Azure Synapse for advanced analytics and Power BI dashboards.
- Stores comprehensive 8x8 resource metric combinations for detailed analysis.

### Cosmos DB

- Optimized for fast retrieval of specific data required for Retrieval-Augmented Generation (RAG) processes.
- Stores only essential data, such as embeddings and resource metadata, for quick access.

### Why Separate Data Sources?

- **Azure Data Lake**: Designed for large-scale storage and analytics, supporting historical data and integration with analytical tools like Azure Synapse.
- **Cosmos DB**: Provides low-latency access to critical data, enabling efficient RAG operations and real-time insights.

By leveraging both Azure Data Lake and Cosmos DB, the system achieves a balance between scalable storage and fast data retrieval, ensuring optimal performance for monitoring and AI-driven insights.
| Service             | Role                              | Strength                                         |
| ------------------- | --------------------------------- | ------------------------------------------------ |
| **Azure Data Lake** | Historical, large-scale analytics | Scalable, cost-efficient, queryable with Synapse |
| **Cosmos DB**       | Real-time data and RAG queries    | Low latency, ideal for AI reasoning & insights   |


### Cosmos DB Creation
Cosmos DB is used to store the embeddings, enable vector embedding
path - /embedding
Float 32 cosine 
1536 dimension
#### Cost estimation for cosmos db embedding
Lets assume there are 8 resources and from each resource we have 8 metrics for each minute
there is a one embedding per resource
Documents/Month: 8 docs/min × 60 min/hour × 24 hours/day × 30 days = 345,600 documents.
RU for Inserts: 345,600 × 80 RU = 27.648 million RU.
RU for Queries: 30,000 × 50 RU = 1.5 million RU.
Total RU: ~28.8 million RU.

Use serverless - as there is no requirement of RU/s. 
Estimated cost - 7-10 dollars/month

