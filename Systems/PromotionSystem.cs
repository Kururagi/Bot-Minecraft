using DSharpPlus.Entities;
using DSharpPlus;
using MySql.Data.MySqlClient;
using System.Data;

public static class PromotionSystem
{
    public static async Task ShowPromotionDropdown(DiscordInteraction interaction)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงโปรโมชั่นทั้งหมดที่ active
            var cmd = new MySqlCommand(
                "SELECT * FROM promotions WHERE is_active = TRUE",
                conn);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("⚠️ ขณะนี้ไม่มีโปรโมชั่นที่ใช้งานได้")
                        .AsEphemeral(true));
                return;
            }

            var options = new List<DiscordSelectComponentOption>();

            while (await reader.ReadAsync())
            {
                options.Add(new DiscordSelectComponentOption(
                    reader.GetString("name"),
                    reader.GetInt32("id").ToString(),
                    description: $"ต้องเติมขั้นต่ำ {reader.GetDecimal("min_topup_points")} บาท"
                ));
            }

            var dropdown = new DiscordSelectComponent(
                "promotion_select",
                "เลือกโปรโมชั่น...",
                options
            );

            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("โปรดเลือกโปรโมชั่นที่ต้องการ")
                    .AddComponents(dropdown)
                    .AsEphemeral(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing promotions: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการแสดงโปรโมชั่น")
                    .AsEphemeral(true));
        }
    }

    public static async Task HandlePromotionSelection(DiscordInteraction interaction, string selectedPromotionId)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลโปรโมชั่น
            var promotionCmd = new MySqlCommand(
                "SELECT * FROM promotions WHERE id = @id",
                conn);
            promotionCmd.Parameters.AddWithValue("@id", selectedPromotionId);

            using var promotionReader = await promotionCmd.ExecuteReaderAsync();

            if (!promotionReader.Read())
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่พบโปรโมชั่นที่เลือก")
                        .AsEphemeral(true));
                return;
            }

            // ตรวจสอบว่าผู้ใช้เคยรับโปรโมชั่นนี้ไปแล้วหรือยัง
            await promotionReader.CloseAsync();

            var checkClaimedCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM user_promotions WHERE user_id = @userId AND promotion_id = @promotionId",
                conn);
            checkClaimedCmd.Parameters.AddWithValue("@userId", interaction.User.Id);
            checkClaimedCmd.Parameters.AddWithValue("@promotionId", selectedPromotionId);

            var alreadyClaimed = Convert.ToInt32(await checkClaimedCmd.ExecuteScalarAsync()) > 0;

            // ดึงข้อมูล topup_points ของผู้ใช้
            var userCmd = new MySqlCommand(
                "SELECT topup_points FROM authme WHERE discord_id = @userId",
                conn);
            userCmd.Parameters.AddWithValue("@userId", interaction.User.Id);

            var userTopupPoints = Convert.ToDecimal(await userCmd.ExecuteScalarAsync());
            var minTopupPoints = promotionReader.GetDecimal("min_topup_points");
            var canClaim = userTopupPoints >= minTopupPoints && !alreadyClaimed;

            // สร้าง Embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle(promotionReader.GetString("name"))
                .WithDescription(promotionReader.GetString("description"))
                .AddField("เงื่อนไข", $"ต้องเติมเงินขั้นต่ำ {minTopupPoints:N2} บาท", true)
                .AddField("สถานะ", alreadyClaimed ? "✅ รับแล้ว" : canClaim ? "🟢 สามารถรับได้" : "❌ ยังไม่สามารถรับได้", true)
                .WithColor(canClaim && !alreadyClaimed ? DiscordColor.Green : DiscordColor.Red)
                .WithThumbnail(promotionReader.GetString("image_url"))
                .WithFooter($"Promotion ID: {selectedPromotionId}");

            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true);

            // เพิ่มปุ่มรับโปรโมชั่นถ้าสามารถรับได้
            if (canClaim && !alreadyClaimed)
            {
                var claimButton = new DiscordButtonComponent(
                    ButtonStyle.Success,
                    "claim_promotion_btn",
                    "รับโปรโมชั่น",
                    emoji: new DiscordComponentEmoji("🎁")
                );
                responseBuilder.AddComponents(claimButton);
            }

            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                responseBuilder);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling promotion selection: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการแสดงรายละเอียดโปรโมชั่น")
                    .AsEphemeral(true));
        }
    }

    public static async Task HandlePromotionClaim(DiscordInteraction interaction, string promotionId)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ตรวจสอบเงื่อนไขอีกครั้งเพื่อป้องกันการรับซ้ำ
                // ดึงข้อมูลโปรโมชั่น
                var promotionCmd = new MySqlCommand(
                    "SELECT min_topup_points, reward_command FROM promotions WHERE id = @id",
                    conn, transaction);
                promotionCmd.Parameters.AddWithValue("@id", promotionId);

                var minTopupPoints = Convert.ToDecimal(await promotionCmd.ExecuteScalarAsync());

                // ตรวจสอบว่าผู้ใช้เคยรับไปแล้วหรือยัง
                var checkClaimedCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM user_promotions WHERE user_id = @userId AND promotion_id = @promotionId",
                    conn, transaction);
                checkClaimedCmd.Parameters.AddWithValue("@userId", interaction.User.Id);
                checkClaimedCmd.Parameters.AddWithValue("@promotionId", promotionId);

                var alreadyClaimed = Convert.ToInt32(await checkClaimedCmd.ExecuteScalarAsync()) > 0;

                // ตรวจสอบ topup_points
                var userCmd = new MySqlCommand(
                    "SELECT topup_points, username FROM authme WHERE discord_id = @userId",
                    conn, transaction);
                userCmd.Parameters.AddWithValue("@userId", interaction.User.Id);

                using var userReader = await userCmd.ExecuteReaderAsync();
                if (!userReader.Read())
                {
                    await transaction.RollbackAsync();
                    await interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่พบข้อมูลผู้ใช้ในระบบ")
                            .AsEphemeral(true));
                    return;
                }

                var userTopupPoints = userReader.GetDecimal("topup_points");
                var username = userReader.GetString("username");
                await userReader.CloseAsync();

                if (alreadyClaimed || userTopupPoints < minTopupPoints)
                {
                    await transaction.RollbackAsync();
                    await interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ คุณไม่สามารถรับโปรโมชั่นนี้ได้")
                            .AsEphemeral(true));
                    return;
                }

                // บันทึกการรับโปรโมชั่น
                var insertCmd = new MySqlCommand(
                    "INSERT INTO user_promotions (user_id, promotion_id) VALUES (@userId, @promotionId)",
                    conn, transaction);
                insertCmd.Parameters.AddWithValue("@userId", interaction.User.Id);
                insertCmd.Parameters.AddWithValue("@promotionId", promotionId);
                await insertCmd.ExecuteNonQueryAsync();

                // ส่งคำสั่ง reward (ตัวอย่างเป็นคำสั่ง Minecraft)
                var rewardCommand = (string)await promotionCmd.ExecuteScalarAsync();
                if (!string.IsNullOrEmpty(rewardCommand))
                {
                    // ส่งคำสั่งไปที่เซิร์ฟเวอร์ Minecraft
                    await MinecraftCommands.SendCommand(rewardCommand.Replace("{player}", username));
                }

                await transaction.CommitAsync();

                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("✅ รับโปรโมชั่นสำเร็จ! รางวัลของคุณกำลังถูกจัดส่ง")
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error claiming promotion: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการรับโปรโมชั่น")
                    .AsEphemeral(true));
        }
    }
}