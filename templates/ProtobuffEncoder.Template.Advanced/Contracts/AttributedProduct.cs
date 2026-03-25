using ProtobuffEncoder.Attributes;

// --- Attributed contract ---
[ProtoContract]
public class AttributedProduct
{
    [ProtoField(10)] public string Sku { get; set; } = "";
    [ProtoField(20)] public string Title { get; set; } = "";
    [ProtoField(30)] public double Weight { get; set; }
}