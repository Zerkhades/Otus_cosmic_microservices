# Battle Service

## Overview

Microservice responsible for conducting battles between players:

* **gRPC** stream `battle.BattleSynchronizer.Connect` for bidirectional communication with the Agent Gateway
* Consumes `battle.created` events from Kafka and creates Battle objects using MediatR commands
* Stores battle state in **InMemoryBattleStore** (can be replaced with Redis in the future)
* Publishes `battle.finished` events when battles are completed
* Features automatic battle finishing with the BattleTimerService

## Quick Start

```sh
# Set environment variables
export Kafka__BootstrapServers="localhost:9092"

# Run the service
dotnet run -p src/BattleService/BattleService.csproj
```

gRPC access: `https://localhost:5003` (see proto in `Protos/battle.proto`).

## API Endpoints

- `GET /` - Service health check
- `POST /api/battles/{id}/finish` - Manually finish a battle

## Architecture

- **Domain Model**: Battle and Turn classes define the core entities
- **CQRS**: Commands (StartBattle, SubmitTurn, FinishBattle) process operations
- **Domain Events**: BattleFinishedDomainEvent for internal notifications
- **Kafka Events**: Published for external services to react to battle events
- **Background Services**:
  - KafkaConsumerHostedService: Listens for battle.created events
  - BattleTimerService: Automatically finishes battles after a timeout

## Future Development

1. **Battle Physics/Logic** - Extract to separate service or WASM module
2. **Replays** - Record and store battle replays for later viewing
3. **Persisted Store** - Replace InMemory with Redis or EventStore
4. **Unit Tests** - gRPC + MediatR pipeline (TestServer, xUnit)