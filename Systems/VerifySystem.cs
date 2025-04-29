using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus;
using static System.Runtime.InteropServices.JavaScript.JSType;

public static class VerifySystem
{
    static DiscordClient discord;
    public static async Task OnVerifyButtonClicked(DiscordClient sender, ComponentInteractionCreateEventArgs e)
    {
        if (e.Id == "verify_btn")
        {
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle("Verify Minecraft Account")
                .WithCustomId("verify_modal")
                .AddComponents(new TextInputComponent(
                    label: "Minecraft Username",
                    customId: "mc_username",
                    placeholder: "กรอกชื่อในเกมของคุณ",
                    required: true,
                    style: TextInputStyle.Short))
                .AddComponents(new TextInputComponent(
                    label: "Minecraft Password",
                    customId: "mc_password",
                    placeholder: "กรอกรหัสผ่านในเกมของคุณ",
                    required: true,
                    style: TextInputStyle.Short));

            await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
        }
    }

    public static async Task verifychannelcreate(MessageCreateEventArgs e)
    {
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
            await e.Message.RespondAsync("❌ ใช้คำสั่งไม่ถูกต้อง ตัวอย่าง: `-verifychannelcreate {channelid}`");
            return;
        }

        // ดึง channel ID จากคำสั่ง
        if (!ulong.TryParse(args[1], out var channelId))
        {
            await e.Message.RespondAsync("❌ Channel ID ไม่ถูกต้อง");
            return;
        }

        // หาช่องที่ระบุ
        var channel = await discord.GetChannelAsync(channelId);
        if (channel == null)
        {
            await e.Message.RespondAsync("❌ ไม่พบช่องที่ระบุ");
            return;
        }

        // ส่งข้อความพร้อมปุ่ม Verify
        var builder = new DiscordMessageBuilder()
            .WithContent("กดปุ่มด้านล่างเพื่อ Verify บัญชี Minecraft")
            .AddComponents(new DiscordButtonComponent(
                ButtonStyle.Primary,
                "verify_btn",
                "Verify Minecraft"));

        await channel.SendMessageAsync(builder);

        // ตอบกลับผู้ใช้
        await e.Message.RespondAsync($"✅ สร้างปุ่ม Verify ในช่อง {channel.Mention} เรียบร้อยแล้ว");
    }

    public static bool VerifyPassword(string inputPassword, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(inputPassword, hashedPassword);
    }
}