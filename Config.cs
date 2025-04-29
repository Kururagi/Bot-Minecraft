public static class Config
{
    // Discord Bot Token
    //FOR TEST
    //public static string DiscordToken => "";
    //FOR REAL
    public static string DiscordToken => "";

    // Minecraft RCON Settings
    public static string MinecraftServerIP => "";
    public static int MinecraftServerPort => 25575; // Default RCON port
    public static string RconPassword => "";

    // MySQL Database
    public static string MySqlHost = "";
    public static string MySqlDatabase = "";
    public static string MySqlUsername = "";
    public static string MySqlPassword = "";
    public static int MySqlPort = 3306; // Default MySQL port

    public static string MySqlConnectionString =>
        $"Server={MySqlHost};Database={MySqlDatabase};User ID={MySqlUsername};Password={MySqlPassword};";

    public static string PromptPayNumber = ""; // เบอร์พร้อมเพย์
    public static string PromptPayName = ""; // เบอร์พร้อมเพย์
    public static decimal PromptPayRate = 1.0m; // 1 บาท = 1 Cash (หรือจะเปลี่ยนเป็น coin ก็ได้)

    public static string MindeeApiKey = "";
}