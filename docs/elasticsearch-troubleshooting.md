# Elasticsearch Troubleshooting Guide

This guide provides information on how to diagnose and fix common Elasticsearch issues, particularly the "read-only / allow delete" error that can occur during bulk indexing operations.

## Common Issues

### Read-Only Index Error

**Error Message:**
```
Failed to index course [ID]: blocked by: [FORBIDDEN/12/index read-only / allow delete (api)]
```

This error occurs when Elasticsearch has set an index to read-only mode. This can happen for several reasons:

1. **Low Disk Space**: Elasticsearch automatically sets indices to read-only when disk space falls below certain thresholds.
2. **Cluster Health Issues**: Problems with cluster state or node communication.
3. **Manual Configuration**: Someone explicitly set the index to read-only.
4. **Index Lifecycle Management**: An ILM policy transitioned the index to a read-only state.

## Diagnosing the Issue

### 1. Check Index Settings

```bash
curl -X GET "http://localhost:9200/offerings/_settings?pretty"
```

Look for `"index.blocks.read_only_allow_delete": "true"` in the response.

### 2. Check Cluster Health

```bash
curl -X GET "http://localhost:9200/_cluster/health?pretty"
```

A "yellow" status is normal for a single-node cluster with replicas, as replicas cannot be assigned to the same node as their primary shards.

### 3. Check Disk Space

```bash
curl -X GET "http://localhost:9200/_cat/allocation?v"
```

If disk usage is high (>85%), this might be causing the read-only issue.

### 4. Check Shards Status

```bash
curl -X GET "http://localhost:9200/_cat/shards?v"
```

Look for unassigned shards that might indicate cluster issues.

## Fixing the Issue

### 1. Clear Read-Only Flag

```bash
curl -X PUT "http://localhost:9200/offerings/_settings" -H 'Content-Type: application/json' -d'
{
  "index.blocks.read_only_allow_delete": null
}'
```

### 2. Reallocate Unassigned Shards

```bash
curl -X POST "http://localhost:9200/_cluster/reroute?retry_failed=true&pretty"
```

### 3. Adjust Watermark Settings (if disk space is an issue)

```bash
curl -X PUT "http://localhost:9200/_cluster/settings" -H 'Content-Type: application/json' -d'
{
  "persistent": {
    "cluster.routing.allocation.disk.threshold_enabled": true,
    "cluster.routing.allocation.disk.watermark.low": "85%",
    "cluster.routing.allocation.disk.watermark.high": "90%",
    "cluster.routing.allocation.disk.watermark.flood_stage": "95%"
  }
}'
```

## Using the Maintenance Script

We've provided a maintenance script to help diagnose and fix common Elasticsearch issues:

```bash
./scripts/elasticsearch-maintenance.sh [elasticsearch_url]
```

The script provides a menu-driven interface to:
- Check cluster health
- Check disk space allocation
- Check indices status
- Check shards status
- Check if an index is in read-only mode
- Clear read-only flag for an index
- Reallocate unassigned shards
- Adjust disk watermark settings
- Run all diagnostics
- Fix common issues (clear read-only flags and reallocate shards)

## Automatic Recovery in Code

The application now includes automatic detection and recovery from read-only index issues. The `ElasticsearchUtils` class provides methods to:

1. Check and clear read-only flags
2. Reallocate unassigned shards
3. Log cluster health information

These methods are automatically called before bulk indexing operations to prevent failures due to read-only indices.

## Monitoring Elasticsearch

To proactively monitor Elasticsearch and prevent issues:

1. **Set up disk space alerts**: Configure alerts when disk usage exceeds 80%.
2. **Monitor cluster health**: Set up regular checks for cluster status.
3. **Check unassigned shards**: Regularly verify that all shards are properly assigned.
4. **Review logs**: Look for warnings about disk space or read-only indices.

## Additional Resources

- [Elasticsearch Disk-Based Shard Allocation](https://www.elastic.co/guide/en/elasticsearch/reference/current/disk-allocator.html)
- [Elasticsearch Cluster Health API](https://www.elastic.co/guide/en/elasticsearch/reference/current/cluster-health.html)
- [Elasticsearch Index Blocks](https://www.elastic.co/guide/en/elasticsearch/reference/current/index-modules-blocks.html)
