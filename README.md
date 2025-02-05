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

3. Start the services:
   ```bash
   docker compose up --build
   ```

This will build the University.Importer Docker image and start all services. The importer will automatically populate the initial course data.

### Environment Variables

The University.Importer service uses the following environment variables:

- `REPLICATOR_URL`: URL of the Jinaga replicator service (default: http://back-end-replicator:8080)
- `ENVIRONMENT_PUBLIC_KEY`: Public key for the environment (default: default-public-key)

You can override these in the docker-compose.yml file or by setting them in your environment before running docker compose.
