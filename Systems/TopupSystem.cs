using DSharpPlus;
using DSharpPlus.Entities;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using DSharpPlus.EventArgs;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using System.Text.Json;

public static class TopupSystem
{
    // ฟังก์ชันในการดึงข้อมูล PromptPay
    public static string GetPromptPayInfo()
    {
        return $" 📱 พร้อมเพย์: `{Config.PromptPayNumber}`\nชื่อบัญชี : `{Config.PromptPayName}`\n💰 อัราเติมเงิน = X{Config.PromptPayRate}";
    }

    public static int CalculateCredit(decimal amount)
    {
        return (int)(amount * Config.PromptPayRate);
    }

    public static string GetPromptPayQrUrl(decimal amount)
    {
        if (amount > 999999.99m)
            amount = 999999.99m;

        string phone = Config.PromptPayNumber;
        string formatted = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return $"https://promptpay.io/{phone}/{formatted}.png";
    }

    public static async Task HandleTopupModal(DiscordInteraction interaction, string amountInput)
    {
        if (!decimal.TryParse(amountInput, out decimal amount) || amount <= 0)
        {
            await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .WithContent("❌ กรุณากรอกจำนวนเงินที่ถูกต้อง")
                .AsEphemeral(true));
            return;
        }

        int credit = CalculateCredit(amount);
        string qrUrl = GetPromptPayQrUrl(amount);

        // เก็บยอดที่กรอกไว้เพื่อใช้ตรวจสอบทีหลัง
        pendingTopups[interaction.User.Id] = amount;

        var embed = new DiscordEmbedBuilder
        {
            Title = "📥 กรุณาโอนเงินตาม QR ด้านล่าง",
            Description = $"💵 จำนวน: `{amount} บาท`\n🪙 Cashที่จะได้รับ: **{credit} Cash**\n\nหลังจากโอนแล้ว โปรดเปิด Ticket แล้วพิม -checkslip พร้อมแนบสลิปเพื่อรอ Confirm ✅\n\n{GetPromptPayInfo()}",
            ImageUrl = qrUrl,
            Color = DiscordColor.Blurple
        };

        await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
            .AddEmbed(embed)
            .AsEphemeral(true));
    }

    public static async Task ShowTopupModal(DiscordInteraction interaction)
    {
        var modal = new DiscordInteractionResponseBuilder()
            .WithTitle("\u0e01\u0e23\u0e2d\u0e01\u0e08\u0e33\u0e19\u0e27\u0e19\u0e40\u0e07\u0e34\u0e19\u0e17\u0e35\u0e48\u0e15\u0e49\u0e2d\u0e07\u0e01\u0e32\u0e23\u0e40\u0e15\u0e34\u0e21")
            .WithCustomId("topup_modal")
            .AddComponents(new TextInputComponent("\u0e08\u0e33\u0e19\u0e27\u0e19\u0e40\u0e07\u0e34\u0e19 (\u0e1a\u0e32\u0e17)", "topup_amount", placeholder: "\u0e40\u0e0a\u0e48\u0e19 100", required: true, style: TextInputStyle.Short));

        await interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
    }

    // ใช้ Mindee OCR แทน Tesseract
    public static async Task<decimal> ExtractAmountFromSlip(string imagePath)
    {
        try
        {
            var apiKey = Config.MindeeApiKey;
            var apiUrl = "https://api.mindee.net/v1/products/mindee/expense_receipts/v5/predict";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Token {apiKey}");

            using var form = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(imagePath);
            form.Add(new StreamContent(fileStream), "document", Path.GetFileName(imagePath));

            var response = await client.PostAsync(apiUrl, form);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ Mindee API Error: {response.StatusCode} - {responseContent}");
                return 0;
            }

            Console.WriteLine("📦 Raw Mindee response:");
            Console.WriteLine(responseContent); // DEBUG ตรงนี้ก่อน

            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("document", out var docElement) &&
                docElement.TryGetProperty("inference", out var inference) &&
                inference.TryGetProperty("pages", out var pages) &&
                pages[0].TryGetProperty("prediction", out var prediction) &&
                prediction.TryGetProperty("total_amount", out var totalAmount))
            {
                if (totalAmount.TryGetProperty("value", out var valueElement))
                {
                    return valueElement.GetDecimal();
                }
            }

            return 0; // ในกรณีที่ไม่พบ value
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Mindee OCR Error: {ex.Message}");
            return 0;
        }
    }

    public static async Task HandleSlipUpload(MessageCreateEventArgs e, DiscordAttachment attachment)
    {
        try
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.Url))
            {
                await e.Message.RespondAsync("❌ ไม่พบไฟล์สลิปที่แนบมา");
                return;
            }

            var slipsDir = Path.Combine(Directory.GetCurrentDirectory(), "slips");
            Directory.CreateDirectory(slipsDir);

            var uri = new Uri(attachment.Url);
            string fileExt = Path.GetExtension(uri.AbsolutePath);
            var tempFilePath = Path.Combine(slipsDir, $"{Guid.NewGuid()}{fileExt}");

            using var httpClient = new HttpClient();
            var imageBytes = await httpClient.GetByteArrayAsync(attachment.Url);
            await File.WriteAllBytesAsync(tempFilePath, imageBytes);

            string finalImagePath = tempFilePath;

            if (fileExt == ".jfif")
            {
                var pngFilePath = Path.Combine(slipsDir, $"{Guid.NewGuid()}.png");
                using var image = await Image.LoadAsync(tempFilePath);
                await image.SaveAsPngAsync(pngFilePath);
                File.Delete(tempFilePath);
                finalImagePath = pngFilePath;
            }

            decimal amountFromSlip = await ExtractAmountFromSlip(finalImagePath);

            if (amountFromSlip <= 0)
            {
                await e.Message.RespondAsync("❌ ไม่พบยอดเงินในสลิป โปรดตรวจสอบสลิปอีกครั้ง");
                return;
            }

            if (IsAmountValid(e.Author.Id, amountFromSlip))
            {
                await AddCreditsToUser(e.Author, amountFromSlip);
                await e.Message.RespondAsync($"✅ เติมเงินสำเร็จ! ยอด {amountFromSlip:N2} บาท ได้รับCashแล้ว");
            }
            else
            {
                await e.Message.RespondAsync("❌ ยอดเงินไม่ตรงกับที่กรอกไว้ โปรดตรวจสอบอีกครั้ง");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ข้อผิดพลาด: {ex.Message}");
            await e.Message.RespondAsync("❌ เกิดข้อผิดพลาดในการประมวลผลสลิป โปรดลองใหม่หรือติดต่อแอดมิน");
        }
    }

    private static Dictionary<ulong, decimal> pendingTopups = new();

    public static bool IsAmountValid(ulong userId, decimal amountFromSlip)
    {
        return pendingTopups.TryGetValue(userId, out var expectedAmount) && Math.Abs(expectedAmount - amountFromSlip) < 0.01m;
    }

    public static async Task AddCreditsToUser(DiscordUser user, decimal amount)
    {
        try
        {
            using var conn = new MySqlConnection(Config.MySqlConnectionString);
            await conn.OpenAsync();

            // เพิ่มเงิน
            var cmd = new MySqlCommand("UPDATE authme SET cash = cash + @amount WHERE discord_id = @userId", conn);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@userId", user.Id);
            await cmd.ExecuteNonQueryAsync();

            // เพิ่มแต้ม (1 บาท = 1 แต้ม)
            int pointsToAdd = (int)Math.Floor(amount);
            var pointCmd = new MySqlCommand("UPDATE authme SET topup_points = topup_points + @points WHERE discord_id = @userId", conn);
            pointCmd.Parameters.AddWithValue("@points", pointsToAdd);
            pointCmd.Parameters.AddWithValue("@userId", user.Id);
            await pointCmd.ExecuteNonQueryAsync();

            // ตรวจสอบยอดเงินใหม่
            var balanceCmd = new MySqlCommand("SELECT cash FROM authme WHERE discord_id = @userId", conn);
            balanceCmd.Parameters.AddWithValue("@userId", user.Id);
            var newBalance = Convert.ToDecimal(await balanceCmd.ExecuteScalarAsync());

            Console.WriteLine($"✅ เพิ่ม Cash {amount} ให้ผู้ใช้ {user.Username} แล้ว | ยอดใหม่: {newBalance} | ได้แต้ม: +{pointsToAdd}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in AddCreditsToUser: {ex.Message}");
        }
    }
}
