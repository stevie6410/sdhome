# GitHub Copilot Instructions - SDHome Signals Project

## Project Overview

**SDHome Signals** is a .NET-based smart home automation backend that processes IoT sensor data from MQTT messages, stores events in SQL Server using Entity Framework Core, and exposes a REST API for querying signal events, sensor readings, and trigger events.

## Architecture

### Solution Structure

```
signals/
├── src/
│   ├── SDHome.Api/          # ASP.NET Core Web API (.NET 10)
│   ├── SDHome.Lib/          # Shared library (models, services, data access)
│   └── ClientApp/           # Frontend client application (Angular)
├── config/                  # Configuration files for infrastructure services
│   ├── grafana/             # Grafana datasource configuration
│   ├── mosquitto/           # MQTT broker configuration
│   ├── prometheus/          # Prometheus metrics configuration
│   └── zigbee/              # Zigbee2MQTT configuration
├── docker-compose.yml       # Full stack orchestration
└── dockerfile               # API container build
```

### Technology Stack

- **Runtime**: .NET 10 (Preview)
- **Web Framework**: ASP.NET Core with Controllers
- **ORM**: Entity Framework Core 10 with SQL Server
- **Database**: SQL Server
- **Message Broker**: Eclipse Mosquitto (MQTT)
- **Logging**: Serilog with Seq sink
- **Metrics**: Prometheus + Grafana
- **API Documentation**: NSwag (OpenAPI/Swagger)
- **Containerization**: Docker & Docker Compose

### Core Components

#### SDHome.Api
- REST API exposing endpoints for signals, readings, triggers, and devices
- Controllers inject `SignalsDbContext` directly (no repository pattern)
- Health check endpoints: `/health`, `/health/ready`, `/health/live`
- OpenAPI documentation via NSwag (auto-generates TypeScript client)

#### SDHome.Lib
- **Data/Entities**: EF Core entity classes (`SignalEventEntity`, `SensorReadingEntity`, `TriggerEventEntity`, `DeviceEntity`)
- **Data/SignalsDbContext**: EF Core DbContext with Fluent API configuration
- **Models**: Domain records (`SignalEvent`, `SensorReading`, `TriggerEvent`, `Device`)
- **Services**: 
  - `SignalsMqttWorker` - Background service subscribing to MQTT topics
  - `SignalsService` - Core business logic for processing MQTT messages
  - `SignalEventProjectionService` - Projects signal events to triggers/readings
  - `DeviceService` - Device management and Zigbee2MQTT sync

## Key Patterns & Conventions

### Data Access (EF Core Direct)
- Controllers and services inject `SignalsDbContext` directly
- No repository pattern - EF Core is the repository
- Use `AsNoTracking()` for read-only queries
- Entity classes have `ToModel()` and `FromModel()` methods for conversion

### Dependency Injection
- Use primary constructors for DI
- Services registered as Scoped (same lifetime as DbContext)
- `IServiceScopeFactory` used in background workers for scoped services

### Configuration
- Configuration sections: `Signals:Mqtt`, `Signals:MSSQL`, `Signals:Webhooks`
- Connection strings in `ConnectionStrings:DefaultConnection`
- Environment-specific settings via `ASPNETCORE_ENVIRONMENT`

### Data Models
- **Entities** (EF Core): Mutable classes in `Data/Entities/`
- **Models** (Domain): Immutable records in `Models/`
- Entities convert to/from domain models via static methods

### API Conventions
- Route prefix: `/api/{resource}`
- Use `[FromQuery]` for optional parameters with defaults
- Return `List<T>` for collection endpoints
- Use async/await throughout

## Infrastructure Services (Docker Compose)

| Service | Port | Purpose |
|---------|------|---------|
| mosquitto | 1883 | MQTT broker for IoT messages |
| mosquitto-mc | 8088 | Mosquitto Management Center UI |
| seq | 5341 | Structured logging server |
| prometheus | 9090 | Metrics collection |
| grafana | 3000 | Metrics visualization |
| signals | 8090 | Main API application |
| zigbee2mqtt | 8080 | Zigbee device gateway |
| n8n | 5678 | Workflow automation |

## MQTT Topics

- Topic filter: `sdhome/#`
- Messages are processed by `SignalsMqttWorker` background service
- Payloads are mapped to `SignalEvent` via `ISignalEventMapper`

## Development Guidelines

### When Adding New Features
1. Define entity class in `SDHome.Lib/Data/Entities/`
2. Add DbSet to `SignalsDbContext`
3. Configure entity in `OnModelCreating()`
4. Add domain model record in `SDHome.Lib/Models/`
5. Add service logic or expose via controller

### Database Migrations
```bash
cd src/SDHome.Api
dotnet ef migrations add <MigrationName> --project ../SDHome.Lib
dotnet ef database update
```

### API Documentation
- NSwag generates OpenAPI spec on debug builds
- TypeScript client generated to `ClientApp/src/app/api/sdhome-client.ts`

## Code Style

- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Use implicit usings (`<ImplicitUsings>enable</ImplicitUsings>`)
- Primary constructors for DI in controllers and services
- Async methods suffixed with `Async`
- File-scoped namespaces

## Running the Project

### Local Development
```bash
# Start infrastructure
docker-compose up -d mosquitto seq prometheus grafana

# Run API
cd src/SDHome.Api
dotnet run
```

### Full Stack (Docker)
```bash
docker-compose up -d
```

### API Endpoints
- Swagger UI: `http://localhost:8090/swagger`
- Health: `GET /health`
- Signals: `GET /api/signals/logs?take=100`
- Readings: `GET /api/readings?take=100`
- Triggers: `GET /api/triggers?take=100`

## Environment Variables (Docker)

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Environment name (Docker, Development) |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Signals__Mqtt__Host` | MQTT broker hostname |
| `Signals__Mqtt__Port` | MQTT broker port |
| `Logging__SeqUrl` | Seq logging server URL |
