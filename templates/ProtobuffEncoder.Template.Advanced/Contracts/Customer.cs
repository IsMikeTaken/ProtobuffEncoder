namespace ProtobuffEncoder.Template.Advanced.Contracts;

// No attributes — used with ProtoRegistry auto-discovery.
public class Customer
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public decimal CreditLimit { get; set; }
}
