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

This project uses OpenTelemetry for collecting telemetry data and Jaeger for visualizing traces. This helps in monitoring and debugging the system by providing insights into the application's performance and behavior.

### Configuring OpenTelemetry collector

1. Create a configuration file for the OpenTelemetry Collector named `otel-collector-config.yml` in the `mesh` directory. The file should define the receivers, processors, and exporters for the collector. Here is an example configuration:

    ```yaml
    receivers:
      otlp:
        protocols:
          grpc:
          http:

    exporters:
      logging:
        loglevel: debug
      jaeger:
        endpoint: "http://jaeger:14250"
        tls:
          insecure: true

    service:
      pipelines:
        traces:
          receivers: [otlp]
          processors: []
          exporters: [logging, jaeger]
    ```

2. In the `docker-compose.yml` file located in the `mesh` directory, add the OpenTelemetry Collector service. This service should use the `otel/opentelemetry-collector:latest` image and mount the configuration file created in the previous step.

### Starting the services

1. Navigate to the `mesh` directory:
    ```bash
    cd mesh
    ```

2. Pull the latest images (optional but recommended):
    ```bash
    docker compose pull
    ```

3. Build the services:
    ```bash
    docker compose build
    ```

4. Start the services:
    ```bash
    docker compose up --build
    ```

This will start all services, including the OpenTelemetry Collector and Jaeger.

### Viewing traces in Jaeger

1. Open a web browser and navigate to `http://localhost:16686`.
2. Use the Jaeger UI to search for and view traces collected by the OpenTelemetry Collector.

By following these steps, new developers will be able to configure and use Jaeger and OpenTelemetry for tracing and monitoring the system.
