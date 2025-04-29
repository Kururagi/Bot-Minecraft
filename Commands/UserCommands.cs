using DSharpPlus.Entities;
using DSharpPlus;
using MySql.Data.MySqlClient;
using System.Data;
using DSharpPlus.EventArgs;
using static System.Runtime.InteropServices.JavaScript.JSType;

public static class UserCommands
{
    private static DiscordClient _discord;

    public static void Initialize(DiscordClient client)
    {
        _discord = client;
    }

    public static async Task ShowUserInfo(DiscordInteraction interaction)
    {
        // ดึง Discord ID ของผู้ใช้
        var discordId = interaction.User.Id;

        // ดึงข้อมูลผู้ใช้จากฐานข้อมูล
        using var conn = new MySqlConnection(Config.MySqlConnectionString);
        await conn.OpenAsync();

        var cmd = new MySqlCommand(
            "SELECT username, realname, email, isLogged, point, cash, topup_points FROM authme WHERE discord_id = @discordId", // เพิ่มฟิลด์ cash
            conn);
        cmd.Parameters.AddWithValue("@discordId", discordId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            await interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ ไม่พบข้อมูลผู้ใช้ในระบบ")
                    .AsEphemeral(true));
            return;
        }

        // อ่านข้อมูลผู้ใช้
        await reader.ReadAsync();
        var username = reader.GetString("username");
        var realname = reader.GetString("realname");
        var email = reader.GetString("email");
        var isLogged = reader.GetBoolean("isLogged");
        var point = reader.GetInt32("point");
        var cash = reader.GetDecimal("cash"); // อ่านข้อมูล cash
        var topup_points = reader.GetDecimal("topup_points"); // อ่านข้อมูล cash

        // สร้าง Embed
        var embed = new DiscordEmbedBuilder()
            .WithTitle("👤 User Info")
            .WithDescription("ข้อมูลผู้ใช้")
            .WithColor(DiscordColor.Blue)
            .WithThumbnail($"https://minotar.net/helm/{realname}")
            .AddField("Username", username, true)
            .AddField("Realname", realname, true)
            .AddField("Email", email, true)
            .AddField("🪙 Point", $"{point:N2} Point", true)
            .AddField("💵 Cash", $"{cash:N2} Cash", true) // แสดง cash แบบมีทศนิยม 2 ตำแหน่ง
            .AddField("💵 แต้มสะสม", $"{topup_points:N2} บาท", true) // แสดง cash แบบมีทศนิยม 2 ตำแหน่ง
            .AddField("สถานะการเข้าสู่ระบบ", isLogged ? "✅ กำลังออนไลน์" : "❌ ออฟไลน์", true)
            .WithFooter("User Info | Kururagi Seimei");

        // ส่ง Embed กลับไปยังผู้ใช้
        await interaction.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true));
    }

    public static async Task userpanelcreate(MessageCreateEventArgs e)
    {
        try
        {
            // ตรวจสอบว่า _discord ถูก Initialize แล้ว
            if (_discord == null)
            {
                await e.Message.RespondAsync("❌ ระบบบอทยังไม่พร้อม กรุณาลองใหม่ในภายหลัง");
                return;
            }

            var member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (member == null)
            {
                await e.Message.RespondAsync("❌ ไม่พบข้อมูลผู้ใช้ในเซิร์ฟเวอร์นี้");
                return;
            }

            // ตรวจสอบสิทธิ์ผู้ใช้ (มีโค้ดซ้ำกัน 2 ครั้งในโค้ดเดิม เอาไว้ครั้งเดียวพอ)
            if (!member.Permissions.HasPermission(Permissions.Administrator))
            {
                await e.Message.RespondAsync("❌ คุณไม่มีสิทธิ์ใช้คำสั่งนี้");
                return;
            }

            // แยกคำสั่งและ channel ID
            var args = e.Message.Content.Split(' ');
            if (args.Length < 2)
            {
                await e.Message.RespondAsync("❌ ใช้คำสั่งไม่ถูกต้อง ตัวอย่าง: `-userpanelcreate {channelid}`");
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

            // สร้าง User Panel
            var embed = new DiscordEmbedBuilder()
                .WithTitle("👤 User Panel")
                .WithDescription("เลือกปุ่มด้านล่างเพื่อดำเนินการ")
                .WithColor(DiscordColor.Blue)
                .WithThumbnail("https://media.discordapp.net/attachments/1346511677700575294/1346511724626448466/main_417b8ccb-6c51-4a42-b6ba-c435770c578814.png?ex=67dc3ac4&is=67dae944&hm=1e3c661c2beaa61a4e48ffea8c52e5556a46bf0c6f170efaa0fd88a2c94e5dc7&=&format=webp&quality=lossless&width=696&height=696")
                .AddField("Check Info", "ดูข้อมูลผู้ใช้", true)
                .AddField("Gacha", "สุ่มของรางวัล", true)
                .AddField("Topup", "เติมเงินหรือไอเทม", true)
                .AddField("Shop", "ซื้อไอเทมจากร้านค้า", true)
                .AddField("Promotion", "รับ Item Promotion ต่างๆ", true)
                .WithFooter("Create By KururagiSeimei");

            var row1 = new List<DiscordButtonComponent>
            {
                new DiscordButtonComponent(ButtonStyle.Primary, "check_info_btn", "Check Info" , emoji: new DiscordComponentEmoji("👤")),
                new DiscordButtonComponent(ButtonStyle.Success, "gacha_btn", "Gacha" , emoji: new DiscordComponentEmoji("🎁")),
                new DiscordButtonComponent(ButtonStyle.Danger, "topup_btn", "Topup" , emoji: new DiscordComponentEmoji("💵")),
                new DiscordButtonComponent(ButtonStyle.Secondary, "shop_btn", "Shop" , emoji: new DiscordComponentEmoji("🛒")),
                new DiscordButtonComponent(ButtonStyle.Primary, "redeem_btn", "ใช้โค้ด", emoji: new DiscordComponentEmoji("🎫"))
            };

            var row2 = new List<DiscordButtonComponent>
            {
                //new DiscordButtonComponent(ButtonStyle.Primary, "promotion_btn", "Promotion", emoji: new DiscordComponentEmoji("🎉"))
            };

            var builder = new DiscordMessageBuilder()
                .WithEmbed(embed)
                .AddComponents(row1);
                //.AddComponents(row2);

            await channel.SendMessageAsync(builder);
            await e.Message.RespondAsync($"✅ สร้าง User Panel ในช่อง {channel.Mention} เรียบร้อยแล้ว");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in userpanelcreate: {ex}");
            await e.Message.RespondAsync("❌ เกิดข้อผิดพลาดในการสร้าง User Panel");
        }
    }
}