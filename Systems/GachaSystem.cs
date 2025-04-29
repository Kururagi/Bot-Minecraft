using System.Data;
using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using MySql.Data.MySqlClient;
using Rcon;

public static class GachaSystem
{
    static RconClient rcon;
    private static Dictionary<ulong, string> userSelectedBox = new();

    public static async Task ShowGachaBoxes(DiscordInteraction interaction)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand("SELECT id, name, image_url FROM gacha_boxes", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var options = new List<DiscordSelectComponentOption>();
            while (await reader.ReadAsync())
            {
                options.Add(new DiscordSelectComponentOption(
                    reader.GetString("name"),
                    reader.GetInt32("id").ToString()
                ));
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("🎁 เลือกกล่องกาชา")
                .WithDescription("เลือกกล่องที่ต้องการเปิดจากเมนูด้านล่าง")
                .WithColor(DiscordColor.Gold);

            var dropdown = new DiscordSelectComponent("gacha_box_select", "เลือกกล่อง...", options);

            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AddComponents(dropdown)
                    .AsEphemeral(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการแสดงกล่องกาชา")
                    .AsEphemeral(true));
        }
    }

    public static async Task ShowGachaBoxDetails(DiscordInteraction interaction, string boxId)
    {
        try
        {
            await interaction.DeferAsync(true);

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลกล่อง
            string boxName, boxImageUrl;
            int boxPrice;
            string currencyType = "point";

            var boxCmd = new MySqlCommand(
                "SELECT name, image_url, price, IFNULL(currency_type, 'point') as currency_type FROM gacha_boxes WHERE id = @boxId",
                conn);
            boxCmd.Parameters.AddWithValue("@boxId", boxId);

            using (var boxReader = await boxCmd.ExecuteReaderAsync())
            {
                if (!await boxReader.ReadAsync())
                {
                    await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                        .WithContent("❌ ไม่พบกล่องกาชานี้"));
                    return;
                }

                boxName = boxReader.GetString("name");
                boxImageUrl = boxReader.GetString("image_url");
                boxPrice = boxReader.GetInt32("price");
                currencyType = boxReader.GetString("currency_type");
            }

            string currencyIcon = currencyType == "cash" ? "💵" : "🪙";
            var items = await GetGachaItems(conn, boxId);

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"🎁 {boxName}")
                .WithDescription("เลือกรูปแบบการสุ่ม")
                .WithColor(DiscordColor.CornflowerBlue)
                .WithThumbnail(boxImageUrl) // เพิ่มรูปภาพกล่องที่ด้านขวาบน
                .AddField("💰 ราคาเปิดกล่อง",
                    $"🎲 1 ครั้ง: {boxPrice} {currencyIcon}\n" +
                    $"🎰 10 ครั้ง: {boxPrice * 10} {currencyIcon}",
                    true);

            // Build items info with validation
            var itemsInfo = new StringBuilder();
            if (items.Count > 0)
            {
                foreach (var item in items.Take(5))
                {
                    itemsInfo.AppendLine($"✦ {item.Name} {GetRarityStars(item.Rarity)}");
                }
                if (items.Count > 5) itemsInfo.AppendLine($"... และอีก {items.Count - 5} ไอเทม");
            }
            else
            {
                itemsInfo.AppendLine("⚠️ ไม่มีไอเทมในกล่องนี้");
            }

            if (itemsInfo.Length > 0)
            {
                embed.AddField("📦 ไอเทมในกล่อง", itemsInfo.ToString());
            }

            var buttons = new List<DiscordButtonComponent>
        {
            new DiscordButtonComponent(
                ButtonStyle.Success,
                $"gacha_roll_1_{boxId}",
                "🎲 สุ่ม 1 ครั้ง",
                emoji: new DiscordComponentEmoji("✨")),
            new DiscordButtonComponent(
                ButtonStyle.Danger,
                $"gacha_roll_10_{boxId}",
                "🎰 สุ่ม 10 ครั้ง",
                emoji: new DiscordComponentEmoji("🎉"))
        };

            await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                .AddEmbed(embed)
                .AddComponents(buttons));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ShowGachaBoxDetails: {ex}");
            await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                .WithContent("❌ เกิดข้อผิดพลาดในการแสดงข้อมูลกล่อง"));
        }
    }

    public static async Task HandleGachaRoll(DiscordInteraction interaction, string customId)
    {
        try
        {
            await interaction.DeferAsync(true);

            // แยกข้อมูลจาก custom ID
            var parts = customId.Split('_');
            if (parts.Length < 4)
            {
                await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("❌ ข้อมูลไม่ถูกต้อง"));
                return;
            }

            var rollCount = int.Parse(parts[2]);
            var boxId = parts[3];

            // ตรวจสอบบัญชี Minecraft
            var username = await DatabaseHelper.GetMinecraftUsername(interaction.User.Id);
            if (string.IsNullOrEmpty(username))
            {
                await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("❌ คุณยังไม่ได้ Verify บัญชี Minecraft"));
                return;
            }

            // ตรวจสอบสถานะออนไลน์
            if (!await MinecraftCommands.IsPlayerOnline(username))
            {
                var retryButton = new DiscordButtonComponent(
                    ButtonStyle.Primary,
                    $"retry_check_{DateTime.Now.Ticks}",
                    "ตรวจสอบอีกครั้ง");

                await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .WithContent($"⚠️ **ระบบไม่พบคุณในเกม**\n" +
                                "ชื่อในเกม: " + username + "\n" +
                                "โปรดตรวจสอบว่า:\n" +
                                "1. คุณล็อกอินเกมแล้วจริงๆ\n" +
                                "2. ชื่อในเกมถูกต้อง\n" +
                                "3. คุณอยู่บนเซิร์ฟเวอร์ที่ถูกต้อง")
                    .AddComponents(retryButton));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลกล่อง
            var boxName = await GetBoxName(conn, boxId);
            if (string.IsNullOrEmpty(boxName))
            {
                await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("❌ ไม่พบกล่องกาชานี้"));
                return;
            }

            var boxPrice = await DatabaseHelper.GetBoxPrice(conn, boxId);
            var totalCost = boxPrice * rollCount;
            var currencyType = await DatabaseHelper.GetBoxCurrencyType(conn, boxId);

            // ตรวจสอบยอดเงิน
            if (!await CanUserAfford(interaction.User.Id, totalCost, currencyType, conn))
            {
                string currencyName = currencyType == "cash" ? "Cash" : "Point";
                await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .WithContent($"❌ คุณมี {currencyName} ไม่เพียงพอ (ต้องการ {totalCost} {currencyName})"));
                return;
            }

            // ดึงไอเทมในกล่อง
            var items = await GetGachaItems(conn, boxId);
            if (items.Count == 0)
            {
                await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("❌ กล่องกาชานี้ไม่มีไอเทม"));
                return;
            }

            // ดึงค่า Guarantee Counter
            int guaranteeCounter = 0;
            try
            {
                guaranteeCounter = await GetUserGuaranteeCounter(interaction.User.Id, boxId);
            }
            catch (MySqlException ex) when (ex.Number == 1146) // Table doesn't exist
            {
                // Create table and retry
                guaranteeCounter = await GetUserGuaranteeCounter(interaction.User.Id, boxId);
            }

            // สุ่มไอเทม
            var (results, updatedCounter) = RollItems(items, rollCount, guaranteeCounter, conn);

            // อัปเดต Guarantee Counter
            await UpdateUserGuaranteeCounter(interaction.User.Id, boxId, updatedCounter);

            // ส่งคำสั่ง Minecraft
            foreach (var item in results)
            {
                await MinecraftCommands.SendMinecraftCommand(username, item);
            }

            // หักเงิน
            if (currencyType == "cash")
            {
                await DatabaseHelper.DeductUserCash(interaction.User.Id, totalCost, conn);
            }
            else
            {
                await DatabaseHelper.DeductUserPoints(interaction.User.Id, totalCost, conn);
            }

            // อัปเดตประวัติ
            foreach (var item in results)
            {
                await UpdateRollHistory(conn, interaction.User.Id, boxId, updatedCounter, item.Id);
            }

            // ดึงยอดเงินล่าสุด
            int remainingBalance = currencyType == "cash"
                ? (int)await DatabaseHelper.GetUserCash(interaction.User.Id, conn)
                : (int)await DatabaseHelper.GetUserPoints(interaction.User.Id, conn);

            // ส่งผลลัพธ์
            await SendResultEmbed(
                interaction,
                boxName,
                results,
                remainingBalance,
                currencyType,
                results.FirstOrDefault()?.ImageUrl
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleGachaRoll: {ex}");
            await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                .WithContent("❌ เกิดข้อผิดพลาดในการสุ่มของรางวัล"));
        }
    }

    public static async Task SendResultEmbed(DiscordInteraction interaction, string boxName, List<GachaItem> results, int remainingBalance, string currencyType, string itemImageUrl = null)
    {
        try
        {
            string currencyIcon = currencyType == "cash" ? "💵" : "🪙";
            string currencyName = currencyType == "cash" ? "Cash" : "Point";

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"🎉 ผลการสุ่ม {results.Count} ครั้ง")
                .WithDescription($"กล่อง: {boxName}")
                .WithColor(DiscordColor.HotPink)
                .AddField("📜 ผลลัพธ์", string.Join("\n", results
                    .GroupBy(x => x.Name)
                    .OrderByDescending(g => g.First().Rarity)
                    .Take(5)
                    .Select(g => $"{GetRarityStars(g.First().Rarity)} {g.Key} x{g.Count()}")))
                .AddField($"💰 {currencyName} ที่เหลือ", $"{remainingBalance} {currencyIcon}", true);

            // เพิ่ม thumbnail ถ้า URL รูปภาพถูกต้อง
            if (!string.IsNullOrWhiteSpace(itemImageUrl) && Uri.TryCreate(itemImageUrl, UriKind.Absolute, out _))
            {
                embed.WithThumbnail(itemImageUrl);
            }
            else
            {
                // ใช้รูปภาพเริ่มต้นถ้าไม่มีหรือไม่ถูกต้อง
                embed.WithThumbnail("https://i.imgur.com/default-gacha.png");
            }

            if (results.Count > 5) embed.AddField("📦 ไอเทมทั้งหมด", $"ได้รับไอเทม {results.Count} ชิ้น");

            var backButton = new DiscordButtonComponent(
                ButtonStyle.Secondary,
                "gacha_back_btn",
                "กลับไปเลือกกล่อง",
                emoji: new DiscordComponentEmoji("🔙"));

            await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                .AddEmbed(embed)
                .AddComponents(backButton));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendResultEmbed: {ex}");
            await interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                .WithContent("✅ สุ่มของรางวัลสำเร็จ แต่เกิดข้อผิดพลาดในการแสดงผล"));
        }
    }

    public static async Task<List<GachaItem>> GetGachaItems(MySqlConnection conn, string boxId)
    {
        var items = new List<GachaItem>();
        var itemCmd = new MySqlCommand(
            "SELECT id, name, image_url, is_guaranteed, command, rarity, probability " + // เพิ่ม probability
            "FROM gacha_items WHERE box_id = @boxId",
            conn);
        itemCmd.Parameters.AddWithValue("@boxId", boxId);

        using (var itemReader = await itemCmd.ExecuteReaderAsync())
        {
            while (await itemReader.ReadAsync())
            {
                items.Add(new GachaItem
                {
                    Id = itemReader.GetInt32("id"),
                    Name = itemReader.GetString("name"),
                    ImageUrl = itemReader.GetString("image_url"),
                    IsGuaranteed = itemReader.GetBoolean("is_guaranteed"),
                    Command = itemReader.GetString("command"),
                    Rarity = itemReader.GetInt32("rarity"),
                    Probability = itemReader.GetInt32("probability") // อ่านค่า probability
                });
            }
        }
        return items;
    }

    public static async Task<bool> CheckItemExists(MySqlConnection conn, int itemId)
    {
        var checkCmd = new MySqlCommand(
            "SELECT COUNT(*) FROM gacha_items WHERE id = @itemId",
            conn);
        checkCmd.Parameters.AddWithValue("@itemId", itemId);
        var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
        return count > 0;
    }

    public static async Task<string> GetBoxName(MySqlConnection conn, string boxId)
    {
        var boxCmd = new MySqlCommand(
            "SELECT name FROM gacha_boxes WHERE id = @boxId",
            conn);
        boxCmd.Parameters.AddWithValue("@boxId", boxId);
        return (await boxCmd.ExecuteScalarAsync())?.ToString();
    }

    public static (List<GachaItem> Results, int UpdatedGuaranteeCounter) RollItems(List<GachaItem> items, int rollCount, int initialGuaranteeCounter, MySqlConnection conn)
    {
        var results = new List<GachaItem>();
        var random = new Random();
        int currentCounter = initialGuaranteeCounter;

        for (int i = 0; i < rollCount; i++)
        {
            currentCounter++;
            bool isGuaranteedRoll = currentCounter >= 10;

            var pool = isGuaranteedRoll
                ? items.Where(x => x.IsGuaranteed).ToList()
                : items;

            if (!pool.Any()) pool = items;

            // สุ่มตามน้ำหนัก
            GachaItem selectedItem = SelectItemByWeight(pool, random);

            results.Add(selectedItem);

            if (isGuaranteedRoll || selectedItem.IsGuaranteed)
            {
                currentCounter = 0; // รีเซ็ตเคาน์เตอร์
            }
        }

        return (results, currentCounter);
    }

    private static GachaItem SelectItemByWeight(List<GachaItem> items, Random random)
    {
        int totalWeight = items.Sum(i => i.Probability);
        if (totalWeight == 0) return items.First();

        int randomNumber = random.Next(totalWeight);
        int cumulative = 0;

        foreach (var item in items)
        {
            cumulative += item.Probability;
            if (randomNumber < cumulative)
            {
                return item;
            }
        }
        return items.Last();
    }

    public static async Task UpdateRollHistory(MySqlConnection conn, ulong userId, string boxId, int guaranteeCounter, int itemId)
    {
        // ตรวจสอบว่า item_id มีอยู่ในตาราง gacha_items
        if (!await CheckItemExists(conn, itemId))
        {
            throw new Exception($"Item ID {itemId} does not exist in gacha_items");
        }

        var updateCmd = new MySqlCommand(
            "INSERT INTO user_gacha_history (user_id, box_id, item_id, roll_count) " +
            "VALUES (@userId, @boxId, @itemId, @rollCount) " +
            "ON DUPLICATE KEY UPDATE roll_count = VALUES(roll_count)",
            conn);
        updateCmd.Parameters.AddWithValue("@userId", userId);
        updateCmd.Parameters.AddWithValue("@boxId", boxId);
        updateCmd.Parameters.AddWithValue("@itemId", itemId);
        updateCmd.Parameters.AddWithValue("@rollCount", guaranteeCounter);
        await updateCmd.ExecuteNonQueryAsync();
    }

    public static async Task<bool> CanUserAfford(ulong userId, int price, string currencyType, MySqlConnection conn)
{
    if (currencyType == "cash")
    {
        var userCash = await DatabaseHelper.GetUserCash(userId, conn);
        return userCash >= price;
    }
    else
    {
        var userPoints = await DatabaseHelper.GetUserPoints(userId, conn);
        return userPoints >= price;
    }
}

    public static string GetRarityStars(int rarity)
    {
        return new string('⭐', rarity);
    }

    private static async Task<int> GetUserGuaranteeCounter(ulong userId, string boxId)
    {
        using var conn = new MySqlConnection(Config.MySqlConnectionString);
        await conn.OpenAsync();

        try
        {
            // First check if table exists
            var tableCheckCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables " +
                "WHERE table_schema = DATABASE() AND table_name = 'user_box_guarantee'",
                conn);

            var tableExists = Convert.ToInt32(await tableCheckCmd.ExecuteScalarAsync()) > 0;

            if (!tableExists)
            {
                // Create table if it doesn't exist
                var createTableCmd = new MySqlCommand(
                    "CREATE TABLE IF NOT EXISTS `user_box_guarantee` ( " +
                    "`user_id` BIGINT UNSIGNED NOT NULL, " +
                    "`box_id` VARCHAR(255) NOT NULL, " +
                    "`counter` INT NOT NULL DEFAULT 0, " +
                    "`last_updated` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP, " +
                    "PRIMARY KEY (`user_id`, `box_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                    conn);

                await createTableCmd.ExecuteNonQueryAsync();
                return 0; // Return default counter for new table
            }

            // Get the counter
            var getCounterCmd = new MySqlCommand(
                "SELECT counter FROM user_box_guarantee WHERE user_id = @userId AND box_id = @boxId",
                conn);
            getCounterCmd.Parameters.AddWithValue("@userId", userId);
            getCounterCmd.Parameters.AddWithValue("@boxId", boxId);

            var result = await getCounterCmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting guarantee counter: {ex}");
            return 0; // Fallback to default counter
        }
    }

    private static async Task UpdateUserGuaranteeCounter(ulong userId, string boxId, int counter)
    {
        using var conn = new MySqlConnection(Config.MySqlConnectionString);
        await conn.OpenAsync();

        try
        {
            var cmd = new MySqlCommand(
                "INSERT INTO user_box_guarantee (user_id, box_id, counter) " +
                "VALUES (@userId, @boxId, @counter) " +
                "ON DUPLICATE KEY UPDATE counter = @counter",
                conn);

            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@boxId", boxId);
            cmd.Parameters.AddWithValue("@counter", counter);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating guarantee counter: {ex}");
        }
    }
}