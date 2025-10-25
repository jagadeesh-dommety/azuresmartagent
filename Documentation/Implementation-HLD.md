# High Level Design for Azure SRE AI Agent

This document presents the high-level architecture of the Azure SRE AI Agent, as described in `Design-doc.md`.

## 1. Admin UI for Resource Selection

- Hosted on Azure Static Web Apps.
- Provides authenticated access for administrators.
- Displays a list of Azure resources using read-only permissions.
- Enables admins to select, add, or modify resources for AI agent monitoring.
- Create a managed identity specific to resource access with necessary permissions

## 2. Metrics and Configuration Collection

Collect metrics for every 1 minute at a interval of 10 minutes and for configs and recommendations once in 6 hours

### a. Metrics Collection

- Collects metrics every minute via Azure Diagnostics Logs.
- Utilizes an Azure Log Analytics Workspace, provisioned with ARM templates.
- Employs managed identities to configure diagnostics and associate resources with the workspace.
- Retrieves metrics using Azure Log Analytics client and Azure Resource SDK.

### b. Configuration and Recommendations

- Gathers resource configurations through Azure SDKs and NuGet packages.
- Performs configuration checks at six-hour intervals.
- Obtains advisory recommendations to improve resource reliability and performance.

## 3. Write the metrics as parquet to Azure datalake

The per minute resource metrics for each resource will be pushed to azure data lake
Create a storage account with hierarchial namespace Azure Data lake and provide the access to managed identity. 
Storage Account Contributor role assignment

Create a parqet file for each 10 minute where having list of embeddings for all the resources

Upload the file to azure data lake

