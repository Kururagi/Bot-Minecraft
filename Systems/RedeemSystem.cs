using DSharpPlus.Entities;
using DSharpPlus;
using MySql.Data.MySqlClient;
using Rcon;

public static class RedeemSystem
{
    static RconClient rcon;

    public static async Task InitializeRcon()
    {
        try
        {
            rcon = new RconClient();
            await rcon.ConnectAsync(Config.MinecraftServerIP, Config.MinecraftServerPort);
            await rcon.AuthenticateAsync(Config.RconPassword);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing RCON: {ex.Message}");
            rcon = null;
        }
    }

    public static async Task ShowRedeemCodeModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("แลก Redeem Code")
            .WithCustomId("redeem_code_modal")
            .AddComponents(new TextInputComponent(
                label: "กรอกรหัสของขวัญ",
                customId: "redeem_code_input",
                placeholder: "เช่น: WELCOME2023",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task HandleRedeemCode(DiscordInteraction interaction, string code)
    {
        try
        {
            var userId = interaction.User.Id;
            await interaction.DeferAsync(true); // Defer ก่อนเพื่อป้องกัน timeout

            // ตรวจสอบการเชื่อมต่อ RCON
            if (rcon == null)
            {
                await InitializeRcon();
                if (rcon == null)
                {
                    await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                        .WithContent("❌ ไม่สามารถเชื่อมต่อกับเซิร์ฟเวอร์ได้"));
                    return;
                }
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ตรวจสอบว่าเคยใช้โค้ดนี้ไปแล้วหรือไม่
            var checkUsedCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM user_redeemed_codes WHERE user_id = @userId AND code = @code",
                conn);
            checkUsedCmd.Parameters.AddWithValue("@userId", userId);
            checkUsedCmd.Parameters.AddWithValue("@code", code);

            if (Convert.ToInt32(await checkUsedCmd.ExecuteScalarAsync()) > 0)
            {
                await interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder()
                        .WithContent("⚠️ คุณใช้โค้ดนี้ไปแล้ว"));
                return;
            }

            // ดึงข้อมูลโค้ด
            var getCodeCmd = new MySqlCommand(
                "SELECT * FROM redeem_codes WHERE code = @code AND (max_uses > use_count OR max_uses = 0) " +
                "AND (expires_at IS NULL OR expires_at > NOW())",
                conn);
            getCodeCmd.Parameters.AddWithValue("@code", code);

            using var reader = await getCodeCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder()
                        .WithContent("❌ โค้ดไม่ถูกต้อง, ถูกใช้หมดแล้ว, หรือหมดอายุ"));
                return;
            }

            var rewardType = reader["reward_type"].ToString();
            var rewardValue = reader["reward_value"].ToString();
            var isSingleUse = Convert.ToBoolean(reader["is_single_use"]);

            // ให้รางวัล
            await reader.CloseAsync();
            switch (rewardType)
            {
                case "command":
                    var username = await DatabaseHelper.GetMinecraftUsername(userId);
                    if (string.IsNullOrEmpty(username))
                    {
                        await interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder()
                                .WithContent("❌ ไม่พบข้อมูล Minecraft ของคุณ"));
                        return;
                    }

                    try
                    {
                        await rcon.SendCommandAsync(rewardValue.Replace("{username}", username));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing RCON command: {ex}");
                        await interaction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder()
                                .WithContent("⚠️ ไม่สามารถส่งคำสั่งไปยังเซิร์ฟเวอร์ได้"));
                        return;
                    }
                    break;

                case "point":
                    var pointCmd = new MySqlCommand(
                        "UPDATE authme SET point = point + @value WHERE discord_id = @userId",
                        conn);
                    pointCmd.Parameters.AddWithValue("@value", int.Parse(rewardValue));
                    pointCmd.Parameters.AddWithValue("@userId", userId);
                    await pointCmd.ExecuteNonQueryAsync();
                    break;

                case "cash":
                    var cashCmd = new MySqlCommand(
                        "UPDATE authme SET cash = cash + @value WHERE discord_id = @userId",
                        conn);
                    cashCmd.Parameters.AddWithValue("@value", decimal.Parse(rewardValue));
                    cashCmd.Parameters.AddWithValue("@userId", userId);
                    await cashCmd.ExecuteNonQueryAsync();
                    break;
            }

            // อัปเดตสถิติ
            var updateCmd = new MySqlCommand(
                "UPDATE redeem_codes SET use_count = use_count + 1 WHERE code = @code",
                conn);
            updateCmd.Parameters.AddWithValue("@code", code);
            await updateCmd.ExecuteNonQueryAsync();

            var insertCmd = new MySqlCommand(
                "INSERT INTO user_redeemed_codes (user_id, code) VALUES (@userId, @code)",
                conn);
            insertCmd.Parameters.AddWithValue("@userId", userId);
            insertCmd.Parameters.AddWithValue("@code", code);
            await insertCmd.ExecuteNonQueryAsync();

            var embed = new DiscordEmbedBuilder()
                .WithTitle("🎉 แลกโค้ดสำเร็จ!")
                .WithDescription($"คุณได้ใช้ โค้ด: {code} เรียบร้อยแล้ว!")
                .WithColor(DiscordColor.Green);

            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .AddEmbed(embed));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error redeeming code: {ex}");
            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการแลกโค้ด"));
        }
    }
}