using DSharpPlus.EventArgs;
using DSharpPlus;
using System;
using DSharpPlus.Entities;
using Microsoft.VisualBasic;
using MySql.Data.MySqlClient;
using System.Data;

public static class InteractionCreated
{
    public static async Task OnComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs e)
    {
        switch (e.Id)
        {
            case "check_info_btn":
                await UserCommands.ShowUserInfo(e.Interaction);
                break;

            case "gacha_btn":
                await GachaSystem.ShowGachaBoxes(e.Interaction);
                break;
            //change topup
            case "topup_btn":
                await TopupSystem.ShowTopupModal(e.Interaction);
                break;
            case "admin_add_gacha_box":
                await AdminModels.ShowAddGachaBoxModal(e.Interaction);
                break;

            case "admin_remove_gacha_box":
                await AdminModels.ShowRemoveGachaBoxModal(e.Interaction);
                break;

            case "admin_remove_gacha_item":
                await AdminModels.ShowRemoveGachaItemModal(e.Interaction);
                break;

            case "admin_add_gacha_item":
                await AdminModels.ShowAddGachaItemModal(e.Interaction);
                break;

            case "admin_list_ids":
                await AdminModels.ShowListIdsMenu(e.Interaction);
                break;

            case "admin_add_points":
                await AdminModels.ShowAddPointsModal(e.Interaction);
                break;

            case "shop_btn":
                await ShopSystem.ShowShopCategories(e.Interaction);
                break;

            case "shop_category_select":
                await ShopSystem.ShowShopItems(e.Interaction, e.Values[0]);
                break;

            case var s when s.StartsWith("buy_item_"):
                var itemId = s.Replace("buy_item_", "");
                await ShopSystem.HandleBuyItem(e.Interaction, itemId);
                break;

            case "shop_back_btn":
                await ShopSystem.ShowShopCategories(e.Interaction);
                break;

            case "admin_add_shop_category":
                await ShopSystem.ShowAddShopCategoryModal(e.Interaction);
                break;

            case "admin_add_shop_item":
                await AdminModels.ShowAddShopItemModal(e.Interaction);
                break;

            case "admin_remove_shop_category":
                await AdminModels.ShowRemoveShopCategoryModal(e.Interaction);
                break;

            case "admin_remove_shop_item":
                await AdminModels.ShowRemoveShopItemModal(e.Interaction);
                break;

            case "admin_list_shop_ids":
                await AdminModels.ShowShopListIdsMenu(e.Interaction);
                break;

            case "admin_create_redeem":
                await AdminModels.ShowCreateRedeemModal(e.Interaction);
                break;

            case "admin_redeem_list":
                await AdminModels.ShowRedeemCodeList(e.Interaction);
                break;

            case "admin_redeem_delete":
                await AdminModels.ShowDeleteRedeemCodeModal(e.Interaction);
                break;

            case "add_promotion_btn":
                try
                {
                    await AdminModels.ShowAddPromotionModal(e.Interaction);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling promotion button: {ex}");
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ เกิดข้อผิดพลาดในการเปิดฟอร์มโปรโมชั่น")
                            .AsEphemeral(true));
                }
                break;

            case "redeem_btn":
                await RedeemSystem.ShowRedeemCodeModal(e.Interaction);
                break;

            case "refresh_redeem_list":
                await AdminModels.ShowRedeemCodeList(e.Interaction);
                break;

            case "admin_add_cash":
                await AdminModels.ShowAddCashModal(e.Interaction);
                break;

            case "admin_edit_shop_item":
                // สร้าง Select Menu สำหรับเลือกสินค้าที่จะแก้ไข
                await AdminModels.ShowShopItemSelectionMenu(e.Interaction);
                break;

            case "select_shop_item_to_edit":
                // Remove any previous declaration of selectedItemId in this scope
                if (e.Values != null && e.Values.Any() && int.TryParse(e.Values[0], out int parsedItemId))
                {
                    // Use parsedItemId instead of selectedItemId
                    await AdminModels.ShowEditShopItemModal(e.Interaction, parsedItemId);
                }
                else
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่สามารถระบุ ID สินค้าได้")
                            .AsEphemeral(true));
                }
                break;

            case "admin_edit_gacha_item":
                await AdminModels.ShowGachaItemSelectionMenu(e.Interaction);
                break;

            case "select_gacha_item_to_edit":
                if (e.Values != null && e.Values.Any() && int.TryParse(e.Values[0], out int gachaItemId))
                {
                    await AdminModels.ShowEditGachaItemModal(e.Interaction, gachaItemId);
                }
                else
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ไม่สามารถระบุ ID ไอเทมได้")
                            .AsEphemeral(true));
                }
                break;

            case "select_gacha_box_for_edit":
                if (e.Values != null && e.Values.Any() && int.TryParse(e.Values[0], out int selectedBoxId))
                {
                    await AdminModels.ShowGachaItemsInBox(e.Interaction, selectedBoxId);
                }
                break;

            case "back_to_box_selection":
                await AdminModels.ShowGachaItemSelectionMenu(e.Interaction);
                break;

            case "promotion_btn":
                await PromotionSystem.ShowPromotionDropdown(e.Interaction);
                break;

            case "claim_promotion_btn":
                await PromotionSystem.HandlePromotionClaim(e.Interaction, e.Values[0]);
                break;
        }
        if (e.Id == "gacha_box_select")
        {
            var selectedBoxId = e.Values[0];
            await GachaSystem.ShowGachaBoxDetails(e.Interaction, selectedBoxId);
        }
        if (e.Id.StartsWith("gacha_roll_"))
        {
            await GachaSystem.HandleGachaRoll(e.Interaction, e.Id);
        }
        else if (e.Id == "gacha_back_btn")
        {
            await GachaSystem.ShowGachaBoxes(e.Interaction);
        }
        if (e.Id.StartsWith("retry_check_"))
        {
            await e.Interaction.DeferAsync(true);
            await GachaSystem.HandleGachaRoll(e.Interaction, "retry_check");
        }
        else if (e.Interaction.Data.CustomId == "edit_gacha_item_input_modal")
        {
            try
            {
                string itemIdStr = string.Empty;
                var components = e.Interaction.Data.Components;

                if (string.IsNullOrWhiteSpace(itemIdStr))
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ กรุณากรอก ID ให้ถูกต้อง")
                            .AsEphemeral(true));
                    return;
                }

                if (!string.IsNullOrEmpty(itemIdStr) && int.TryParse(itemIdStr, out int itemId))
                {
                    using var conn = new MySqlConnection(Config.MySqlConnectionString);
                    await conn.OpenAsync();

                    var cmd = new MySqlCommand(
                        "SELECT * FROM gacha_items WHERE id = @itemId",
                        conn);
                    cmd.Parameters.AddWithValue("@itemId", itemId);

                    using var reader = await cmd.ExecuteReaderAsync();

                    if (reader.Read())
                    {
                        // สร้าง Modal แก้ไขข้อมูล
                        var editModal = new DiscordInteractionResponseBuilder()
                            .WithTitle($"✏️ แก้ไขไอเทม ID {itemId}")
                            .WithCustomId($"edit_gacha_item_modal_{itemId}")
                            .AddComponents(new TextInputComponent(
                                label: "ชื่อไอเทม",
                                customId: "edit_gacha_item_name",
                                value: reader.GetString("name"),
                                required: true,
                                style: TextInputStyle.Short,
                                max_length: 50
                            ))
                            .AddComponents(new TextInputComponent(
                                label: "ลิงค์รูปภาพ",
                                customId: "edit_gacha_item_image",
                                value: reader.GetString("image_url"),
                                required: true,
                                style: TextInputStyle.Short,
                                max_length: 200
                            ))
                            .AddComponents(new TextInputComponent(
                                label: "คำสั่ง Minecraft",
                                customId: "edit_gacha_item_command",
                                value: reader.GetString("command"),
                                required: true,
                                style: TextInputStyle.Paragraph,
                                max_length: 1000
                            ))
                            .AddComponents(new TextInputComponent(
                                label: "Rarity,Guaranteed,Probability",
                                customId: "edit_gacha_item_meta",
                                value: $"{reader.GetInt32("rarity")},{reader.GetBoolean("is_guaranteed")},{reader.GetInt32("probability")}",
                                required: true,
                                style: TextInputStyle.Short,
                                max_length: 20
                            ));

                        await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, editModal);
                    }
                    else
                    {
                        await e.Interaction.CreateResponseAsync(
                            InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent($"❌ ไม่พบไอเทม ID {itemId}")
                                .AsEphemeral(true));
                    }
                }
                else
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ID ไอเทมต้องเป็นตัวเลขเท่านั้น")
                            .AsEphemeral(true));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling edit input: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการโหลดข้อมูลไอเทม")
                        .AsEphemeral(true));
            }
        }
        else if (e.Interaction.Data.CustomId == "edit_gacha_item_modal")
        {
            try
            {
                // ใน DSharpPlus เวอร์ชันใหม่ e.Values เป็น array เราต้องรู้ลำดับของ components
                // ถ้ามีแค่ 1 input field ใน modal ให้ใช้ index 0
                string itemIdStr = e.Values[0];

                // ตรวจสอบว่ามีค่าที่กรอกหรือไม่
                if (string.IsNullOrWhiteSpace(itemIdStr))
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ กรุณากรอก ID ไอเทมที่ต้องการแก้ไข")
                            .AsEphemeral(true));
                    return;
                }

                // แปลง ID เป็นตัวเลข
                if (!int.TryParse(itemIdStr, out int itemId) || itemId <= 0)
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("❌ ID ไอเทมต้องเป็นตัวเลขบวกเท่านั้น")
                            .AsEphemeral(true));
                    return;
                }

                // ดึงข้อมูลไอเทมจากฐานข้อมูล
                using var conn = new MySqlConnection(Config.MySqlConnectionString);
                await conn.OpenAsync();

                var cmd = new MySqlCommand(
                    "SELECT * FROM gacha_items WHERE id = @itemId",
                    conn);
                cmd.Parameters.AddWithValue("@itemId", itemId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"❌ ไม่พบไอเทม ID {itemId} ในระบบ")
                            .AsEphemeral(true));
                    return;
                }

                await reader.ReadAsync();

                // สร้าง Modal แก้ไขข้อมูล
                var editModal = new DiscordInteractionResponseBuilder()
                    .WithTitle($"✏️ แก้ไขไอเทม ID {itemId}")
                    .WithCustomId($"edit_gacha_item_confirm_{itemId}")
                    .AddComponents(new TextInputComponent(
                        label: "ชื่อไอเทม",
                        customId: "item_name",
                        value: reader.GetString("name"),
                        required: true,
                        style: TextInputStyle.Short
                    ))
                    .AddComponents(new TextInputComponent(
                        label: "ลิงค์รูปภาพ",
                        customId: "item_image",
                        value: reader.GetString("image_url"),
                        required: false,
                        style: TextInputStyle.Short
                    ))
                    .AddComponents(new TextInputComponent(
                        label: "คำสั่ง Minecraft",
                        customId: "item_command",
                        value: reader.GetString("command"),
                        required: true,
                        style: TextInputStyle.Paragraph
                    ));

                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.Modal,
                    editModal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing edit request: {ex}");
                await e.Interaction.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("❌ เกิดข้อผิดพลาดในการเตรียมฟอร์มแก้ไข")
                        .AsEphemeral(true));
            }
        }
    }

}