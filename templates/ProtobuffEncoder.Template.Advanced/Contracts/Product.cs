namespace ProtobuffEncoder.Template.Advanced.Contracts;

// No attributes — field numbering depends on the registry strategy.
public class Product
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string Category { get; set; } = "";
}
