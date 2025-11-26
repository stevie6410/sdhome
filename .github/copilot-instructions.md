# SDHome Project - Copilot Context

## Project Overview
SDHome is a home automation system built with ASP.NET Core that integrates with MQTT brokers to handle signals, triggers, and sensor readings. The system stores events in SQL Server and provides a RESTful API with an Angular frontend client.

## Architecture

### Technology Stack
- **.NET 10.0** - Target framework
- **ASP.NET Core Web API** - Backend API
- **SQL Server** - Primary database (with PostgreSQL support)
- **MQTT (Mosquitto)** - Message broker for IoT devices
- **MQTTnet** - MQTT client library
- **NSwag** - OpenAPI/Swagger spec generation and TypeScript client generation
- **Angular** - Frontend application
- **Docker** - Container orchestration with docker-compose
- **MediatR** - Mediator pattern implementation
- **Grafana & Prometheus** - Monitoring and metrics

### Project Structure
```
sdhome/
├── src/
│   ├── SDHome.Api/              # ASP.NET Core Web API
│   │   ├── Controllers/         # API controllers
│   │   ├── Program.cs           # Application entry point and DI setup
│   │   ├── nswag.json           # NSwag configuration
│   │   └── appsettings*.json    # Configuration files
│   ├── SDHome.Lib/              # Core business logic library
│   │   ├── Data/                # Repository implementations
│   │   ├── Services/            # Business services
│   │   ├── Models/              # Domain models
│   │   └── Mappers/             # Object mapping
│   └── ClientApp/               # Angular frontend
│       └── src/app/api/         # Generated TypeScript client
├── config/                      # Docker service configurations
│   ├── mosquitto/               # MQTT broker config
│   ├── prometheus/              # Metrics config
│   ├── grafana/                 # Dashboard config
│   └── zigbee/                  # Zigbee integration config
├── docker-compose.yml           # Docker services definition
└── .github/                     # GitHub configuration
```

## Core Components

### API Controllers
- **SignalsController** - Handles signal event queries (uses `ISignalQueryService`)
- **ReadingsController** - Manages sensor readings (uses `ISensorReadingsRepository`)
- **TriggersController** - Manages trigger events (uses `ITriggerEventsRepository`)

### Repositories (Data Layer)
All repositories follow the interface/implementation pattern:
- **ISignalEventsRepository** → `SqlServerSignalEventsRepository`, `PostgresSignalEventsRepository`
- **ISensorReadingsRepository** → `SqlServerSensorReadingsRepository`
- **ITriggerEventsRepository** → `SqlServerTriggerEventsRepository`

Repositories are registered as **singletons** with connection string injection.

### Services (Business Logic)
- **ISignalsService** → `SignalsService` - Core signal processing logic
- **ISignalQueryService** → `SignalQueryService` - Query service for signal events
- **ISignalEventProjectionService** → `SignalEventProjectionService` - Projects signal events into triggers and readings
- **SignalsMqttWorker** - Background service (IHostedService) that subscribes to MQTT topics

### Configuration Models
Configuration is bound from `appsettings.json` using the Options pattern:
- **MqttOptions** - MQTT broker settings
- **PostgresOptions** - PostgreSQL connection settings
- **MsSQLOptions** - SQL Server connection settings
- **WebhookOptions** - Webhook endpoint URLs
- **LoggingOptions** - Seq logging configuration
- **MetricsOptions** - Prometheus metrics settings

## Important Patterns & Conventions

### Dependency Injection
- **Repositories**: Singleton lifetime (single connection string)
- **Services**: Singleton for stateless services, Scoped for stateful
- **Background Workers**: Registered as IHostedService
- **Primary constructor injection** used throughout (e.g., `public class SignalsController(ISignalQueryService queryService)`)

### Environment-Specific Behavior
The application uses multiple environments:
- **Development** - Normal dev environment with full database and MQTT
- **Production** - Production settings
- **NSwag** - Special environment for OpenAPI generation

**Critical**: When running in "NSwag" environment:
- Database initialization is **skipped** (no `EnsureCreatedAsync` call)
- Background services like `SignalsMqttWorker` are **not registered**
- Uses `appsettings.NSwag.json` with empty connection strings
- This prevents NSwag from attempting database/MQTT connections during code generation

### NSwag Configuration
- **Build Integration**: NSwag runs as a post-build step in Debug configuration
- **Output Files**: 
  - OpenAPI spec: `bin/Debug/net8.0/swagger.json`
  - TypeScript client: `../ClientApp/src/app/api/sdhome-client.ts`
- **Client Template**: Angular with HttpClient, RxJS 7.0
- **Important**: `nswag.json` must have `"aspNetCoreEnvironment": "NSwag"` to prevent database connections

### Database Schema
The application uses SQL Server with three main tables:
- **signal_events** - Raw signal events from MQTT
- **trigger_events** - Triggered events derived from signals
- **sensor_readings** - Sensor data readings

Schema is created automatically on startup via `SqlServerSignalEventsRepository.EnsureCreatedAsync()`.

## Configuration Files

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=signals;..."
  },
  "Signals": {
    "Mqtt": { "Host": "...", "Port": 1883, "TopicFilter": "sdhome/#" },
    "MSSQL": { "ConnectionString": "..." },
    "Webhooks": { "Main": "http://..." }
  }
}
```

### appsettings.NSwag.json
Empty connection strings to prevent connections during OpenAPI generation.

## Docker Services
The `docker-compose.yml` defines:
- **mosquitto** - MQTT broker (port 1883)
- **mosquitto-mc** - Management Center UI
- **zigbee2mqtt** - Zigbee device integration
- **prometheus** - Metrics collection
- **grafana** - Dashboards and visualization
- **mssql** - SQL Server database
- **n8n** - Workflow automation (for webhooks)
- **seq** - Log aggregation

## Build & Run

### Build Commands
```powershell
dotnet build                                    # Build solution
dotnet build src/SDHome.Api/SDHome.Api.csproj  # Build API only
dotnet clean                                    # Clean build artifacts
```

### Important Build Notes
- NSwag runs automatically during Debug builds
- If NSwag fails, check that:
  1. `nswag.json` has `"aspNetCoreEnvironment": "NSwag"`
  2. `appsettings.NSwag.json` exists with empty connection strings
  3. Background services are conditionally registered

### Docker
```bash
docker-compose up -d      # Start all services
docker-compose down       # Stop all services
```

## Common Issues & Solutions

### NSwag Build Failures
**Symptom**: "No service for type 'SqlServerSignalEventsRepository' has been registered"

**Solution**: 
1. Ensure `SqlServerSignalEventsRepository` is registered both as interface and concrete type
2. Check `nswag.json` uses `"aspNetCoreEnvironment": "NSwag"`
3. Verify background services are skipped in NSwag environment

### Null Connection String Warnings
**Solution**: Use `?? string.Empty` when retrieving connection strings

### Database Connection During Build
**Solution**: Check that `app.Environment.IsEnvironment("NSwag")` guards database initialization

## API Endpoints
- `/swagger` - Swagger UI
- `/api/signals` - Signal event queries
- `/api/readings` - Sensor readings
- `/api/triggers` - Trigger events

## Development Workflow
1. Make code changes in `src/SDHome.Api` or `src/SDHome.Lib`
2. Build triggers NSwag to regenerate TypeScript client
3. Frontend gets updated API client in `ClientApp/src/app/api/sdhome-client.ts`
4. MQTT messages on `sdhome/#` are processed by `SignalsMqttWorker`
5. Events stored in SQL Server and projected to triggers/readings

## Key Dependencies
- NSwag.AspNetCore 14.6.3
- MediatR 13.1.0
- Swashbuckle.AspNetCore 10.0.1
- Microsoft.AspNetCore.OpenApi 10.0.0

## Notes for AI Assistance
- Always check environment context before suggesting database operations
- Remember that repositories use primary constructors with connection strings
- NSwag environment is critical for build process - don't suggest removing it
- Background services must be conditionally registered
- Connection strings should handle null cases with `?? string.Empty`
