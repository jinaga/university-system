using Nest;
using Serilog;

namespace University.Indexer.Elasticsearch
{
    /// <summary>
    /// Utility methods for Elasticsearch operations
    /// </summary>
    public static class ElasticsearchUtils
    {
        /// <summary>
        /// Checks if an index is in read-only mode and attempts to clear the flag if it is.
        /// </summary>
        /// <param name="client">The Elasticsearch client</param>
        /// <param name="indexName">The name of the index to check</param>
        /// <param name="logger">Logger instance</param>
        /// <returns>True if the index is not in read-only mode or was successfully cleared, false otherwise</returns>
        public static async Task<bool> CheckAndClearReadOnlyFlag(ElasticClient client, string indexName, ILogger logger)
        {
            try
            {
                logger.Debug("Checking if index {IndexName} is in read-only mode", indexName);
                
                // Get the current index settings
                var settingsResponse = await client.GetIndexSettingsAsync(i => i.Index(indexName));
                if (!settingsResponse.IsValid)
                {
                    logger.Error("Failed to get index settings: {Error}", settingsResponse.DebugInformation);
                    return false;
                }
                
                // Check if the index is in read-only mode
                bool isReadOnly = false;
                if (settingsResponse.Indices.TryGetValue(indexName, out var indexSettings))
                {
                    if (indexSettings.Settings.TryGetValue("index.blocks.read_only_allow_delete", out var readOnlySetting))
                    {
                        isReadOnly = readOnlySetting?.ToString()?.ToLowerInvariant() == "true";
                    }
                }
                
                if (!isReadOnly)
                {
                    logger.Debug("Index {IndexName} is not in read-only mode", indexName);
                    return true;
                }
                
                logger.Warning("Index {IndexName} is in read-only mode. Attempting to clear the flag...", indexName);
                
                // Check cluster health to log potential issues
                var healthResponse = await client.ClusterHealthAsync();
                if (healthResponse.IsValid)
                {
                    logger.Information("Cluster health: Status={Status}, UnassignedShards={UnassignedShards}", 
                        healthResponse.Status, healthResponse.UnassignedShards);
                }
                
                // Check allocation to log disk space
                var allocationResponse = await client.CatAllocationAsync();
                if (allocationResponse.IsValid)
                {
                    foreach (var node in allocationResponse.Records)
                    {
                        logger.Information("Node {Node}: Disk used {DiskPercent}%, available {DiskAvailable}", 
                            node.Node, node.DiskPercent, node.DiskAvailable);
                    }
                }
                
                // Attempt to clear the read-only flag
                var updateResponse = await client.UpdateIndexSettingsAsync(indexName, u => u
                    .IndexSettings(s => s
                        .Setting("index.blocks.read_only_allow_delete", null)
                    )
                );
                
                if (updateResponse.IsValid)
                {
                    logger.Information("Successfully cleared read-only flag for index {IndexName}", indexName);
                    return true;
                }
                else
                {
                    logger.Error("Failed to clear read-only flag: {Error}", updateResponse.DebugInformation);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception while checking/clearing read-only flag for index {IndexName}", indexName);
                return false;
            }
        }
        
        /// <summary>
        /// Attempts to reallocate unassigned shards in the cluster.
        /// </summary>
        /// <param name="client">The Elasticsearch client</param>
        /// <param name="logger">Logger instance</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        public static async Task<bool> ReallocateUnassignedShards(ElasticClient client, ILogger logger)
        {
            try
            {
                logger.Information("Attempting to reallocate unassigned shards...");
                
                var rerouteResponse = await client.ClusterRerouteAsync(r => r
                    .RetryFailed(true)
                );
                
                if (rerouteResponse.IsValid)
                {
                    logger.Information("Successfully requested shard reallocation");
                    return true;
                }
                else
                {
                    logger.Error("Failed to reallocate shards: {Error}", rerouteResponse.DebugInformation);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception while reallocating unassigned shards");
                return false;
            }
        }
        
        /// <summary>
        /// Checks the cluster health and logs relevant information.
        /// </summary>
        /// <param name="client">The Elasticsearch client</param>
        /// <param name="logger">Logger instance</param>
        public static async Task LogClusterHealth(ElasticClient client, ILogger logger)
        {
            try
            {
                // Check cluster health
                var healthResponse = await client.ClusterHealthAsync();
                if (healthResponse.IsValid)
                {
                    logger.Information("Cluster health: Status={Status}, ActiveShards={ActiveShards}, UnassignedShards={UnassignedShards}", 
                        healthResponse.Status, healthResponse.ActiveShards, healthResponse.UnassignedShards);
                }
                else
                {
                    logger.Warning("Failed to get cluster health: {Error}", healthResponse.DebugInformation);
                }
                
                // Check indices status
                var indicesResponse = await client.CatIndicesAsync();
                if (indicesResponse.IsValid)
                {
                    foreach (var index in indicesResponse.Records)
                    {
                        logger.Information("Index {Index}: Health={Health}, Status={Status}, DocsCount={DocsCount}", 
                            index.Index, index.Health, index.Status, index.DocsCount);
                    }
                }
                
                // Check allocation status
                var allocationResponse = await client.CatAllocationAsync();
                if (allocationResponse.IsValid)
                {
                    foreach (var node in allocationResponse.Records)
                    {
                        logger.Information("Node {Node}: Shards={Shards}, DiskUsed={DiskUsed}, DiskAvail={DiskAvail}, DiskPercent={DiskPercent}", 
                            node.Node, node.Shards, node.DiskUsed, node.DiskAvailable, node.DiskPercent);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception while logging cluster health");
            }
        }
    }
}
