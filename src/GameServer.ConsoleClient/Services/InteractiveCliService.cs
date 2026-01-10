using GameServer.ConsoleClient.Clients;
using Microsoft.Extensions.Logging;

namespace GameServer.ConsoleClient.Services;

public sealed class InteractiveCliService(GameClient client, ILogger<InteractiveCliService> logger)
{
    private const string DefaultServerUri = "ws://localhost:5000/ws";
    private string? _currentDeviceId;
    private Guid? _currentPlayerId;

    public async Task<int> RunAsync(string[] args, bool verboseMode = false)
    {
        var serverUri = ResolveServerUri(args);

        SetupEventHandlers();

        PrintBanner(verboseMode);

        try
        {
            Console.Write($"Connecting to {serverUri}... ");
            var connected = await client.ConnectAsync(serverUri);

            if (!connected)
            {
                Console.WriteLine("FAILED");
                logger.LogError("Could not connect to server at {ServerUri}", serverUri);
                return 1;
            }

            Console.WriteLine("OK");
            client.StartListening();

            await RunCommandLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Error: {ex.Message}");
            logger.LogError(ex, "Fatal error in CLI");
            return 1;
        }

        return 0;
    }

    private void SetupEventHandlers()
    {
        client.OnLoginResponse += response =>
        {
            _currentPlayerId = response.PlayerId;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Logged in! PlayerId: {response.PlayerId}");
            Console.ResetColor();
            Console.Write("> ");
            logger.LogInformation("Login successful: PlayerId={PlayerId}", response.PlayerId);
        };

        client.OnGiftReceived += gift =>
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"🎁 Gift received! From: {gift.FromPlayerId}, Type: {gift.ResourceType}, Amount: {gift.Amount}");
            Console.ResetColor();
            Console.Write("> ");
            logger.LogInformation("Gift received: From={FromPlayerId}, Type={ResourceType}, Amount={Amount}", 
                gift.FromPlayerId, gift.ResourceType, gift.Amount);
        };

        client.OnFriendAdded += friendAdded =>
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"👋 New friend! Player {friendAdded.ByPlayerId} added you as a friend.");
            Console.ResetColor();
            Console.Write("> ");
            logger.LogInformation("Friend added by: PlayerId={PlayerId}", friendAdded.ByPlayerId);
        };

        client.OnFriendOnline += friendOnline =>
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🟢 Friend online! Player {friendOnline.FriendPlayerId} is now online.");
            Console.ResetColor();
            Console.Write("> ");
            logger.LogInformation("Friend came online: PlayerId={PlayerId}", friendOnline.FriendPlayerId);
        };

        client.OnResourceUpdated += resourceUpdated =>
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"💰 Balance updated! {resourceUpdated.TypeName}: {resourceUpdated.NewBalance}");
            Console.ResetColor();
            Console.Write("> ");
            logger.LogInformation("Resource updated: Type={Type}, NewBalance={NewBalance}", 
                resourceUpdated.TypeName, resourceUpdated.NewBalance);
        };

        client.OnError += error =>
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: [{error.Code}] {error.Message}");
            Console.ResetColor();
            Console.Write("> ");
            logger.LogWarning("Server error: Code={Code}, Message={Message}", error.Code, error.Message);
        };

        client.OnDisconnected += (status, description) =>
        {
            _currentPlayerId = null;
            _currentDeviceId = null;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Disconnected: {status} - {description}");
            Console.ResetColor();
        };
    }

    private async Task RunCommandLoop()
    {
        PrintHelp();

        while (client.IsConnected)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLowerInvariant();

            logger.LogDebug("Command received: {Command}", command);

            try
            {
                switch (command)
                {
                    case "login":
                        await HandleLogin(parts);
                        break;

                    case "balance" or "update":
                        await HandleUpdateResource(parts);
                        break;

                    case "gift":
                        await HandleSendGift(parts);
                        break;

                    case "addfriend" or "friend":
                        await HandleAddFriend(parts);
                        break;

                    case "status" or "whoami":
                        PrintStatus();
                        break;

                    case "help":
                        PrintHelp();
                        break;

                    case "quit" or "exit":
                        Console.WriteLine("Disconnecting...");
                        await client.DisconnectAsync();
                        return;

                    default:
                        Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Command failed: {ex.Message}");
                Console.ResetColor();
                logger.LogError(ex, "Command '{Command}' failed", command);
            }
        }
    }

    private async Task HandleLogin(string[] parts)
    {
        if (_currentPlayerId.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Already logged in as PlayerId: {_currentPlayerId.Value}");
            Console.WriteLine($"DeviceId: {_currentDeviceId}");
            Console.ResetColor();
            return;
        }

        var deviceId = parts.Length >= 2 ? parts[1] : Guid.NewGuid().ToString();
        _currentDeviceId = deviceId;
        
        Console.WriteLine($"Logging in with DeviceId: {deviceId}...");
        logger.LogInformation("Sending login request: DeviceId={DeviceId}", deviceId);
        await client.SendAsync(new LoginRequest(deviceId));
    }

    private async Task HandleUpdateResource(string[] parts)
    {
        if (!RequireLogin())
            return;

        if (parts.Length < 3)
        {
            Console.WriteLine("Usage: balance <type> <value>");
            Console.WriteLine("Types: 0 = Coins, 1 = Rolls");
            Console.WriteLine("Example: balance 0 100    (add 100 coins)");
            Console.WriteLine("Example: balance 0 -50   (deduct 50 coins)");
            return;
        }

        if (!int.TryParse(parts[1], out var resourceType) || resourceType < 0 || resourceType > 1)
        {
            Console.WriteLine("Invalid resource type. Use 0 for Coins, 1 for Rolls.");
            return;
        }

        if (!long.TryParse(parts[2], out var value))
        {
            Console.WriteLine("Invalid value. Must be a number.");
            return;
        }

        var typeName = resourceType == 0 ? "Coins" : "Rolls";
        var action = value >= 0 ? "Adding" : "Deducting";
        Console.WriteLine($"{action} {Math.Abs(value)} {typeName}...");
        logger.LogInformation("Sending update resource: Type={ResourceType}, Value={Value}", typeName, value);
        await client.SendAsync(new UpdateResourceRequest(resourceType, value));
    }

    private async Task HandleSendGift(string[] parts)
    {
        if (!RequireLogin())
            return;

        if (parts.Length < 4)
        {
            Console.WriteLine("Usage: gift <friend-player-id> <type> <amount>");
            Console.WriteLine("Types: 0 = Coins, 1 = Rolls");
            Console.WriteLine("Example: gift 550e8400-e29b-41d4-a716-446655440000 0 100");
            return;
        }

        if (!Guid.TryParse(parts[1], out var friendPlayerId))
        {
            Console.WriteLine("Invalid friend player ID. Must be a valid GUID.");
            return;
        }

        if (!int.TryParse(parts[2], out var resourceType) || resourceType < 0 || resourceType > 1)
        {
            Console.WriteLine("Invalid resource type. Use 0 for Coins, 1 for Rolls.");
            return;
        }

        if (!long.TryParse(parts[3], out var amount) || amount <= 0)
        {
            Console.WriteLine("Invalid amount. Must be a positive number.");
            return;
        }

        var typeName = resourceType == 0 ? "Coins" : "Rolls";
        Console.WriteLine($"Sending {amount} {typeName} to {friendPlayerId}...");
        logger.LogInformation("Sending gift: FriendId={FriendId}, Type={ResourceType}, Amount={Amount}", 
            friendPlayerId, typeName, amount);
        await client.SendAsync(new SendGiftRequest(friendPlayerId, resourceType, amount));
    }

    private async Task HandleAddFriend(string[] parts)
    {
        if (!RequireLogin())
            return;

        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: addfriend <friend-player-id>");
            Console.WriteLine("Example: addfriend 550e8400-e29b-41d4-a716-446655440000");
            return;
        }

        if (!Guid.TryParse(parts[1], out var friendPlayerId))
        {
            Console.WriteLine("Invalid friend player ID. Must be a valid GUID.");
            return;
        }

        Console.WriteLine($"Adding friend {friendPlayerId}...");
        logger.LogInformation("Sending add friend: FriendId={FriendId}", friendPlayerId);
        await client.SendAsync(new AddFriendRequest(friendPlayerId));
    }

    private static void PrintBanner(bool verboseMode)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║         Game Server Console Client         ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");
        if (verboseMode)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("             [VERBOSE MODE ENABLED]           ");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    private bool RequireLogin()
    {
        if (_currentPlayerId.HasValue)
            return true;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("You must login first. Use 'login' command.");
        Console.ResetColor();
        return false;
    }

    private void PrintStatus()
    {
        Console.WriteLine();
        if (_currentPlayerId.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Status: Logged In");
            Console.ResetColor();
            Console.WriteLine($"  PlayerId: {_currentPlayerId.Value}");
            Console.WriteLine($"  DeviceId: {_currentDeviceId}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Status: Not Logged In");
            Console.ResetColor();
            Console.WriteLine("  Use 'login' command to authenticate.");
        }
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  login [device-id]                        - Login (auto-generates ID if omitted)");
        Console.WriteLine("  status                                   - Show current login status");
        Console.WriteLine("  balance <type> <value>                   - Update resource (type: 0=Coins, 1=Rolls)");
        Console.WriteLine("  addfriend <player-id>                    - Add a player as friend");
        Console.WriteLine("  gift <friend-id> <type> <amount>         - Send gift to friend");
        Console.WriteLine("  help                                     - Show this help message");
        Console.WriteLine("  quit                                     - Disconnect and exit");
        Console.WriteLine();
    }

    private static Uri ResolveServerUri(string[] args)
    {
        var nonFlagArg = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (!string.IsNullOrEmpty(nonFlagArg))
            return new Uri(nonFlagArg);

        var envUri = Environment.GetEnvironmentVariable("GAMESERVER_URI");
        if (!string.IsNullOrWhiteSpace(envUri))
            return new Uri(envUri);

        return new Uri(DefaultServerUri);
    }
}
