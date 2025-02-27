#!/bin/bash

# Elasticsearch Maintenance Script
# This script helps diagnose and fix common Elasticsearch issues
# Usage: ./elasticsearch-maintenance.sh [elasticsearch_url]

# Default Elasticsearch URL
ES_URL=${1:-"http://localhost:9200"}

echo "Elasticsearch Maintenance Tool"
echo "==============================="
echo "Using Elasticsearch URL: $ES_URL"
echo

# Function to check cluster health
check_health() {
  echo "Checking cluster health..."
  curl -s -X GET "$ES_URL/_cluster/health?pretty"
  echo
}

# Function to check disk space
check_disk_space() {
  echo "Checking disk space allocation..."
  curl -s -X GET "$ES_URL/_cat/allocation?v"
  echo
}

# Function to check indices status
check_indices() {
  echo "Checking indices status..."
  curl -s -X GET "$ES_URL/_cat/indices?v"
  echo
}

# Function to check shards status
check_shards() {
  echo "Checking shards status..."
  curl -s -X GET "$ES_URL/_cat/shards?v"
  echo
}

# Function to check if an index is in read-only mode
check_readonly() {
  local index=$1
  echo "Checking if index '$index' is in read-only mode..."
  curl -s -X GET "$ES_URL/$index/_settings?pretty" | grep -A 1 "read_only_allow_delete"
  echo
}

# Function to clear read-only flag for an index
clear_readonly() {
  local index=$1
  echo "Clearing read-only flag for index '$index'..."
  curl -s -X PUT "$ES_URL/$index/_settings" -H 'Content-Type: application/json' -d'
  {
    "index.blocks.read_only_allow_delete": null
  }'
  echo
  echo "Read-only flag cleared. Verifying..."
  check_readonly "$index"
}

# Function to reallocate unassigned shards
reallocate_shards() {
  echo "Reallocating unassigned shards..."
  curl -s -X POST "$ES_URL/_cluster/reroute?retry_failed=true&pretty"
  echo
}

# Function to adjust watermark settings
adjust_watermarks() {
  echo "Adjusting disk watermark settings..."
  curl -s -X PUT "$ES_URL/_cluster/settings" -H 'Content-Type: application/json' -d'
  {
    "persistent": {
      "cluster.routing.allocation.disk.threshold_enabled": true,
      "cluster.routing.allocation.disk.watermark.low": "85%",
      "cluster.routing.allocation.disk.watermark.high": "90%",
      "cluster.routing.allocation.disk.watermark.flood_stage": "95%"
    }
  }'
  echo
  echo "Watermark settings adjusted."
}

# Main menu
while true; do
  echo
  echo "Select an option:"
  echo "1) Check cluster health"
  echo "2) Check disk space allocation"
  echo "3) Check indices status"
  echo "4) Check shards status"
  echo "5) Check if an index is in read-only mode"
  echo "6) Clear read-only flag for an index"
  echo "7) Reallocate unassigned shards"
  echo "8) Adjust disk watermark settings"
  echo "9) Run all diagnostics"
  echo "10) Fix common issues (clear read-only flags and reallocate shards)"
  echo "0) Exit"
  echo
  read -p "Enter your choice: " choice
  
  case $choice in
    1) check_health ;;
    2) check_disk_space ;;
    3) check_indices ;;
    4) check_shards ;;
    5)
      read -p "Enter index name: " index_name
      check_readonly "$index_name"
      ;;
    6)
      read -p "Enter index name: " index_name
      clear_readonly "$index_name"
      ;;
    7) reallocate_shards ;;
    8) adjust_watermarks ;;
    9)
      check_health
      check_disk_space
      check_indices
      check_shards
      ;;
    10)
      echo "Fixing common issues..."
      # Get all indices
      indices=$(curl -s -X GET "$ES_URL/_cat/indices?h=index" | tr -d '[:space:]')
      
      # Clear read-only flag for all indices
      for index in $indices; do
        clear_readonly "$index"
      done
      
      # Reallocate unassigned shards
      reallocate_shards
      
      echo "Common issues fixed. Checking cluster health..."
      check_health
      ;;
    0) 
      echo "Exiting."
      exit 0
      ;;
    *)
      echo "Invalid option. Please try again."
      ;;
  esac
done
