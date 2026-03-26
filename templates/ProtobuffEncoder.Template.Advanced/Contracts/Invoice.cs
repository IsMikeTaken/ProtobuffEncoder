namespace ProtobuffEncoder.Template.Advanced.Contracts;

// No attributes — resolved via global auto-discover mode.
public class Invoice
{
    public string Number { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime DueDate { get; set; }
}
