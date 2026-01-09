# GameServer - WebSocket Game Server

A production-grade, distributed-ready game server built with .NET 8 using raw WebSockets.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        GameServer.Api                           │
│  ┌─────────────────┐  ┌──────────────────┐  ┌───────────────┐  │
│  │ WebSocket       │  │ Message          │  │ /health       │  │
│  │ Middleware      │──│ Dispatcher       │  │ Endpoint      │  │
│  └────────┬────────┘  └────────┬─────────┘  └───────────────┘  │
│           │                    │                                │
│           ▼                    ▼                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              GameServer.Application                      │   │
│  │  ┌──────────────┐ ┌────────────────┐ ┌──────────────┐   │   │
│  │  │ LoginHandler │ │ ResourceHandler│ │ GiftHandler  │   │   │
│  │  └──────────────┘ └────────────────┘ └──────────────┘   │   │
│  └─────────────────────────────────────────────────────────┘   │
│           │                    │                                │
│           ▼                    ▼                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │            GameServer.Infrastructure                     │   │
│  │  ┌────────────────┐ ┌─────────────┐ ┌────────────────┐  │   │
│  │  │ SessionManager │ │ GameNotifier│ │ StateRepository│  │   │
│  │  │ (In-Memory)    │ │ (WebSocket) │ │ (SQLite/EF)    │  │   │
│  │  └────────────────┘ └─────────────┘ └────────────────┘  │   │
│  └─────────────────────────────────────────────────────────┘   │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              GameServer.Domain                           │   │
│  │  ┌────────┐ ┌──────────┐ ┌────────────┐ ┌───────────┐   │   │
│  │  │ Player │ │ Resource │ │ Friendship │ │ Result<T> │   │   │
│  │  └────────┘ └──────────┘ └────────────┘ └───────────┘   │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started) (optional, for containerized deployment)

## Quick Start

### Option 1: Docker (Recommended)

```bash
# Clone the repository
git clone <repository-url>
cd assignment

# Start the server
docker-compose up -d

# Check health
curl http://localhost:5000/health

# Run the interactive client
docker-compose --profile client run --rm gameserver-client
```

### Option 2: Local Development

```bash
# Clone and restore
git clone <repository-url>
cd assignment
dotnet restore

# Run the server
dotnet run --project src/GameServer.Api

# In another terminal, run the client
dotnet run --project src/GameServer.ConsoleClient
```

### Option 3: Run Tests

```bash
dotnet test
```

## Environment Variables

### API Server

| Variable                         | Description                | Default         |
| -------------------------------- | -------------------------- | --------------- |
| `ASPNETCORE_ENVIRONMENT`         | Environment mode           | `Production`    |
| `ASPNETCORE_URLS`                | Listen URL                 | `http://+:8080` |
| `GameServer__MaxConnections`     | Max concurrent connections | `1000`          |
| `GameServer__LatencyThresholdMs` | Slow message threshold     | `50`            |
| `Serilog__MinimumLevel__Default` | Log level                  | `Information`   |

### Console Client

| Variable               | Description                       | Default                  |
| ---------------------- | --------------------------------- | ------------------------ |
| `GAMESERVER_URI`       | WebSocket server URL              | `ws://localhost:5000/ws` |
| `GAMESERVER_LOG_LEVEL` | Log level (DEBUG/INFO/WARN/ERROR) | `Information`            |

## Protocol

All messages use a JSON envelope format:

```json
{
  "type": "MESSAGE_TYPE",
  "payload": { ... }
}
```

### Message Types

| Type               | Direction       | Description                   |
| ------------------ | --------------- | ----------------------------- |
| `LOGIN`            | Client → Server | Authenticate with device ID   |
| `LOGIN_RESPONSE`   | Server → Client | Returns player ID             |
| `UPDATE_RESOURCES` | Client → Server | Add/deduct coins or rolls     |
| `ADD_FRIEND`       | Client → Server | Add another player as friend  |
| `SEND_GIFT`        | Client → Server | Send resources to a friend    |
| `FRIEND_ADDED`     | Server → Client | Notification when added as friend |
| `GIFT_RECEIVED`    | Server → Client | Notification of received gift |
| `ERROR`            | Server → Client | Error response                |

### Example: Login

**Request:**

```json
{
  "type": "LOGIN",
  "payload": { "deviceId": "my-device-123" }
}
```

**Response:**

```json
{
  "type": "LOGIN_RESPONSE",
  "payload": { "playerId": "550e8400-e29b-41d4-a716-446655440000" }
}
```

## Client Commands

```
login [device-id]              Login (auto-generates ID if omitted)
balance <type> <value>         Update resource (0=Coins, 1=Rolls)
addfriend <player-id>          Add a player as friend
gift <friend-id> <type> <amt>  Send gift to friend
help                           Show commands
quit                           Disconnect and exit
```

## Testing Gift Flow

To test the complete gift flow between two players:

```bash
# Terminal 1: Start the API server
dotnet run --project src/GameServer.Api

# Terminal 2: Client A
dotnet run --project src/GameServer.ConsoleClient
> login
Logging in with DeviceId: aaaa-1111-2222-3333-444444444444...
✓ Logged in! PlayerId: 11111111-aaaa-bbbb-cccc-dddddddddddd

# Terminal 3: Client B
dotnet run --project src/GameServer.ConsoleClient
> login
Logging in with DeviceId: bbbb-5555-6666-7777-888888888888...
✓ Logged in! PlayerId: 22222222-eeee-ffff-0000-111111111111

# Client A: Add coins to have something to gift
> balance 0 500
Adding 500 Coins...

# Client A: Add Client B as friend (copy B's PlayerId)
> addfriend 22222222-eeee-ffff-0000-111111111111

# Client A: Send gift to Client B
> gift 22222222-eeee-ffff-0000-111111111111 0 100
Sending 100 Coins to 22222222-eeee-ffff-0000-111111111111...

# Client B should see:
🎁 Gift received! From: 11111111-aaaa-bbbb-cccc-dddddddddddd, Type: Coins, Amount: 100
```

**Important:** Players must be friends before sending gifts!

## Docker Commands

```bash
# Build images
docker-compose build

# Start API only
docker-compose up -d

# Start with client
docker-compose --profile client up

# View logs
docker-compose logs -f gameserver-api

# Stop all
docker-compose down

# Rebuild without cache
docker-compose build --no-cache
```

## Project Structure

```
GameServer/
├── src/
│   ├── GameServer.Domain/          # Entities, interfaces, Result<T>
│   ├── GameServer.Application/     # Handlers, business logic
│   ├── GameServer.Infrastructure/  # EF Core, session management
│   ├── GameServer.Api/             # WebSocket middleware, entry point
│   └── GameServer.ConsoleClient/   # Interactive test client
├── tests/
│   └── GameServer.UnitTests/       # xUnit + Moq tests
├── docker-compose.yml
└── Dockerfile
```

## Key Features

- **Raw WebSockets**: No SignalR, pure `System.Net.WebSockets`
- **Clean Architecture**: Domain → Application → Infrastructure → API
- **Security**: Player ID derived from WebSocket context, never from payload
- **Performance**: Zero-allocation logging with `[LoggerMessage]` source generators
- **Concurrency**: Thread-safe session management with `ConcurrentDictionary`
- **Transactions**: Atomic gift transfers using EF Core transactions
- **Health Checks**: `/health` endpoint for container orchestration

## License

MIT
