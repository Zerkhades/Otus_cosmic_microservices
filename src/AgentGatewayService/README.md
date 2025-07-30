# Agent Gateway Service

## Overview

The Agent Gateway Service acts as an intermediary between game clients (agents) and the Battle Service. It provides:

- WebSocket connections for game clients via SignalR
- gRPC client connection to the Battle Service
- Bridge between player WebSocket connections and battle gRPC streams
- API endpoints for battle management

## Architecture

```
Game Client <--[WebSocket/SignalR]--> AgentGatewayService <--[gRPC]--> BattleService
```

The service translates between two communication protocols:
- **SignalR/WebSockets**: For real-time communication with game clients
- **gRPC**: For efficient, bidirectional streaming with the Battle Service

## Features

- **Real-time communication** with game clients via SignalR
- **Bidirectional streaming** with Battle Service via gRPC
- **Battle management** REST API
- **Event publishing** to Kafka for system integration

## API Endpoints

### WebSocket (SignalR)

- **Hub**: `/hubs/agent`
- **Connection Parameters**:
  - `battleId`: GUID of the battle
  - `playerId`: GUID of the player

### REST API

- **POST `/api/battles`**: Create a new battle
- **POST `/api/battles/{id}/connect`**: Connect to an existing battle
- **POST `/api/battles/{id}/finish`**: Request to finish a battle

## Client Usage

```javascript
// Connect to SignalR hub
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`/hubs/agent?battleId=${battleId}&playerId=${playerId}`)
  .build();

// Handle server updates
connection.on("ReceiveUpdate", (update) => {
  console.log(`Received update for tick ${update.tick}`);
  // Process game state update
});

// Submit player turn
connection.invoke("SubmitTurn", JSON.stringify({
  tick: 42,
  payload: [1, 2, 3, 4] // Turn data
}));
```

## Quick Start

```sh
# Set environment variables
export BattleService__Grpc="http://battle-service:5003"
export Kafka__BootstrapServers="kafka:9092"

# Run the service
dotnet run -p src/AgentGatewayService/AgentGatewayService.csproj
```

The service will be available at: http://localhost:5005