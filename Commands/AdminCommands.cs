using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus;
using static System.Runtime.InteropServices.JavaScript.JSType;

public static class AdminCommands
{
    private static DiscordClient _discord;

    // Add initialization method
    public static void Initialize(DiscordClient client)
    {
        _discord = client;
    }

    public static async Task AdminPanelCreate(MessageCreateEventArgs e)
    {
        try
        {
            // Check if client is initialized
            if (_discord == null)
            {
                await e.Message.RespondAsync("❌ ระบบบอทกำลังเริ่มต้น กรุณาลองใหม่ในภายหลัง");
                return;
            }

            var member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (member == null)
            {
                await e.Message.RespondAsync("❌ ไม่พบข้อมูลผู้ใช้ในเซิร์ฟเวอร์นี้");
                return;
            }

            // ตรวจสอบสิทธิ์ผู้ใช้
            if (!member.Permissions.HasPermission(Permissions.Administrator))
            {
                await e.Message.RespondAsync("❌ คุณไม่มีสิทธิ์ใช้คำสั่งนี้");
                return;
            }

            // แยกคำสั่งและ channel ID
            var args = e.Message.Content.Split(' ');
            if (args.Length < 2)
            {
                await e.Message.RespondAsync("❌ ใช้คำสั่งไม่ถูกต้อง ตัวอย่าง: `-adminpanelcreate {channelid}`");
                return;
            }

            // ดึง channel ID จากคำสั่ง
            if (!ulong.TryParse(args[1], out var channelId))
            {
                await e.Message.RespondAsync("❌ Channel ID ไม่ถูกต้อง");
                return;
            }

            // หาช่องที่ระบุ
            var channel = await _discord.GetChannelAsync(channelId);
            if (channel == null)
            {
                await e.Message.RespondAsync("❌ ไม่พบช่องที่ระบุ");
                return;
            }

            // สร้าง Admin Panel
            var embed = new DiscordEmbedBuilder()
                .WithTitle("🛠️ Admin Panel")
                .WithDescription("เลือกปุ่มด้านล่างเพื่อดำเนินการ")
                .WithColor(DiscordColor.Red)
                .AddField("Gacha Management", "จัดการระบบกาชา", false)
                .AddField("Add Gacha Box", "เพิ่มกล่องกาชาใหม่", true)
                .AddField("Add Gacha Item", "เพิ่มไอเทมในกล่องกาชา", true)
                .AddField("Edit Gacha Item", "แก้ไขไอเทมกาชา", true) // เพิ่มฟิลด์ใหม่
                .AddField("Remove Gacha Box", "ลบกล่องกาชา", true)
                .AddField("Remove Gacha Item", "ลบไอเทมจากกล่องกาชา", true)
                .AddField("List IDs", "แสดงรายการ ID ทั้งหมด", true)
                .AddField("Shop Management", "จัดการร้านค้า", false)
                .AddField("Add Shop Category", "เพิ่มหมวดหมู่ร้านค้า", true)
                .AddField("Add Shop Item", "เพิ่มสินค้าใหม่", true)
                .AddField("Edit Shop Item", "แก้ไขสินค้า", true)
                .AddField("Remove Shop Category", "ลบหมวดหมู่", true)
                .AddField("Remove Shop Item", "ลบสินค้า", true)
                .AddField("List Shop IDs", "แสดงรายการ ID", true)
                .AddField("User Management", "จัดการผู้ใช้", false)
                .AddField("Add Points", "เพิ่ม Point ให้ผู้ใช้", true)
                .AddField("Add Cash", "เพิ่ม Cash ให้ผู้ใช้", true)
                .AddField("Redeem System", "จัดการระบบ Redeem", false)
                .AddField("Create Redeem", "สร้างโค้ด Redeem", true)
                .AddField("Redeem List", "รายการโค้ด", true)
                .AddField("Delete Redeem", "ลบโค้ด", true)
                .WithFooter("Admin Panel | Kururagi Seimei");

            // แถวที่ 1 - จัดการกาชา
            var row1Buttons = new List<DiscordButtonComponent>
            {
                new DiscordButtonComponent(ButtonStyle.Primary, "admin_add_gacha_box", "Gacha Add Box"),
                new DiscordButtonComponent(ButtonStyle.Success, "admin_add_gacha_item", "Gacha Add Item"),
                new DiscordButtonComponent(ButtonStyle.Secondary, "admin_edit_gacha_item", "Gacha Edit Item"),
                new DiscordButtonComponent(ButtonStyle.Danger, "admin_remove_gacha_box", "Gacha Remove Box"),
                new DiscordButtonComponent(ButtonStyle.Danger, "admin_remove_gacha_item", "Gacha Remove Item")
            };

            // แถวที่ 2 - จัดการร้านค้า
            var row2Buttons = new List<DiscordButtonComponent>
            {
                new DiscordButtonComponent(ButtonStyle.Secondary, "admin_list_ids", "List Gacha IDs"),
                new DiscordButtonComponent(ButtonStyle.Primary, "admin_add_shop_category", "Add Category Shop"),
                new DiscordButtonComponent(ButtonStyle.Success, "admin_add_shop_item", "Add Shop Item"),
                new DiscordButtonComponent(ButtonStyle.Primary, "admin_edit_shop_item", "Edit Shop Item"),
                new DiscordButtonComponent(ButtonStyle.Danger, "admin_remove_shop_category", "Remove Category Shop")
            };

            // แถวที่ 3 - จัดการผู้ใช้และ Redeem
            var row3Buttons = new List<DiscordButtonComponent>
            {
                new DiscordButtonComponent(ButtonStyle.Danger, "admin_remove_shop_item", "Remove Shop Item"),
                new DiscordButtonComponent(ButtonStyle.Secondary, "admin_list_shop_ids", "List Shop IDs"),
                new DiscordButtonComponent(ButtonStyle.Primary, "admin_add_points", "Add Points"),
                new DiscordButtonComponent(ButtonStyle.Primary, "admin_add_cash", "Add Cash", emoji: new DiscordComponentEmoji("💰")),
                new DiscordButtonComponent(ButtonStyle.Primary, "admin_create_redeem", "Create Redeem", emoji: new DiscordComponentEmoji("🎫"))
            };

            // แถวที่ 4 - จัดการ Redeem
            var row4Buttons = new List<DiscordButtonComponent>
            {
                new DiscordButtonComponent(ButtonStyle.Secondary, "admin_redeem_list", "Redeem List"),
                new DiscordButtonComponent(ButtonStyle.Danger, "admin_redeem_delete", "Delete Redeem")
                //new DiscordButtonComponent(ButtonStyle.Danger, "admin_add_promotion", "Add Promotion")
            };

            var builder = new DiscordMessageBuilder()
                .WithEmbed(embed)
                .AddComponents(row1Buttons)
                .AddComponents(row2Buttons)
                .AddComponents(row3Buttons)
                .AddComponents(row4Buttons);

            await channel.SendMessageAsync(builder);
            await e.Message.RespondAsync($"✅ สร้าง Admin Panel ในช่อง {channel.Mention} เรียบร้อยแล้ว");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in AdminPanelCreate: {ex}");
            await e.Message.RespondAsync("❌ เกิดข้อผิดพลาดในการสร้าง Admin Panel");
        }
    }
}