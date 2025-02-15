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

### Starting the Mesh

1. Navigate to the mesh directory:
   ```bash
   cd mesh
   ```

2. Set your environment public key (optional - defaults to "default-public-key"):
   ```bash
   export ENVIRONMENT_PUBLIC_KEY=your-public-key
   ```

3. Pull the latest images (optional but recommended):
   ```bash
   docker compose pull
   ```

4. Build the services:
   ```bash
   docker compose build
   ```

5. Start the services:
   ```bash
   docker compose up --build
   ```

This will build the University.Importer and University.Indexer Docker images and start all services. The importer will automatically populate the initial course data. The indexer will update the Elasticsearch index.

If you make any changes to source code, please remember to run `docker compose build`. Then use `docker compose down` and `docker compose up` to create new containers for those rebuilt images.

### Environment Variables

The University.Importer service uses the following environment variables:

- `REPLICATOR_URL`: URL of the Jinaga replicator service (default: http://back-end-replicator:8080)
- `ENVIRONMENT_PUBLIC_KEY`: Public key for the environment (default: default-public-key)

You can override these in the docker-compose.yml file or by setting them in your environment before running docker compose.

## Telemetry and tracing

This project uses OpenTelemetry for collecting telemetry data and Jaeger for visualizing traces, Grafana and Loki for log aggregation and visualization, and Prometheus for analyzing metrics. This helps in monitoring and debugging the system by providing insights into the application's performance and behavior.

### Viewing traces in Jaeger

1. Open a web browser and navigate to `http://localhost:16686`.
2. Use the Jaeger UI to search for and view traces collected by the OpenTelemetry Collector.

### Viewing Logs in Grafana

After starting the services with `docker compose up`, follow these steps to view the logs:

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

3. View and Query Logs:
   - Click on "Explore" in the left sidebar
   - Select "Loki" as the data source
   - Use LogQL queries to filter and view logs:
     * `{container="university-mesh-importer-1"}` - View Importer service logs
     * `{container="university-mesh-indexer-1"}` - View Indexer service logs
   - Click "Run query" to see the results
