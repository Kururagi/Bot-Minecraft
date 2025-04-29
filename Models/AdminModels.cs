using DSharpPlus.Entities;
using DSharpPlus;
using System.Text;
using DSharpPlus.EventArgs;
using MySql.Data.MySqlClient;
using System.Data;
using Rcon;
using DSharpPlus.Exceptions;
using static System.Runtime.InteropServices.JavaScript.JSType;

public static class AdminModels
{
    static RconClient rcon;
    public static async Task ShowAddPointsModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("เพิ่ม Point ให้ผู้ใช้")
            .WithCustomId("add_points_modal")
            .AddComponents(new TextInputComponent(
                label: "Discord ID ผู้ใช้",
                customId: "target_user_id",
                placeholder: "กรอก Discord ID ของผู้ใช้",
                required: true,
                style: TextInputStyle.Short))
            .AddComponents(new TextInputComponent(
                label: "จำนวน Point",
                customId: "points_amount",
                placeholder: "กรอกจำนวน Point ที่ต้องการเพิ่ม",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task ShowAddGachaBoxModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("Add Gacha Box")
            .WithCustomId("add_gacha_box_modal")
            .AddComponents(new TextInputComponent(
                label: "ชื่อกล่อง",
                customId: "gacha_box_name",
                placeholder: "กรอกชื่อกล่องกาชา",
                required: true,
                style: TextInputStyle.Short))
            .AddComponents(new TextInputComponent(
                label: "ลิงค์รูปภาพ",
                customId: "gacha_box_image",
                placeholder: "กรอก URL รูปภาพกล่อง",
                required: true,
                style: TextInputStyle.Short))
            .AddComponents(new TextInputComponent(
                label: "ราคา (เช่น 100 point หรือ 50 cash)",
                customId: "gacha_box_price",
                placeholder: "ตัวอย่าง: 100 point หรือ 50 cash",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task ShowAddGachaItemModal(DiscordInteraction interaction)
    {
        try
        {
            var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("➕ เพิ่มไอเทมกาชา")
            .WithCustomId("add_gacha_item_modal")
            .AddComponents(new TextInputComponent(
                label: "Box ID",
                customId: "gacha_item_box_id",
                placeholder: "กรอก ID กล่องกาชา (ดูได้จาก /listids)",
                required: true,
                style: TextInputStyle.Short,
                min_length: 1,
                max_length: 10
            ))
            .AddComponents(new TextInputComponent(
                label: "ชื่อไอเทม",
                customId: "gacha_item_name",
                placeholder: "กรอกชื่อไอเทม (ไม่เกิน 50 ตัวอักษร)",
                required: true,
                style: TextInputStyle.Short,
                min_length: 1,
                max_length: 50
            ))
            .AddComponents(new TextInputComponent(
                label: "ลิงค์รูปภาพ",
                customId: "gacha_item_image",
                placeholder: "https://example.com/image.png",
                required: true,
                style: TextInputStyle.Short,
                min_length: 10,
                max_length: 200
            ))
            .AddComponents(new TextInputComponent(
                label: "คำสั่ง Minecraft",
                customId: "gacha_item_command",
                placeholder: "give {username} diamond 1",
                required: true,
                style: TextInputStyle.Paragraph,
                min_length: 5,
                max_length: 1000
            ))
            .AddComponents(new TextInputComponent(
                label: "Rarity,Guaranteed,Probability",
                customId: "gacha_item_meta",
                placeholder: "3,false,15 (1-5,true/false,1-100)",
                required: true,
                style: TextInputStyle.Short,
                min_length: 3,
                max_length: 20
            ));

            // Respond with the modal
            await interaction.CreateResponseAsync(
                InteractionResponseType.Modal,
                modal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ShowAddGachaItemModal: {ex}");

            // Fallback response
            try
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการแสดงฟอร์ม")
                        .AsEphemeral(true));
            }
            catch
            {
                // If everything fails, log to console
                Console.WriteLine("Complete failure to send error message");
            }
        }
    }

    public static async Task ShowRemoveGachaBoxModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("Remove Gacha Box")
            .WithCustomId("remove_gacha_box_modal")
            .AddComponents(new TextInputComponent(
                label: "Box ID",
                customId: "remove_gacha_box_id",
                placeholder: "กรอก ID กล่องที่ต้องการลบ",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task ShowRemoveGachaItemModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("Remove Gacha Item")
            .WithCustomId("remove_gacha_item_modal")
            .AddComponents(new TextInputComponent(
                label: "Item ID",
                customId: "remove_gacha_item_id",
                placeholder: "กรอก ID ไอเทมที่ต้องการลบ",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task ShowListIdsMenu(DiscordInteraction interaction)
    {
        try
        {
            await interaction.DeferAsync(true);

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลกล่องกาชา
            var boxes = new List<GachaBox>();
            var boxCmd = new MySqlCommand("SELECT id, name FROM gacha_boxes ORDER BY id", conn);
            using (var boxReader = await boxCmd.ExecuteReaderAsync())
            {
                while (await boxReader.ReadAsync())
                {
                    boxes.Add(new GachaBox
                    {
                        Id = boxReader.GetInt32("id"),
                        Name = boxReader.GetString("name")
                    });
                }
            }

            // ดึงข้อมูลไอเทมกาชา
            var items = new List<GachaItem>();
            var itemCmd = new MySqlCommand("SELECT id, box_id, name FROM gacha_items ORDER BY box_id, id", conn);
            using (var itemReader = await itemCmd.ExecuteReaderAsync())
            {
                while (await itemReader.ReadAsync())
                {
                    items.Add(new GachaItem
                    {
                        Id = itemReader.GetInt32("id"),
                        BoxId = itemReader.GetInt32("box_id"),
                        Name = itemReader.GetString("name")
                    });
                }
            }

            // สร้าง Embed สำหรับกล่องกาชา
            var boxEmbed = new DiscordEmbedBuilder()
                .WithTitle("📦 รายการกล่องกาชา")
                .WithColor(DiscordColor.Blue);

            var boxInfo = new StringBuilder();
            foreach (var box in boxes)
            {
                boxInfo.AppendLine($"🆔 **{box.Id}** - {box.Name}");
            }

            boxEmbed.WithDescription(boxInfo.ToString());

            // สร้าง Embed สำหรับไอเทมกาชา
            var itemEmbed = new DiscordEmbedBuilder()
                .WithTitle("🎁 รายการไอเทมกาชา")
                .WithColor(DiscordColor.Green);

            var itemInfo = new StringBuilder();
            var currentBoxId = -1;

            foreach (var item in items)
            {
                if (item.BoxId != currentBoxId)
                {
                    itemInfo.AppendLine($"\n**กล่อง ID {item.BoxId}**");
                    currentBoxId = item.BoxId;
                }
                itemInfo.AppendLine($"└ 🆔 {item.Id} - {item.Name}");
            }

            itemEmbed.WithDescription(itemInfo.ToString());

            // ส่ง Embed ทั้งสองพร้อมปุ่มกลับ
            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .AddEmbed(boxEmbed)
                    .AddEmbed(itemEmbed));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ShowListIdsMenu: {ex}");
            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการดึงข้อมูล")
                    );
        }
    }

    public static async Task ShowAddShopItemModal(DiscordInteraction interaction)
    {
        try
        {
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle("➕ เพิ่มสินค้าใหม่")
                .WithCustomId("add_shop_item_modal")
                .AddComponents(
                    new TextInputComponent(
                        label: "Category ID",
                        customId: "shop_category_id",
                        placeholder: "กรอก ID หมวดหมู่ (ตัวเลขเท่านั้น)",
                        required: true,
                        style: TextInputStyle.Short
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "ชื่อสินค้า",
                        customId: "shop_item_name",
                        placeholder: "กรอกชื่อสินค้า (ไม่เกิน 50 ตัวอักษร)",
                        required: true,
                        style: TextInputStyle.Short,
                        max_length: 50
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "ราคา / สกุลเงิน / จำกัดการซื้อ",
                        customId: "shop_price_currency_limit",
                        placeholder: "เช่น: 100 point 1 (ถ้าไม่จำกัดให้เว้นไว้ เช่น '100 point')",
                        required: true,
                        style: TextInputStyle.Short,
                        max_length: 30
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "ลิงค์รูปภาพ",
                        customId: "shop_item_image",
                        placeholder: "https://example.com/image.png",
                        required: false,
                        style: TextInputStyle.Short,
                        max_length: 200
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "คำสั่ง Minecraft",
                        customId: "shop_item_command",
                        placeholder: "ใช้ {username} สำหรับชื่อผู้เล่น",
                        required: true,
                        style: TextInputStyle.Paragraph
                    ));

            await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating modal: {ex}");

            try
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่สามารถสร้างฟอร์มได้ โปรดลองใหม่ภายหลัง")
                        .AsEphemeral(true));
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"Failed to send error message: {innerEx}");
            }
        }
    }


    public static async Task ShowRemoveShopCategoryModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("ลบหมวดหมู่ร้านค้า")
            .WithCustomId("remove_shop_category_modal")
            .AddComponents(new TextInputComponent(
                label: "Category ID",
                customId: "remove_shop_category_id",
                placeholder: "กรอก ID หมวดหมู่ที่ต้องการลบ",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task ShowRemoveShopItemModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("ลบสินค้า")
            .WithCustomId("remove_shop_item_modal")
            .AddComponents(new TextInputComponent(
                label: "Item ID",
                customId: "remove_shop_item_id",
                placeholder: "กรอก ID สินค้าที่ต้องการลบ",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task ShowShopListIdsMenu(DiscordInteraction interaction)
    {
        try
        {
            await interaction.DeferAsync(true);

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลหมวดหมู่
            var categories = new List<ShopCategory>();
            var categoryCmd = new MySqlCommand("SELECT id, name FROM shop_categories ORDER BY id", conn);
            using (var categoryReader = await categoryCmd.ExecuteReaderAsync())
            {
                while (await categoryReader.ReadAsync())
                {
                    categories.Add(new ShopCategory
                    {
                        Id = categoryReader.GetInt32("id"),
                        Name = categoryReader.GetString("name")
                    });
                }
            }

            // ดึงข้อมูลสินค้า
            var items = new List<ShopItem>();
            var itemCmd = new MySqlCommand("SELECT id, category_id, name, price FROM shop_items ORDER BY category_id, id", conn);
            using (var itemReader = await itemCmd.ExecuteReaderAsync())
            {
                while (await itemReader.ReadAsync())
                {
                    items.Add(new ShopItem
                    {
                        Id = itemReader.GetInt32("id"),
                        CategoryId = itemReader.GetInt32("category_id"),
                        Name = itemReader.GetString("name"),
                        Price = itemReader.GetInt32("price")
                    });
                }
            }

            // สร้าง Embed สำหรับหมวดหมู่
            var categoryEmbed = new DiscordEmbedBuilder()
                .WithTitle("📦 รายการหมวดหมู่ร้านค้า")
                .WithColor(DiscordColor.Blue);

            var categoryInfo = new StringBuilder();
            foreach (var category in categories)
            {
                categoryInfo.AppendLine($"🆔 **{category.Id}** - {category.Name}");
            }

            categoryEmbed.WithDescription(categoryInfo.ToString());

            // สร้าง Embed สำหรับสินค้า
            var itemEmbed = new DiscordEmbedBuilder()
                .WithTitle("🎁 รายการสินค้า")
                .WithColor(DiscordColor.Green);

            var itemInfo = new StringBuilder();
            var currentCategoryId = -1;

            foreach (var item in items)
            {
                if (item.CategoryId != currentCategoryId)
                {
                    var category = categories.FirstOrDefault(c => c.Id == item.CategoryId);
                    itemInfo.AppendLine($"\n**หมวดหมู่ ID {item.CategoryId}** ({category?.Name ?? "Unknown"})");
                    currentCategoryId = item.CategoryId;
                }
                itemInfo.AppendLine($"└ 🆔 {item.Id} - {item.Name} ({item.Price} Point)");
            }

            itemEmbed.WithDescription(itemInfo.ToString());

            // ส่ง Embed ทั้งสองพร้อมปุ่มกลับ
            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .AddEmbed(categoryEmbed)
                    .AddEmbed(itemEmbed));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการดึงข้อมูล"));
        }
    }

    public static async Task ShowRedeemCodeList(DiscordInteraction interaction)
    {
        try
        {
            await interaction.DeferAsync(true); // รอการประมวลผล

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลโค้ดทั้งหมด (ล่าสุด 20 รายการ)
            var cmd = new MySqlCommand(
                "SELECT code, reward_type, reward_value, max_uses, use_count, " +
                "expires_at, created_at, is_single_use " +
                "FROM redeem_codes ORDER BY created_at DESC LIMIT 20",
                conn);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("📋 รายการ Redeem Codes")
                .WithColor(DiscordColor.Blue)
                .WithFooter($"แสดงผลล่าสุด {DateTime.Now:yyyy-MM-dd HH:mm}");

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows)
            {
                embed.WithDescription("⛔ ยังไม่มีโค้ดในระบบ");
            }
            else
            {
                while (await reader.ReadAsync())
                {
                    var code = reader["code"].ToString();
                    var rewardType = reader["reward_type"].ToString();
                    var rewardValue = reader["reward_value"].ToString();
                    var maxUses = Convert.ToInt32(reader["max_uses"]);
                    var useCount = Convert.ToInt32(reader["use_count"]);
                    var expiresAt = reader["expires_at"] is DBNull ?
                        "ไม่มีวันหมดอายุ" :
                        Convert.ToDateTime(reader["expires_at"]).ToString("yyyy-MM-dd");
                    var isSingleUse = Convert.ToBoolean(reader["is_single_use"]);
                    var createdDate = Convert.ToDateTime(reader["created_at"]).ToString("yyyy-MM-dd");

                    // สถานะโค้ด
                    string status;
                    if (maxUses > 0 && useCount >= maxUses)
                        status = "❌ หมดแล้ว";
                    else if (reader["expires_at"] is not DBNull && Convert.ToDateTime(reader["expires_at"]) < DateTime.Now)
                        status = "❌ หมดอายุ";
                    else
                        status = "✅ ใช้ได้";

                    embed.AddField(
                        $"🎟️ {code} ({status})",
                        $"**ประเภท:** {rewardType}\n" +
                        $"**มูลค่า:** {rewardValue}\n" +
                        $"**ใช้แล้ว:** {useCount}/{maxUses}\n" +
                        $"**หมดอายุ:** {expiresAt}\n" +
                        $"**สร้างเมื่อ:** {createdDate}\n" +
                        $"**ใช้ซ้ำได้:** {(isSingleUse ? "❌" : "✅")}",
                        true);
                }
            }

            // สร้างปุ่ม Refresh
            var refreshButton = new DiscordButtonComponent(
                ButtonStyle.Secondary,
                "refresh_redeem_list",
                "รีเฟรช",
                emoji: new DiscordComponentEmoji("🔄"));

            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .AddEmbed(embed)
                    .AddComponents(refreshButton));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing redeem list: {ex}");
            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการแสดงรายการโค้ด"));
        }
    }

    public static async Task ShowDeleteRedeemCodeModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("ลบ Redeem Code")
            .WithCustomId("delete_redeem_modal")
            .AddComponents(new TextInputComponent(
                label: "กรอกรหัสที่ต้องการลบ",
                customId: "redeem_code_to_delete",
                placeholder: "เช่น: SUMMER2023",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task HandleDeleteRedeemCode(DiscordInteraction interaction, string code)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // เริ่ม Transaction
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ลบประวัติการใช้งาน
                var deleteHistoryCmd = new MySqlCommand(
                    "DELETE FROM user_redeemed_codes WHERE code = @code",
                    conn, transaction);
                deleteHistoryCmd.Parameters.AddWithValue("@code", code);
                await deleteHistoryCmd.ExecuteNonQueryAsync();

                // ลบโค้ด
                var deleteCodeCmd = new MySqlCommand(
                    "DELETE FROM redeem_codes WHERE code = @code",
                    conn, transaction);
                deleteCodeCmd.Parameters.AddWithValue("@code", code);

                int rowsAffected = await deleteCodeCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    await interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่พบโค้ดที่ต้องการลบ")
                            .AsEphemeral(true));
                    return;
                }

                await transaction.CommitAsync();

                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ ลบโค้ด `{code}` เรียบร้อยแล้ว")
                        .AsEphemeral(true));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting redeem code: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการลบโค้ด")
                    .AsEphemeral(true));
        }
    }

    public static async Task ShowCreateRedeemModal(DiscordInteraction interaction)
    {
        try
        {
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle("สร้าง Redeem Code")
                .WithCustomId("create_redeem_modal")
                .AddComponents(new TextInputComponent(
                    label: "รหัสโค้ด (เว้นว่างเพื่อสร้างอัตโนมัติ)",
                    customId: "redeem_code",
                    placeholder: "เช่น: SUMMER2023 หรือเว้นว่างเพื่อสร้างรหัสสุ่ม",
                    required: false,  // เปลี่ยนจาก true เป็น false
                    style: TextInputStyle.Short,
                    max_length: 20))
                .AddComponents(new TextInputComponent(
                    label: "ประเภทของรางวัล",
                    customId: "reward_type",
                    placeholder: "command/point/cash",
                    required: true,
                    style: TextInputStyle.Short,
                    max_length: 10))
                .AddComponents(new TextInputComponent(
                    label: "มูลค่ารางวัล",
                    customId: "reward_value",
                    placeholder: "100 หรือ give {username} diamond 1",
                    required: true,
                    style: TextInputStyle.Paragraph,
                    max_length: 400))
                .AddComponents(new TextInputComponent(
                    label: "จำนวนครั้งที่ใช้ได้",
                    customId: "max_uses",
                    placeholder: "0 = ไม่จำกัด",
                    required: true,
                    style: TextInputStyle.Short,
                    max_length: 5))
                .AddComponents(new TextInputComponent(
                    label: "วันหมดอายุ (YYYY-MM-DD)",
                    customId: "expiry_date",
                    placeholder: "เว้นว่างหากไม่มีวันหมดอายุ",
                    required: false,
                    style: TextInputStyle.Short,
                    max_length: 10));

            await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating redeem modal: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ ไม่สามารถสร้างฟอร์มได้")
                    .AsEphemeral(true));
        }
    }

    public static async Task HandleCreateRedeemCode(DiscordInteraction interaction, IReadOnlyDictionary<string, string> values)
    {
        try
        {
            // สร้างรหัสอัตโนมัติหากไม่กรอก
            string code = string.IsNullOrWhiteSpace(values["redeem_code"])
                ? GenerateRandomCode()
                : values["redeem_code"].ToUpper();

            // ตรวจสอบข้อมูลอื่นๆ
            if (!int.TryParse(values["max_uses"], out int maxUses))
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("⚠️ จำนวนครั้งที่ใช้ได้ต้องเป็นตัวเลข")
                        .AsEphemeral(true));
                return;
            }

            DateTime? expiresAt = null;
            if (!string.IsNullOrEmpty(values["expiry_date"]))
            {
                if (!DateTime.TryParse(values["expiry_date"], out DateTime tempDate))
                {
                    await interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ รูปแบบวันที่ไม่ถูกต้อง ต้องเป็น YYYY-MM-DD")
                            .AsEphemeral(true));
                    return;
                }
                expiresAt = tempDate;
            }

            // บันทึกลงฐานข้อมูล
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "INSERT INTO redeem_codes (code, reward_type, reward_value, max_uses, expires_at) " +
                "VALUES (@code, @type, @value, @maxUses, @expiresAt)",
                conn);

            cmd.Parameters.AddWithValue("@code", code);
            cmd.Parameters.AddWithValue("@type", values["reward_type"]);
            cmd.Parameters.AddWithValue("@value", values["reward_value"]);
            cmd.Parameters.AddWithValue("@maxUses", maxUses);
            cmd.Parameters.AddWithValue("@expiresAt", expiresAt);

            await cmd.ExecuteNonQueryAsync();

            // แสดงผลลัพธ์
            var embed = new DiscordEmbedBuilder()
                .WithTitle("✅ สร้างโค้ดสำเร็จ")
                .AddField("รหัส", code, true)
                .AddField("ประเภท", values["reward_type"], true)
                .AddField("มูลค่า", values["reward_value"], false)
                .AddField("ใช้ได้", maxUses == 0 ? "ไม่จำกัด" : maxUses.ToString(), true)
                .AddField("หมดอายุ", expiresAt?.ToString("yyyy-MM-dd") ?? "ไม่มี", true)
                .WithColor(DiscordColor.Green)
                .WithFooter($"สร้างโดย {interaction.User.Username}");

            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AsEphemeral(true));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("⚠️ มีรหัสนี้อยู่แล้วในระบบ")
                    .AsEphemeral(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating redeem code: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการสร้างโค้ด")
                    .AsEphemeral(true));
        }
    }

    public static string GenerateRandomCode(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public static async Task ShowAddCashModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("Add Cash to User")
            .WithCustomId("add_cash_modal")
            .AddComponents(new TextInputComponent(
                label: "Discord ID ผู้ใช้",
                customId: "target_user_id",
                placeholder: "กรอก Discord ID ของผู้ใช้",
                required: true,
                style: TextInputStyle.Short))
            .AddComponents(new TextInputComponent(
                label: "จำนวน Cash ที่ต้องการเพิ่ม",
                customId: "cash_amount",
                placeholder: "กรอกจำนวน Cash ที่ต้องการเพิ่ม",
                required: true,
                style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    public static async Task HandleAddCashModal(DiscordInteraction interaction, IReadOnlyDictionary<string, string> values)
    {
        try
        {
            if (!ulong.TryParse(values["target_user_id"], out var userId))
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Discord ID ไม่ถูกต้อง")
                        .AsEphemeral(true));
                return;
            }

            if (!decimal.TryParse(values["cash_amount"], out var amount) || amount <= 0)
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ จำนวน Cash ต้องเป็นตัวเลขบวก")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "UPDATE authme SET cash = cash + @amount WHERE discord_id = @userId",
                conn);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@userId", userId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่พบผู้ใช้ในระบบ")
                        .AsEphemeral(true));
                return;
            }

            // ดึงข้อมูลยอดคงเหลือใหม่
            var balanceCmd = new MySqlCommand(
                "SELECT cash FROM authme WHERE discord_id = @userId",
                conn);
            balanceCmd.Parameters.AddWithValue("@userId", userId);
            var newBalance = Convert.ToDecimal(await balanceCmd.ExecuteScalarAsync());

            var embed = new DiscordEmbedBuilder()
                .WithTitle("✅ เพิ่ม Cash สำเร็จ")
                .WithDescription($"เพิ่ม {amount} Cash ให้ผู้ใช้ <@{userId}>")
                .AddField("ยอดคงเหลือใหม่", $"{newBalance:N2} Cash", true)
                .WithColor(DiscordColor.Green)
                .WithFooter($"ดำเนินการโดย {interaction.User.Username}");

            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AsEphemeral(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding cash: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการเพิ่ม Cash")
                    .AsEphemeral(true));
        }
    }

    public static async Task ShowEditShopItemModal(DiscordInteraction interaction, int itemId)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลสินค้าปัจจุบัน
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM shop_items WHERE id = @itemId";
            cmd.Parameters.AddWithValue("@itemId", itemId);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!reader.Read())
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่พบสินค้าด้วย ID นี้")
                        .AsEphemeral(true));
                return;
            }

            // สร้าง Modal และเติมข้อมูลปัจจุบัน
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle($"✏️ แก้ไขสินค้า ID {itemId}")
                .WithCustomId($"edit_shop_item_modal_{itemId}")
                .AddComponents(
                    new TextInputComponent(
                        label: "ชื่อสินค้า",
                        customId: "edit_shop_item_name",
                        value: reader["name"].ToString(),
                        required: true,
                        style: TextInputStyle.Short,
                        max_length: 50
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "ราคาและสกุลเงิน",
                        customId: "edit_shop_item_price_currency",
                        value: $"{reader["price"]} {reader["currency_type"]}",
                        required: true,
                        style: TextInputStyle.Short,
                        max_length: 20
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "ลิงค์รูปภาพ",
                        customId: "edit_shop_item_image",
                        value: reader["image_url"].ToString(),
                        required: false,
                        style: TextInputStyle.Short,
                        max_length: 200
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "คำสั่ง Minecraft",
                        customId: "edit_shop_item_command",
                        value: reader["command"].ToString(),
                        required: true,
                        style: TextInputStyle.Paragraph,
                        max_length: 2000
                    ))
                .AddComponents(
                    new TextInputComponent(
                        label: "จำกัดการซื้อ (0=ไม่จำกัด)",
                        customId: "edit_shop_item_purchase_limit",
                        value: reader.IsDBNull("purchase_limit") ? "0" : reader["purchase_limit"].ToString(),
                        required: false,
                        style: TextInputStyle.Short,
                        max_length: 2
                    ));

            await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading shop item for edit: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการโหลดข้อมูลสินค้า")
                    .AsEphemeral(true));
        }
    }

    public static async Task HandleEditShopItemModal(DiscordInteraction interaction, int itemId, IReadOnlyDictionary<string, string> values)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // สร้างคำสั่ง SQL แบบไดนามิกตามฟิลด์ที่ต้องการอัปเดต
            var updates = new List<string>();
            var updateCmd = conn.CreateCommand();

            // ตรวจสอบและเพิ่มแต่ละฟิลด์ที่จะอัปเดต
            if (values.ContainsKey("edit_shop_item_name"))
            {
                updates.Add("name = @name");
                updateCmd.Parameters.AddWithValue("@name", values["edit_shop_item_name"]);
            }

            if (values.ContainsKey("edit_shop_item_price_currency"))
            {
                var priceCurrency = values["edit_shop_item_price_currency"].Split(' ');
                if (priceCurrency.Length != 2 || !int.TryParse(priceCurrency[0], out int price))
                {
                    await interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ รูปแบบราคาไม่ถูกต้อง")
                            .AsEphemeral(true));
                    return;
                }

                var currencyType = priceCurrency[1].ToLower();
                if (currencyType != "point" && currencyType != "cash")
                {
                    await interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ สกุลเงินต้องเป็น point หรือ cash เท่านั้น")
                            .AsEphemeral(true));
                    return;
                }

                updates.Add("price = @price");
                updates.Add("currency_type = @currencyType");
                updateCmd.Parameters.AddWithValue("@price", price);
                updateCmd.Parameters.AddWithValue("@currencyType", currencyType);
            }

            if (values.ContainsKey("edit_shop_item_image"))
            {
                updates.Add("image_url = @imageUrl");
                updateCmd.Parameters.AddWithValue("@imageUrl", values["edit_shop_item_image"]);
            }

            if (values.ContainsKey("edit_shop_item_command"))
            {
                updates.Add("command = @command");
                updateCmd.Parameters.AddWithValue("@command", values["edit_shop_item_command"]);
            }

            if (values.ContainsKey("edit_shop_item_purchase_limit"))
            {
                int? purchaseLimit = null;
                if (int.TryParse(values["edit_shop_item_purchase_limit"], out int limit) && limit > 0)
                {
                    purchaseLimit = limit;
                }
                updates.Add("purchase_limit = @purchaseLimit");
                updateCmd.Parameters.AddWithValue("@purchaseLimit", purchaseLimit ?? (object)DBNull.Value);
            }

            if (updates.Count == 0)
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("⚠️ ไม่มีข้อมูลที่จะอัปเดต")
                        .AsEphemeral(true));
                return;
            }

            updateCmd.CommandText = $"UPDATE shop_items SET {string.Join(", ", updates)} WHERE id = @itemId";
            updateCmd.Parameters.AddWithValue("@itemId", itemId);

            int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ อัปเดตสินค้า ID {itemId} เรียบร้อยแล้ว")
                        .AsEphemeral(true));
            }
            else
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("⚠️ ไม่สามารถอัปเดตสินค้าได้")
                        .AsEphemeral(true));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error editing shop item: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการแก้ไขสินค้า")
                    .AsEphemeral(true));
        }
    }

    public static async Task ShowShopItemSelectionMenu(DiscordInteraction interaction)
    {
        try
        {
            // รอการ defer ให้เสร็จสมบูรณ์ก่อน
            await interaction.DeferAsync(true);

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name FROM shop_items ORDER BY id LIMIT 25"; // จำกัดจำนวนรายการ

            var options = new List<DiscordSelectComponentOption>();

            using var reader = await cmd.ExecuteReaderAsync();
            while (reader.Read())
            {
                options.Add(new DiscordSelectComponentOption(
                    label: $"{reader["id"]} - {reader["name"]}".Truncate(100), // จำกัดความยาว
                    value: reader["id"].ToString(),
                    description: $"ID: {reader["id"]}".Truncate(100)
                ));
            }

            if (options.Count == 0)
            {
                var errorBuilder = new DiscordWebhookBuilder()
                    .WithContent("⚠️ ไม่มีสินค้าในร้านค้า");

                await interaction.EditOriginalResponseAsync(errorBuilder);
                return;
            }

            var selectMenu = new DiscordSelectComponent(
                customId: "select_shop_item_to_edit",
                placeholder: "เลือกสินค้าที่ต้องการแก้ไข",
                options: options,
                minOptions: 1,
                maxOptions: 1,
                disabled: false
            );

            var responseBuilder = new DiscordWebhookBuilder()
                .WithContent("กรุณาเลือกสินค้าที่ต้องการแก้ไข:")
                .AddComponents(selectMenu);

            // แก้ไขข้อความตอบกลับเดิม
            await interaction.EditOriginalResponseAsync(responseBuilder);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing shop item selection: {ex}");

            try
            {
                // พยายามส่งข้อความแสดงข้อผิดพลาด
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการโหลดรายการสินค้า")
                        .AsEphemeral(true));
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"Error sending error message: {innerEx}");
            }
        }
    }

    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
    }

    public static async Task ShowGachaItemSelectionMenu(DiscordInteraction interaction)
    {
        try
        {
            await interaction.DeferAsync(true);

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลกล่องกาชาทั้งหมด
            var boxes = new List<GachaBox>();
            var boxCmd = new MySqlCommand("SELECT id, name FROM gacha_boxes ORDER BY id", conn);

            using (var boxReader = await boxCmd.ExecuteReaderAsync())
            {
                while (await boxReader.ReadAsync())
                {
                    boxes.Add(new GachaBox
                    {
                        Id = boxReader.GetInt32("id"),
                        Name = boxReader.GetString("name")
                    });
                }
            }

            if (boxes.Count == 0)
            {
                await interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder()
                        .WithContent("⚠️ ไม่พบกล่องกาชาในระบบ"));
                return;
            }

            // สร้าง Select Menu สำหรับเลือกกล่อง
            var boxOptions = boxes.Select(b =>
                new DiscordSelectComponentOption(
                    label: $"{b.Id} - {b.Name.Truncate(50)}",
                    value: b.Id.ToString(),
                    description: $"กล่อง ID: {b.Id}"
                )).ToList();

            var boxSelectMenu = new DiscordSelectComponent(
                "select_gacha_box_for_edit",
                "เลือกกล่องกาชาที่ต้องการ",
                boxOptions,
                minOptions: 1,
                maxOptions: 1
            );

            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("กรุณาเลือกกล่องกาชาที่ต้องการดูไอเทม:")
                    .AddComponents(boxSelectMenu));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing box selection: {ex}");
            await interaction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการโหลดรายการกล่องกาชา"));
        }
    }

    public static async Task ShowGachaItemsInBox(DiscordInteraction interaction, int boxId)
    {
        try
        {
            await interaction.DeferAsync(true);

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // Get box name
            var boxNameCmd = new MySqlCommand("SELECT name FROM gacha_boxes WHERE id = @boxId", conn);
            boxNameCmd.Parameters.AddWithValue("@boxId", boxId);
            var boxName = (await boxNameCmd.ExecuteScalarAsync())?.ToString() ?? "Unknown";

            // Get items in this box
            var items = new List<GachaItem>();
            var itemCmd = new MySqlCommand(
                "SELECT id, name FROM gacha_items WHERE box_id = @boxId ORDER BY id",
                conn);
            itemCmd.Parameters.AddWithValue("@boxId", boxId);

            using var reader = await itemCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new GachaItem
                {
                    Id = reader.GetInt32("id"),
                    Name = reader.GetString("name")
                });
            }

            if (items.Count == 0)
            {
                await interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder()
                        .WithContent($"⚠️ ไม่พบไอเทมในกล่อง {boxName} (ID: {boxId})"));
                return;
            }

            // Create Select Menu options (limit to 25 items)
            var itemOptions = items.Take(25).Select(i =>
                new DiscordSelectComponentOption(
                    label: $"{i.Id} - {i.Name}".Truncate(100),
                    value: i.Id.ToString(),
                    description: $"ID: {i.Id}".Truncate(100)
                )).ToList();

            var backButton = new DiscordButtonComponent(
                ButtonStyle.Secondary,
                "back_to_box_selection",
                "กลับไปเลือกกล่องอื่น",
                emoji: new DiscordComponentEmoji("⬅️"));

            var itemSelectMenu = new DiscordSelectComponent(
                "select_gacha_item_to_edit",
                "เลือกไอเทมที่ต้องการแก้ไข",
                itemOptions,
                minOptions: 1,
                maxOptions: 1
            );

            // Create the response builder
            var response = new DiscordWebhookBuilder()
                .WithContent($"📦 กล่อง: {boxName} (ID: {boxId})\nกรุณาเลือกไอเทมที่ต้องการแก้ไข:");

            // Add components (menu first, then button)
            response.AddComponents(itemSelectMenu);
            response.AddComponents(backButton);

            await interaction.EditOriginalResponseAsync(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing items in box: {ex}");
            try
            {
                await interaction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการโหลดรายการไอเทม"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send error message: {e}");
            }
        }
    }

    public static async Task ShowEditGachaItemModal(DiscordInteraction interaction, int itemId)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // ดึงข้อมูลไอเทมปัจจุบัน
            var cmd = new MySqlCommand(
                "SELECT * FROM gacha_items WHERE id = @itemId",
                conn);
            cmd.Parameters.AddWithValue("@itemId", itemId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.Read())
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่พบไอเทมนี้ในระบบ")
                        .AsEphemeral(true));
                return;
            }

            // สร้าง Modal พร้อมข้อมูลปัจจุบัน
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle($"✏️ แก้ไขไอเทม ID {itemId}")
                .WithCustomId($"edit_gacha_item_modal_{itemId}")
                .AddComponents(new TextInputComponent(
                    label: "ชื่อไอเทม",
                    customId: "gacha_item_name",
                    value: reader.GetString("name"),
                    required: true,
                    style: TextInputStyle.Short,
                    max_length: 50
                ))
                .AddComponents(new TextInputComponent(
                    label: "ลิงค์รูปภาพ",
                    customId: "gacha_item_image",
                    value: reader.GetString("image_url"),
                    required: true,
                    style: TextInputStyle.Short,
                    max_length: 200
                ))
                .AddComponents(new TextInputComponent(
                    label: "คำสั่ง Minecraft",
                    customId: "gacha_item_command",
                    value: reader.GetString("command"),
                    required: true,
                    style: TextInputStyle.Paragraph,
                    max_length: 1000
                ))
                .AddComponents(new TextInputComponent(
                    label: "Rarity,Guaranteed,Probability",
                    customId: "gacha_item_meta",
                    value: $"{reader.GetInt32("rarity")},{reader.GetBoolean("is_guaranteed")},{reader.GetInt32("probability")}",
                    required: true,
                    style: TextInputStyle.Short,
                    max_length: 20
                ));

            await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading item for edit: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการโหลดข้อมูลไอเทม")
                    .AsEphemeral(true));
        }
    }

    public static async Task HandleEditGachaItemModal(DiscordInteraction interaction, int itemId, IReadOnlyDictionary<string, string> values)
    {
        try
        {
            // ตรวจสอบและแปลงข้อมูล Meta
            var metaParts = values["gacha_item_meta"].Split(',');
            if (metaParts.Length != 3 ||
                !int.TryParse(metaParts[0], out int rarity) ||
                !bool.TryParse(metaParts[1], out bool isGuaranteed) ||
                !int.TryParse(metaParts[2], out int probability))
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ รูปแบบ Meta Data ไม่ถูกต้อง")
                        .AsEphemeral(true));
                return;
            }

            // อัปเดตฐานข้อมูล
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "UPDATE gacha_items SET " +
                "name = @name, " +
                "image_url = @imageUrl, " +
                "command = @command, " +
                "rarity = @rarity, " +
                "is_guaranteed = @isGuaranteed, " +
                "probability = @probability " +
                "WHERE id = @itemId",
                conn);

            cmd.Parameters.AddWithValue("@name", values["gacha_item_name"]);
            cmd.Parameters.AddWithValue("@imageUrl", values["gacha_item_image"]);
            cmd.Parameters.AddWithValue("@command", values["gacha_item_command"]);
            cmd.Parameters.AddWithValue("@rarity", rarity);
            cmd.Parameters.AddWithValue("@isGuaranteed", isGuaranteed);
            cmd.Parameters.AddWithValue("@probability", probability);
            cmd.Parameters.AddWithValue("@itemId", itemId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่สามารถอัปเดตข้อมูลได้")
                        .AsEphemeral(true));
                return;
            }

            // สร้าง Embed ยืนยันการแก้ไข
            var embed = new DiscordEmbedBuilder()
                .WithTitle("✅ อัปเดตไอเทมสำเร็จ")
                .AddField("ชื่อ", values["gacha_item_name"], true)
                .AddField("Rarity", new string('⭐', rarity), true)
                .AddField("Guaranteed", isGuaranteed ? "✅" : "❌", true)
                .AddField("Probability", $"{probability}%", true)
                .WithThumbnail(values["gacha_item_image"])
                .WithColor(DiscordColor.Green);

            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AsEphemeral(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating gacha item: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการอัปเดตข้อมูล")
                    .AsEphemeral(true));
        }
    }

    public static async Task HandleAddPromotionModal(DiscordInteraction interaction, IReadOnlyDictionary<string, string> values)
    {
        try
        {
            if (!decimal.TryParse(values["promotion_min_topup"], out var minTopup) || minTopup <= 0)
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ จำนวนเงินขั้นต่ำต้องเป็นตัวเลขบวก")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "INSERT INTO promotions (name, description, min_topup_points, reward_command, image_url) " +
                "VALUES (@name, @desc, @minTopup, @command, @image)",
                conn);

            cmd.Parameters.AddWithValue("@name", values["promotion_name"]);
            cmd.Parameters.AddWithValue("@desc", values["promotion_desc"]);
            cmd.Parameters.AddWithValue("@minTopup", minTopup);
            cmd.Parameters.AddWithValue("@command", values["promotion_command"]);
            cmd.Parameters.AddWithValue("@image", values["promotion_image"]);

            await cmd.ExecuteNonQueryAsync();

            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"✅ เพิ่มโปรโมชั่น '{values["promotion_name"]}' เรียบร้อยแล้ว")
                    .AsEphemeral(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding promotion: {ex}");
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ เกิดข้อผิดพลาดในการเพิ่มโปรโมชั่น")
                    .AsEphemeral(true));
        }
    }

    public static async Task ShowAddPromotionModal(DiscordInteraction interaction)
    {
        try
        {
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle("เพิ่มโปรโมชั่นใหม่")
                .WithCustomId("add_promotion_modal")
                .AddComponents(new TextInputComponent(
                    label: "ชื่อโปรโมชั่น",
                    customId: "promotion_name",
                    placeholder: "โปรโมชั่นวันเกิด",
                    required: true,
                    style: TextInputStyle.Short,
                    min_length: 1,
                    max_length: 100
                ))
                .AddComponents(new TextInputComponent(
                    label: "คำอธิบาย",
                    customId: "promotion_desc",
                    placeholder: "รับของรางวัลวันเกิดพิเศษ!",
                    required: true,
                    style: TextInputStyle.Paragraph,
                    min_length: 1,
                    max_length: 1000
                ))
                .AddComponents(new TextInputComponent(
                    label: "ยอดเติมขั้นต่ำ",
                    customId: "promotion_min_topup",
                    placeholder: "500",
                    required: true,
                    style: TextInputStyle.Short,
                    min_length: 1,
                    max_length: 10
                ))
                .AddComponents(new TextInputComponent(
                    label: "คำสั่งเมื่อรับ (ใช้ {player} สำหรับชื่อผู้เล่น)",
                    customId: "promotion_command",
                    placeholder: "give {player} minecraft:cake 1",
                    required: true,
                    style: TextInputStyle.Paragraph,
                    min_length: 1,
                    max_length: 500
                ))
                .AddComponents(new TextInputComponent(
                    label: "ลิงค์รูปภาพ (ไม่จำเป็น)",
                    customId: "promotion_image",
                    placeholder: "https://example.com/image.png",
                    required: false,
                    style: TextInputStyle.Short,
                    min_length: 0,
                    max_length: 255
                ));

            await interaction.CreateResponseAsync(
                InteractionResponseType.Modal,
                modal
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing promotion modal: {ex}");

            // ลองตอบสนองด้วยวิธีอื่นถ้าสร้าง Modal ไม่ได้
            try
            {
                await interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่สามารถแสดงฟอร์มเพิ่มโปรโมชั่นได้")
                        .AsEphemeral(true));
            }
            catch
            {
                // ถ้าไม่สามารถตอบสนองได้เลย
                Console.WriteLine("Failed to send error response for promotion modal");
            }
        }
    }
}