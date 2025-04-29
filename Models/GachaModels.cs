public class GachaBox
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string ImageUrl { get; set; }
    public int Price { get; set; }
    public string CurrencyType { get; set; } = "point";
    public List<GachaItem> Items { get; set; } = new();
}

public class GachaItem
{
    public int Id { get; set; }
    public int BoxId { get; set; }
    public string Name { get; set; }
    public string ImageUrl { get; set; }
    public bool IsGuaranteed { get; set; }
    public string Command { get; set; }
    public int Rarity { get; set; }
    public int Probability { get; set; }
}