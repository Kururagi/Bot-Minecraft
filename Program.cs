using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using MySql.Data.MySqlClient;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Rcon;
using Newtonsoft.Json;
using System.Text;
using SixLabors.ImageSharp;
using static Org.BouncyCastle.Math.EC.ECCurve;

class Program
{
    static DiscordClient discord;
    static InteractivityExtension interactivity;
    static RconClient rcon;
    private static System.Timers.Timer restartTimer;
    private static volatile bool shouldRestart;
    private static ManualResetEventSlim shutdownEvent = new ManualResetEventSlim();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting bot application...");

        // Setup graceful shutdown for Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("Shutdown signal received!");
            e.Cancel = true;
            shutdownEvent.Set();
        };

        while (!shutdownEvent.IsSet)
        {
            shouldRestart = false;

            // Initialize and start the 2-hour restart timer
            //restartTimer = new System.Timers.Timer(12000); // 2 hours in milliseconds
            restartTimer = new System.Timers.Timer(7200000); // 2 hours in milliseconds
            restartTimer = new System.Timers.Timer(300000); // 5 mins in milliseconds
            restartTimer.Elapsed += (s, e) =>
            {
                Console.WriteLine("Restart timer elapsed! Initiating restart...");
                shouldRestart = true;
                shutdownEvent.Set(); // This will break the RunBot loop
            };
            restartTimer.AutoReset = false;
            restartTimer.Start();

            try
            {
                Console.WriteLine("Starting bot instance...");
                await RunBot();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bot crashed: {ex}");
            }
            finally
            {
                restartTimer.Stop();
                restartTimer.Dispose();
            }

            if (shouldRestart)
            {
                Console.WriteLine("Preparing to restart bot...");
                await Task.Delay(5000); // Wait 5 seconds before restarting
                shutdownEvent.Reset(); // Reset the shutdown signal for the next iteration
            }
            else
            {
                Console.WriteLine("Bot stopped unexpectedly. Restarting in 10 seconds...");
                await Task.Delay(10000);
            }
        }

        Console.WriteLine("Application is shutting down...");
    }

    static async Task RunBot()
    {
        // 1. Configure Discord Client
        discord = new DiscordClient(new DiscordConfiguration()
        {
            Token = Config.DiscordToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
            AutoReconnect = true,
            MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information
        });

        interactivity = discord.UseInteractivity(new InteractivityConfiguration()
        {
            Timeout = TimeSpan.FromMinutes(5)
        });

        // 2. Initialize RCON Connection
        rcon = new RconClient();
        try
        {
            await rcon.ConnectAsync(Config.MinecraftServerIP, Config.MinecraftServerPort);
            await rcon.AuthenticateAsync(Config.RconPassword);
            Console.WriteLine("RCON connected successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RCON connection failed: {ex.Message}");
            throw;
        }

        // 3. Initialize Command Modules
        AdminCommands.Initialize(discord);
        UserCommands.Initialize(discord);

        // 4. Register Event Handlers
        discord.MessageCreated += MessageCreated.OnMessageCreated;
        discord.ComponentInteractionCreated += VerifySystem.OnVerifyButtonClicked;
        discord.ModalSubmitted += ModalSubmitted.OnModalSubmitted;
        discord.ComponentInteractionCreated += InteractionCreated.OnComponentInteractionCreated;

        // 5. Connect to Discord
        await discord.ConnectAsync();
        Console.WriteLine($"Discord bot connected at {DateTime.Now}");

        // 6. Keep the bot running until shutdown or restart
        while (!shutdownEvent.IsSet)
        {
            await Task.Delay(1000); // Reduce CPU usage
        }

        // 7. Cleanup before shutdown/restart
        Console.WriteLine("Disconnecting bot...");
        await discord.DisconnectAsync();

        if (rcon != null && rcon.Connected)
        {
            rcon.Disconnect();
            Console.WriteLine("RCON disconnected.");
        }
    }
}