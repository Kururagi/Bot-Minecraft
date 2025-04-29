public class ShopCategory
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class ShopItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; } // เพิ่ม property นี้
    public int Price { get; set; }
    public string CurrencyType { get; set; } // เพิ่ม property นี้
    public string Command { get; set; }
    public int? PurchaseLimit { get; set; }
}