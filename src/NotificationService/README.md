# Notification Service

## Overview

The Notification Service provides real-time notifications to players through WebSocket connections using SignalR. It consumes events from Kafka topics and pushes them to connected clients.

## Features

- **Real-time notifications** via SignalR WebSocket connections
- **Persistent storage** of notifications in PostgreSQL database
- **Kafka integration** for consuming events from other microservices
- **Outbox pattern** ensuring notification delivery reliability
- **REST API** for retrieving unread notifications

## User Flow

1. Client connects to WebSocket endpoint (`/ws/notifications`) with authentication
2. SignalR matches the `UserIdentifier` (from JWT claims or query parameter) and adds the connection to a user group
3. Kafka consumer listens for events (`battle.finished`, `tournament.*`, `notification.player`)
4. When an event is received, it's stored in the database and pushed to the appropriate user group
5. Client displays the notification to the user

## Quick Start

```sh
# Set environment variables
export CONNECTIONSTRINGS__DEFAULT="Host=localhost;Database=notifications;Username=postgres;Password=pass"
export Kafka__BootstrapServers="localhost:9092"
export AllowedOrigins__0="http://localhost:5173"

# Run the service
dotnet run -p src/NotificationService/NotificationService.csproj
```

## API Endpoints

- **WebSocket**: `/ws/notifications` - SignalR hub for real-time notifications
- **GET** `/api/notifications/unread/{userId}` - Retrieve unread notifications for a user
- **POST** `/api/notifications` - Manually enqueue a notification

## Kafka Topics

The service subscribes to the following Kafka topics:
- `notification.player` - Direct notifications to a specific player
- `battle.finished` - Battle completion notifications
- `tournament.registration.accepted` - Successful tournament registrations
- `tournament.registration.rejected` - Failed tournament registrations

## Future Enhancements

- **Multiple channels** - Add support for Email and Push notification channels
- **Notification preferences** - Allow users to configure notification preferences
- **Read/unread status** - Add APIs for marking notifications as read
- **Batch processing** - Add background service to process notification batches