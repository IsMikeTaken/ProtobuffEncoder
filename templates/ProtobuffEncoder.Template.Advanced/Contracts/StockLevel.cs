namespace ProtobuffEncoder.Template.Advanced.Contracts;

// Auto-discovered response type for the InventoryService.
public class StockLevel
{
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public bool InStock { get; set; }
    public string Warehouse { get; set; } = "";
}
