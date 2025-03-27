# University System - Developer Guidelines

## Build Commands
- Build solution: `dotnet build ../University.sln`
- Build project: `dotnet build`
- Run project: `dotnet run`
- Publish: `dotnet publish -c Release`

## Code Style Guidelines
- **Imports**: Group imports by category (System, Jinaga, Local), sorted alphabetically within groups
- **Naming**: PascalCase for types/methods, camelCase for variables/parameters
- **Nullable**: Enable nullable reference types with proper annotations (`?` suffix)
- **Error Handling**: Use structured exception handling with logging, prefer async/await pattern
- **Types**: Use explicit types over var when type isn't obvious from assignment
- **Services**: Implement IService interface with Start/Stop methods
- **Dependency Injection**: Constructor injection for dependencies
- **Documentation**: XML documentation for public APIs
- **Testing**: Follow AAA pattern (Arrange, Act, Assert)
- **Logging**: Use Serilog with structured logging
- **Telemetry**: Implement OpenTelemetry for tracing and metrics