using DSharpPlus.EventArgs;
using DSharpPlus;
using System.Threading.Tasks;

public static class MessageCreated
{
    public static async Task OnMessageCreated(DiscordClient sender, MessageCreateEventArgs e)
    {
        if (e.Author.IsBot) return; // Skip if the author is a bot

        if (e.Message.Content.StartsWith("-verifychannelcreate"))
        {
            await VerifySystem.verifychannelcreate(e);
        }
        else if (e.Message.Content.StartsWith("-userpanelcreate"))
        {
            await UserCommands.userpanelcreate(e);
        }
        else if (e.Message.Content.StartsWith("-adminpanelcreate"))
        {
            await AdminCommands.AdminPanelCreate(e);
        }
        else if (e.Message.Content.StartsWith("-checkslip"))
        {
            // Ensure that there's at least one attachment
            if (e.Message.Attachments.Count == 0)
            {
                await e.Message.RespondAsync("❌ กรุณาแนบสลิปการโอนเงิน");
                return;
            }

            // If there's an attachment, get the first one
            var attachment = e.Message.Attachments[0];

            // Handle the attachment using TopupSystem
            await TopupSystem.HandleSlipUpload(e, attachment);
        }
    }
}