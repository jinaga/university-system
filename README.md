# University System Example

This example demonstrates how Jinaga could be used to model and run a university system. It shows how replication moves data to a client or service as needed. Even though the model is simplified, it contains enough complexity to show the principles of security and distribution within an immutable runtime.

## Model

Begin with a detailed description of the model. See the [University System Model](./notebooks/UniversityModel.ipynb) Polyglot Notebook. To open this notebook in Visual Studio Code, install the [Polyglot Notebook](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode) extension.

## Running the System

The system consists of several components running in Docker containers:

- **University.Model**: Core domain models
- **University.Importer**: Service for importing initial course data
- **Back-end Replicator**: Jinaga replication service
- **Elasticsearch**: Search and indexing service

### Prerequisites

- Docker and Docker Compose
- .NET 9.0 SDK (for local development)
- [Graphviz](https://graphviz.org) (for generating diagrams in the notebook)

### Starting the Mesh

1. Navigate to the mesh directory:
   ```bash
   cd mesh
   ```

2. Pull the latest images (optional but recommended):
   ```bash
   docker compose pull
   ```

3. Build and start the services:
   ```bash
   docker compose up -d --build
   ```

This will build the University.Importer and University.Indexer Docker images and start all services.

If you make any changes to source code, just run `docker compose up -d --build` again to rebuild and restart the services.

### Running the Demo

The demo shows two processes communicating via a replicator. One process is the importer, which reads course data from a CSV file and writes it to the replicator. The other process is the indexer, which receives course data from the replicator and writes it to Elasticsearch.

1. Open a web browser and navigate to `http://localhost:9200/_cat/indices?v` to view the Elasticsearch indices. You should see an index named `offerings`.
2. Open a web browser and navigate to `http://localhost:9200/offerings/_search` to view the offerings in the index. At first, you will see an empty list.
3. Expand the `data` directory in Visual Studio Code, and then the `mesh`, `data`, and subdirectories. You will see the `university_data.csv` file.
4. Drag the `university_data.csv` file from `data` to `import`. It will be imported and moved to the `processed` directory.
5. Refresh the Elasticsearch index page. You should see the offerings in the index.

Use Jaeger, Grafana, and Prometheus to view traces, logs, and metrics.

## Telemetry and tracing

This project uses OpenTelemetry for collecting telemetry data and Jaeger for visualizing traces, Grafana and Loki for log aggregation and visualization, and Prometheus for analyzing metrics. This helps in monitoring and debugging the system by providing insights into the application's performance and behavior.

### Viewing traces in Jaeger

1. Open a web browser and navigate to `http://localhost:16686`.
2. Use the Jaeger UI to search for and view traces collected by the OpenTelemetry Collector.

### Viewing Logs in Grafana

After starting the services with `docker compose up -v --build`, follow these steps to view the logs:

1. Access Grafana:
   - Open a web browser and navigate to `http://localhost:3000`
   - Log in with the default credentials:
     * Username: admin
     * Password: admin
   - You may be prompted to change the password, which you can skip for now

2. Configure Loki Data Source:
   - Click on the Connections (overlapping circles) icon in the left sidebar
   - Select "Data sources"
   - Click "Add data source"
   - Search for and select "Loki"
   - Set the following configuration:
     * URL: `http://loki:3100`
     * Leave Authentication set to "No Authentication"
     * Leave other settings at their defaults
   - Click "Save & test" to verify the connection

3. View Logs:
   - Expand the "Explore" menu in the left sidebar
   - Click on "Logs" under "Explore"
   - Select "Loki" as the data source if it is not already selected
   - View the logs by service

### Viewing metrics in Grafana

1. Add the Prometheus data source to Grafana.
   - Open a web browser and navigate to `http://localhost:3000`.
   - Log in with the default credentials:
     * Username: admin
     * Password: admin
   - You may be prompted to change the password, which you can skip for now.
   - Click on the "Configuration" gear icon in the left sidebar.
   - Select "Data Sources".
   - Click "Add data source".
   - Search for and select "Prometheus".
   - Set the following configuration:
     * URL: `http://prometheus:9090`
     * Leave other settings at their defaults.
   - Click "Save & test" to verify the connection.
2. Explore metrics.
   - Expand the "Explore" menu in the left sidebar.
   - Click on "Metrics" under "Explore".
   - Click "Select" on one of the graphs to configure it.
   - Click on the compass icon labeled "Open in explore".
   - In the new tab, copy the query for use in the dashboard.
3. Build a dashboard.
   - Expand the "Create" menu in the left sidebar.
   - Click on "Dashboard" under "Create".
   - Click on "Add new panel".
   - Switch to the "Code" tab to use one of the example queries.

#### Example Queries

Use the following queries to check throughput:
- Files processed by the Importer:
  ```prometheus
  sum(rate(files_processed_total{}[$__rate_interval]) )
  ```
- Rows processed by the Importer:
  ```prometheus
  sum(rate(rows_processed_total{}[$__rate_interval]) )
  ```
- Offerings indexed by the Indexer:
  ```prometheus
  sum(rate(offerings_indexed_total{}[$__rate_interval]) )
  ```
- Facts saved by the Replicator:
  ```prometheus
  sum(rate(facts_saved_total{}[$__rate_interval]) )
  ```
- Facts loaded by the Replicator:
  ```prometheus
  sum(rate(facts_loaded_total{}[$__rate_interval]) )
  ```
- Warnings in the Replicator:
  ```loki
  {service_name="back-end-replicator"} | json | logfmt | drop __error__, __error_details__ | severity="WARN"
  ```

These queries show the rate of files, rows, offerings, facts saved, and facts loaded over the selected interval.