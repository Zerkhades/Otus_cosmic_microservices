# CosmicBattles Microservices

A microservices-based game backend system built with .NET 9.

## Architecture

The solution consists of the following microservices:

- **PlayerService**: Manages player accounts and profiles
- **TournamentService**: Handles tournament creation and player registration
- **BattleService**: Controls real-time battles via gRPC streams
- **NotificationService**: Delivers real-time notifications via WebSockets

## Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/) (included in Docker Desktop)

## Quick Start

To run the entire solution:

```bash
docker-compose up
```

To run in detached mode:

```bash
docker-compose up -d
```

To view logs for a specific service:

```bash
docker-compose logs -f service-name  # e.g., docker-compose logs -f player-service
```

## Service Endpoints

- PlayerService: http://localhost:5001
- TournamentService: http://localhost:5002
- BattleService: http://localhost:5003
- NotificationService: http://localhost:5004 (WebSocket: ws://localhost:5004/ws/notifications)

## Observability

- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000

## Development

Each service can also be run independently for development:

```bash
# Run just the infrastructure
docker-compose up postgres mongo kafka zookeeper

# Run a specific service
cd src/PlayerService
dotnet run
```

## Stopping Services

To stop all services:

```bash
docker-compose down
```

To stop and remove all data volumes:

```bash
docker-compose down -v
```