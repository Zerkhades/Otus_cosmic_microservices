# Tournament Service

## Overview
A microservice that manages tournament creation, player registration, and retrieval of tournament information. Data is stored in MongoDB, and asynchronous events are handled using Kafka.

## Quick Start

```sh
# Set environment variables
export Mongo__ConnectionString="mongodb://localhost:27017"
export Kafka__BootstrapServers="localhost:9092"

# Run the service
dotnet run -p src/TournamentService/TournamentService.csproj
```

Swagger UI is available at: https://localhost:5001/swagger when running in Development environment.

## Features

- **Tournament Management**: Create tournaments with custom rules
- **Player Registration**: Register players for tournaments with validation
- **Event Publishing**: Publishes events to Kafka:
  - `tournament.created`: When a new tournament is created
  - `tournament.registration.accepted`: When player registration succeeds
  - `tournament.registration.rejected`: When player registration fails

## API Endpoints

- `POST /tournaments` - Create a new tournament
- `POST /tournaments/{id}/register` - Register a player for a tournament
- `GET /tournaments/upcoming` - List upcoming tournaments
- `GET /tournaments/{id}` - Get tournament details

## Future Enhancements

- Add registration deadline constraints
- Implement a scheduler to automatically update tournament status
- Add comprehensive unit tests with xUnit and Mongo2Go