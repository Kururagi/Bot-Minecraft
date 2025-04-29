using MySql.Data.MySqlClient;

public static class DatabaseHelper
{
    public static async Task<string> GetMinecraftUsername(ulong discordId)
    {
        using var conn = new MySqlConnection(Config.MySqlConnectionString);
        await conn.OpenAsync();

        var cmd = new MySqlCommand(
            "SELECT username FROM authme WHERE discord_id = @discordId",
            conn
        );
        cmd.Parameters.AddWithValue("@discordId", discordId);

        return (await cmd.ExecuteScalarAsync())?.ToString();
    }

    public static async Task<int> GetUserBalance(ulong userId, string currencyType, MySqlConnection conn)
    {
        var column = currencyType == "cash" ? "cash" : "point";
        var cmd = new MySqlCommand($"SELECT {column} FROM authme WHERE discord_id = @userId", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public static async Task DeductUserCash(ulong userId, int amount, MySqlConnection conn)
    {
        var cmd = new MySqlCommand("UPDATE authme SET cash = cash - @amount WHERE discord_id = @userId", conn);
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> GetUserCash(ulong userId, MySqlConnection conn)
    {
        var cmd = new MySqlCommand("SELECT cash FROM authme WHERE discord_id = @userId", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public static async Task DeductUserPoints(ulong userId, int amount, MySqlConnection conn)
    {
        var cmd = new MySqlCommand("UPDATE authme SET point = point - @amount WHERE discord_id = @userId", conn);
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> GetBoxPrice(MySqlConnection conn, string boxId)
    {
        var cmd = new MySqlCommand(
            "SELECT price FROM gacha_boxes WHERE id = @boxId",
            conn);
        cmd.Parameters.AddWithValue("@boxId", boxId);

        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public static async Task<int> GetUserPoints(ulong userId, MySqlConnection conn)
    {
        var cmd = new MySqlCommand("SELECT point FROM authme WHERE discord_id = @userId", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public static async Task<string> GetBoxCurrencyType(MySqlConnection conn, string boxId)
    {
        var cmd = new MySqlCommand(
            "SELECT IFNULL(currency_type, 'point') FROM gacha_boxes WHERE id = @boxId",
            conn);
        cmd.Parameters.AddWithValue("@boxId", boxId);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? "point";
    }
}