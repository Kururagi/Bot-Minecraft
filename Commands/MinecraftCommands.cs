using Rcon;
using System;
using System.Threading.Tasks;

public static class MinecraftCommands
{
    private static RconClient rcon;
    private static bool isInitialized = false;
    private static readonly object lockObject = new object();

    public static async Task InitializeRcon()
    {
        try
        {
            lock (lockObject)
            {
                if (isInitialized && rcon != null)
                    return;
            }

            var newRcon = new RconClient();
            await newRcon.ConnectAsync(Config.MinecraftServerIP, Config.MinecraftServerPort);
            await newRcon.AuthenticateAsync(Config.RconPassword);

            lock (lockObject)
            {
                rcon = newRcon;
                isInitialized = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RCON Initialization Error: {ex.Message}");
            isInitialized = false;
        }
    }

    public static async Task<bool> SendCommand(string command)
    {
        try
        {
            if (!isInitialized || rcon == null)
            {
                await InitializeRcon();
                if (!isInitialized) return false;
            }

            if (string.IsNullOrWhiteSpace(command))
                return false;

            var response = await rcon.SendCommandAsync(command);
            Console.WriteLine($"RCON Response: {response}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending Minecraft command: {ex.Message}");
            isInitialized = false;
            return false;
        }
    }

    public static async Task SendMinecraftCommand(string username, GachaItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(username))
            return;

        var formattedCommand = item.Command.Replace("{username}", username);
        await SendCommand(formattedCommand);
    }

    public static async Task<bool> IsPlayerOnline(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        try
        {
            if (!isInitialized || rcon == null)
            {
                await InitializeRcon();
                if (!isInitialized || rcon == null)
                {
                    Console.WriteLine("RCON not initialized.");
                    return false;
                }
            }

            var safeUsername = username.Replace("\"", "");
            var response = await rcon.SendCommandAsync($"data get entity {safeUsername} Pos");

            return !(string.IsNullOrEmpty(response) ||
                   response.Contains("No entity was found", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RCON Error] Failed to check player '{username}': {ex.Message}");
            isInitialized = false;
            return false;
        }
    }
}