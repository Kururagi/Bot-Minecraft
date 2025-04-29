using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus;
using MySql.Data.MySqlClient;
using System.Data;
using System.Linq;

public static class ModalSubmitted
{
    public static async Task OnModalSubmitted(DiscordClient sender, ModalSubmitEventArgs e)
    {
        if (e.Interaction.Data.CustomId == "verify_modal")
        {
            var username = e.Values["mc_username"];
            var password = e.Values["mc_password"];

            // ตรวจสอบในฐานข้อมูล
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "SELECT password FROM authme WHERE username = @username",
                conn);
            cmd.Parameters.AddWithValue("@username", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows)
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่พบชื่อผู้เล่นในระบบ")
                        .AsEphemeral(true));
                return;
            }

            // ดึง passwordHash จากฐานข้อมูล
            await reader.ReadAsync();
            var passwordHash = reader.GetString("password");

            // ตรวจสอบรหัสผ่าน
            if (!VerifySystem.VerifyPassword(password, passwordHash))
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ รหัสผ่านไม่ถูกต้อง")
                        .AsEphemeral(true));
                return;
            }

            // อัปเดตฐานข้อมูล
            await reader.CloseAsync(); // ปิด Reader ก่อนใช้ Command ใหม่
            var updateCmd = new MySqlCommand(
                "UPDATE authme SET discord_id = @discordId WHERE username = @username",
                conn);
            updateCmd.Parameters.AddWithValue("@discordId", e.Interaction.User.Id);
            updateCmd.Parameters.AddWithValue("@username", username);

            await updateCmd.ExecuteNonQueryAsync();

            // ส่งข้อความยืนยัน
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"{e.Interaction.User.Mention} Verify สำเร็จ! บัญชี {username} ถูกผูกกับ Discord แล้ว")
                    .AsEphemeral(true));
        }
        else if (e.Interaction.Data.CustomId == "topup_modal")
        {
            var amount = e.Values["topup_amount"]; // ดึงค่าที่กรอกมา
            await TopupSystem.HandleTopupModal(e.Interaction, amount);
        }
        else if (e.Interaction.Data.CustomId == "add_cash_modal")
        {
            await AdminModels.HandleAddCashModal(e.Interaction, e.Values);
        }
        if (e.Interaction.Data.CustomId == "add_gacha_box_modal")
        {
            var name = e.Values["gacha_box_name"];
            var imageUrl = e.Values["gacha_box_image"];
            var priceInput = e.Values["gacha_box_price"].Trim().ToLower();

            // แยกค่า price และ currency
            var parts = priceInput.Split(' ');
            if (parts.Length != 2 || !decimal.TryParse(parts[0], out var price) || price <= 0)
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ รูปแบบราคาไม่ถูกต้อง ตัวอย่าง: '100 point' หรือ '50 cash'")
                        .AsEphemeral(true));
                return;
            }

            var currencyType = parts[1];
            if (currencyType != "point" && currencyType != "cash")
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ สกุลเงินต้องเป็น 'point' หรือ 'cash' เท่านั้น")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            try
            {
                var cmd = new MySqlCommand(
                    "INSERT INTO gacha_boxes (name, image_url, price, currency_type) " +
                    "VALUES (@name, @imageUrl, @price, @currencyType)",
                    conn);

                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@imageUrl", imageUrl);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@currencyType", currencyType);

                await cmd.ExecuteNonQueryAsync();

                var currencyIcon = currencyType == "cash" ? "💵" : "🪙";
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ เพิ่มกล่องกาชา '{name}' ราคา {price} {currencyIcon} เรียบร้อยแล้ว")
                        .AsEphemeral(true));
            }
            catch (MySqlException ex) when (ex.Number == 1054) // Error: Unknown column
            {
                // ถ้ายังไม่มีคอลัมน์ currency_type ให้ใช้ค่าเริ่มต้นเป็น 'point'
                var fallbackCmd = new MySqlCommand(
                    "INSERT INTO gacha_boxes (name, image_url, price) " +
                    "VALUES (@name, @imageUrl, @price)",
                    conn);

                fallbackCmd.Parameters.AddWithValue("@name", name);
                fallbackCmd.Parameters.AddWithValue("@imageUrl", imageUrl);
                fallbackCmd.Parameters.AddWithValue("@price", price);

                await fallbackCmd.ExecuteNonQueryAsync();

                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ เพิ่มกล่องกาชา '{name}' ราคา {price} point เรียบร้อยแล้ว (ใช้ค่าเริ่มต้น)")
                        .AsEphemeral(true));

                Console.WriteLine("Warning: currency_type column not found, used default 'point'");
            }
        }
        else if (e.Interaction.Data.CustomId == "add_gacha_item_modal")
        {
            var boxIdStr = e.Values["gacha_item_box_id"];
            var name = e.Values["gacha_item_name"];
            var imageUrl = e.Values["gacha_item_image"];
            var command = e.Values["gacha_item_command"];
            var metaData = e.Values["gacha_item_meta"];

            // ตั้งค่าดีฟอลต์หากไม่กรอก meta data
            int rarity = 3;
            bool isGuaranteed = false;
            int probability = 10; // ค่าเริ่มต้น 10%

            // แยกค่า meta data ถ้ามีการกรอก
            if (!string.IsNullOrWhiteSpace(metaData))
            {
                var parts = metaData.Split(',');
                if (parts.Length < 2 || parts.Length > 3)
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ รูปแบบ Meta Data ไม่ถูกต้อง ต้องเป็น『rarity,guaranteed,probability』หรือ『rarity,guaranteed』")
                            .AsEphemeral(true));
                    return;
                }

                // ตรวจสอบ rarity (1-5)
                if (!int.TryParse(parts[0].Trim(), out rarity) || rarity < 1 || rarity > 5)
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ความหายาก (Rarity) ต้องเป็นตัวเลข 1-5")
                            .AsEphemeral(true));
                    return;
                }

                // ตรวจสอบ guaranteed (true/false)
                if (!bool.TryParse(parts[1].Trim(), out isGuaranteed))
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ค่าการันตี (Guaranteed) ต้องเป็น true หรือ false")
                            .AsEphemeral(true));
                    return;
                }

                // ตรวจสอบ probability ถ้ามีการกรอก (1-100)
                if (parts.Length == 3)
                {
                    if (!int.TryParse(parts[2].Trim(), out probability) || probability < 1 || probability > 100)
                    {
                        await e.Interaction.CreateResponseAsync(
                            InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("❌ ความน่าจะเป็น (Probability) ต้องเป็นตัวเลข 1-100")
                                .AsEphemeral(true));
                        return;
                    }
                }
            }

            // ตรวจสอบ Box ID
            if (!int.TryParse(boxIdStr, out var boxId))
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Box ID ต้องเป็นตัวเลข")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            try
            {
                // ตรวจสอบว่า Box มีอยู่จริง
                var checkBoxCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM gacha_boxes WHERE id = @boxId",
                    conn);
                checkBoxCmd.Parameters.AddWithValue("@boxId", boxId);

                if (Convert.ToInt32(await checkBoxCmd.ExecuteScalarAsync()) == 0)
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่พบกล่องกาชาด้วย ID นี้")
                            .AsEphemeral(true));
                    return;
                }

                // เพิ่มไอเทมใหม่
                var cmd = new MySqlCommand(
                    "INSERT INTO gacha_items (box_id, name, image_url, command, rarity, is_guaranteed, probability) " +
                    "VALUES (@boxId, @name, @imageUrl, @command, @rarity, @isGuaranteed, @probability)",
                    conn);

                cmd.Parameters.AddWithValue("@boxId", boxId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@imageUrl", imageUrl);
                cmd.Parameters.AddWithValue("@command", command);
                cmd.Parameters.AddWithValue("@rarity", rarity);
                cmd.Parameters.AddWithValue("@isGuaranteed", isGuaranteed);
                cmd.Parameters.AddWithValue("@probability", probability);

                await cmd.ExecuteNonQueryAsync();

                // สร้าง Embed ยืนยัน
                var embed = new DiscordEmbedBuilder()
                    .WithTitle("✅ เพิ่มไอเทมกาชาสำเร็จ")
                    .AddField("ชื่อไอเทม", name, true)
                    .AddField("กล่อง ID", boxId.ToString(), true)
                    .AddField("ความหายาก", new string('⭐', rarity), true)
                    .AddField("การันตี", isGuaranteed ? "✅" : "❌", true)
                    .AddField("ความน่าจะเป็น", $"{probability}%", true)
                    .WithColor(DiscordColor.Green)
                    .WithThumbnail(imageUrl)
                    .WithFooter($"เพิ่มโดย {e.Interaction.User.Username}");

                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(embed)
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding gacha item: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการเพิ่มไอเทม: " + ex.Message)
                        .AsEphemeral(true));
            }
        }
        else if (e.Interaction.Data.CustomId == "remove_gacha_box_modal")
        {
            var boxIdStr = e.Values["remove_gacha_box_id"];

            if (!int.TryParse(boxIdStr, out var boxId))
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Box ID ต้องเป็นตัวเลข")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // เริ่ม Transaction
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // 1. ลบประวัติการสุ่มที่เกี่ยวข้องกับไอเทมในกล่องนี้
                var deleteHistoryCmd = new MySqlCommand(
                    "DELETE FROM user_gacha_history WHERE item_id IN " +
                    "(SELECT id FROM gacha_items WHERE box_id = @boxId)",
                    conn, transaction);
                deleteHistoryCmd.Parameters.AddWithValue("@boxId", boxId);
                await deleteHistoryCmd.ExecuteNonQueryAsync();

                // 2. ลบไอเทมทั้งหมดในกล่องนี้
                var deleteItemsCmd = new MySqlCommand(
                    "DELETE FROM gacha_items WHERE box_id = @boxId",
                    conn, transaction);
                deleteItemsCmd.Parameters.AddWithValue("@boxId", boxId);
                await deleteItemsCmd.ExecuteNonQueryAsync();

                // 3. ลบกล่องกาชา
                var deleteBoxCmd = new MySqlCommand(
                    "DELETE FROM gacha_boxes WHERE id = @boxId",
                    conn, transaction);
                deleteBoxCmd.Parameters.AddWithValue("@boxId", boxId);

                var rowsAffected = await deleteBoxCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่พบกล่องกาชาที่ต้องการลบ")
                            .AsEphemeral(true));
                    return;
                }

                // Commit Transaction ถ้าทำงานสำเร็จทั้งหมด
                await transaction.CommitAsync();

                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ ลบกล่องกาชา ID {boxId} และข้อมูลที่เกี่ยวข้องเรียบร้อยแล้ว")
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error deleting gacha box: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการลบกล่องกาชา: " + ex.Message)
                        .AsEphemeral(true));
            }
        }
        else if (e.Interaction.Data.CustomId == "remove_gacha_item_modal")
        {
            var itemIdStr = e.Values["remove_gacha_item_id"];

            if (!int.TryParse(itemIdStr, out var itemId))
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Item ID ต้องเป็นตัวเลข")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // เริ่ม Transaction
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // 1. ลบประวัติการสุ่มที่เกี่ยวข้องกับไอเทมนี้
                var deleteHistoryCmd = new MySqlCommand(
                    "DELETE FROM user_gacha_history WHERE item_id = @itemId",
                    conn, transaction);
                deleteHistoryCmd.Parameters.AddWithValue("@itemId", itemId);
                await deleteHistoryCmd.ExecuteNonQueryAsync();

                // 2. ลบไอเทม
                var deleteItemCmd = new MySqlCommand(
                    "DELETE FROM gacha_items WHERE id = @itemId",
                    conn, transaction);
                deleteItemCmd.Parameters.AddWithValue("@itemId", itemId);

                var rowsAffected = await deleteItemCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่พบไอเทมที่ต้องการลบ")
                            .AsEphemeral(true));
                    return;
                }

                // Commit Transaction ถ้าทำงานสำเร็จทั้งหมด
                await transaction.CommitAsync();

                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ ลบไอเทม ID {itemId} และประวัติที่เกี่ยวข้องเรียบร้อยแล้ว")
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error deleting gacha item: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการลบไอเทม")
                        .AsEphemeral(true));
            }
        }
        else if (e.Interaction.Data.CustomId == "add_points_modal")
        {
            var userIdStr = e.Values["target_user_id"];
            var amountStr = e.Values["points_amount"];

            if (!ulong.TryParse(userIdStr, out var userId))
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Discord ID ไม่ถูกต้อง")
                        .AsEphemeral(true));
                return;
            }

            if (!int.TryParse(amountStr, out var amount) || amount <= 0)
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ จำนวน Point ต้องเป็นตัวเลขบวก")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            try
            {
                var cmd = new MySqlCommand(
                    "UPDATE authme SET point = point + @amount WHERE discord_id = @userId",
                    conn);
                cmd.Parameters.AddWithValue("@amount", amount);
                cmd.Parameters.AddWithValue("@userId", userId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่พบผู้ใช้ในระบบ")
                            .AsEphemeral(true));
                    return;
                }

                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ เพิ่ม {amount} Point ให้ผู้ใช้ ID {userId} เรียบร้อยแล้ว")
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding points: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการเพิ่ม Point")
                        .AsEphemeral(true));
            }
        }
        if (e.Interaction.Data.CustomId == "add_shop_category_modal")
        {
            var name = e.Values["shop_category_name"];

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "INSERT INTO shop_categories (name) VALUES (@name)",
                conn);
            cmd.Parameters.AddWithValue("@name", name);

            await cmd.ExecuteNonQueryAsync();
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"✅ เพิ่มหมวดหมู่ '{name}' เรียบร้อยแล้ว")
                    .AsEphemeral(true));
        }
        else if (e.Interaction.Data.CustomId == "add_shop_item_modal")
        {
            try
            {
                // แยกข้อมูลจากช่องเดียว
                var parts = e.Values["shop_price_currency_limit"].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2 || !int.TryParse(parts[0], out int price))
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ รูปแบบราคาผิด ต้องใส่『ราคา』และ『point/cash』 เช่น: 100 point")
                            .AsEphemeral(true));
                    return;
                }

                var currencyType = parts[1].ToLower();
                if (currencyType != "point" && currencyType != "cash")
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ สกุลเงินต้องเป็น『point』หรือ『cash』เท่านั้น")
                            .AsEphemeral(true));
                    return;
                }

                int? purchaseLimit = null;
                if (parts.Length > 2 && int.TryParse(parts[2], out int limit) && limit > 0)
                {
                    purchaseLimit = limit;
                }

                // บันทึกข้อมูลลงฐานข้อมูล
                using var conn = new MySqlConnection(Config.MySqlConnectionString);
                await conn.OpenAsync();

                var cmd = new MySqlCommand(
                    "INSERT INTO shop_items (category_id, name, price, currency_type, image_url, command, purchase_limit) " +
                    "VALUES (@categoryId, @name, @price, @currencyType, @imageUrl, @command, @purchaseLimit)",
                    conn);

                cmd.Parameters.AddWithValue("@categoryId", e.Values["shop_category_id"]);
                cmd.Parameters.AddWithValue("@name", e.Values["shop_item_name"]);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@currencyType", currencyType);
                cmd.Parameters.AddWithValue("@imageUrl", string.IsNullOrEmpty(e.Values["shop_item_image"]) ?
                    "https://i.imgur.com/default.png" : e.Values["shop_item_image"]);
                cmd.Parameters.AddWithValue("@command", e.Values["shop_item_command"]);
                cmd.Parameters.AddWithValue("@purchaseLimit", purchaseLimit ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                var embed = new DiscordEmbedBuilder()
                    .WithTitle("✅ เพิ่มสินค้าสำเร็จ")
                    .WithDescription($"สินค้า『{e.Values["shop_item_name"]}』ถูกเพิ่มแล้ว")
                    .AddField("ราคา", $"{price} {currencyType}", true)
                    .AddField("จำกัดการซื้อ", purchaseLimit.HasValue ? purchaseLimit.Value.ToString() : "ไม่จำกัด", true)
                    .WithColor(DiscordColor.Green);

                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(embed)
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding shop item: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการเพิ่มสินค้า")
                        .AsEphemeral(true));
            }
        }
        else if (e.Interaction.Data.CustomId == "remove_shop_category_modal")
        {
            var categoryIdStr = e.Values["remove_shop_category_id"];

            if (!int.TryParse(categoryIdStr, out var categoryId))
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Category ID ต้องเป็นตัวเลข")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ลบสินค้าในหมวดหมู่นี้
                var deleteItemsCmd = new MySqlCommand(
                    "DELETE FROM shop_items WHERE category_id = @categoryId",
                    conn, transaction);
                deleteItemsCmd.Parameters.AddWithValue("@categoryId", categoryId);
                await deleteItemsCmd.ExecuteNonQueryAsync();

                // ลบหมวดหมู่
                var deleteCategoryCmd = new MySqlCommand(
                    "DELETE FROM shop_categories WHERE id = @categoryId",
                    conn, transaction);
                deleteCategoryCmd.Parameters.AddWithValue("@categoryId", categoryId);

                var rowsAffected = await deleteCategoryCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่พบหมวดหมู่ที่ต้องการลบ")
                            .AsEphemeral(true));
                    return;
                }

                await transaction.CommitAsync();
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"✅ ลบหมวดหมู่ ID {categoryId} และสินค้าที่เกี่ยวข้องเรียบร้อยแล้ว")
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการลบหมวดหมู่")
                        .AsEphemeral(true));
            }
        }
        else if (e.Interaction.Data.CustomId == "remove_shop_item_modal")
        {
            var itemIdStr = e.Values["remove_shop_item_id"];

            if (!int.TryParse(itemIdStr, out var itemId))
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ Item ID ต้องเป็นตัวเลข")
                        .AsEphemeral(true));
                return;
            }

            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                "DELETE FROM shop_items WHERE id = @itemId",
                conn);
            cmd.Parameters.AddWithValue("@itemId", itemId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ ไม่พบสินค้าที่ต้องการลบ")
                        .AsEphemeral(true));
                return;
            }

            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"✅ ลบสินค้า ID {itemId} เรียบร้อยแล้ว")
                    .AsEphemeral(true));
        }
        else if (e.Interaction.Data.CustomId == "redeem_code_modal")
        {
            await RedeemSystem.HandleRedeemCode(e.Interaction, e.Values["redeem_code_input"]);
        }
        else if (e.Interaction.Data.CustomId == "delete_redeem_modal")
        {
            await AdminModels.HandleDeleteRedeemCode(e.Interaction, e.Values["redeem_code_to_delete"]);
        }
        else if (e.Interaction.Data.CustomId == "create_redeem_modal")
        {
            await AdminModels.HandleCreateRedeemCode(e.Interaction, e.Values);
        }
        else if (e.Interaction.Data.CustomId.StartsWith("edit_shop_item_modal_"))
        {
            var itemId = int.Parse(e.Interaction.Data.CustomId.Split('_').Last());
            await AdminModels.HandleEditShopItemModal(e.Interaction, itemId, e.Values);
        }
        else if (e.Interaction.Data.CustomId.StartsWith("edit_gacha_item_modal_"))
        {
            // Extract the ID safely
            if (int.TryParse(e.Interaction.Data.CustomId.Split('_').Last(), out int modalItemId))
            {
                await AdminModels.HandleEditGachaItemModal(e.Interaction, modalItemId, e.Values);
            }
            else
            {
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ รูปแบบ ID ไม่ถูกต้อง")
                        .AsEphemeral(true));
            }
        }
        else if (e.Interaction.Data.CustomId == "add_promotion_modal")
        {
            await AdminModels.HandleAddPromotionModal(e.Interaction, e.Values);
        }

    }
}