using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts.Models;

[ProtoContract(Name = "GetOrderRequest")]
public class GetOrderRequest
{
    [ProtoField(FieldNumber = 1)]
    public Guid OrderId { get; set; }
}

[ProtoContract(Version = 1, Name = "Order", Metadata = "Core E-Commerce Order Aggregate")]
public class Order
{
    [ProtoField(FieldNumber = 1)]
    public Guid Id { get; set; }

    [ProtoField(FieldNumber = 2)]
    public DateTimeOffset CreatedAt { get; set; }

    [ProtoField(FieldNumber = 3)]
    public CustomerDetails Customer { get; set; } = new();

    [ProtoField(FieldNumber = 4)]
    public List<OrderLineItem> Items { get; set; } = [];

    [ProtoField(FieldNumber = 5)]
    public OrderStatus Status { get; set; }
}

[ProtoContract]
public class CustomerDetails
{
    [ProtoField(FieldNumber = 1)]
    public string FirstName { get; set; } = "";

    [ProtoField(FieldNumber = 2)]
    public string LastName { get; set; } = "";

    [ProtoField(FieldNumber = 3)]
    public ShippingAddress Address { get; set; } = new();
}

[ProtoContract]
public class ShippingAddress
{
    [ProtoField(FieldNumber = 1)]
    public string Street { get; set; } = "";

    [ProtoField(FieldNumber = 2)]
    public string City { get; set; } = "";

    [ProtoField(FieldNumber = 3)]
    public string PostalCode { get; set; } = "";

    [ProtoField(FieldNumber = 4)]
    public string Country { get; set; } = "";
}

[ProtoContract]
public class OrderLineItem
{
    [ProtoField(FieldNumber = 1)]
    public string ProductId { get; set; } = "";

    [ProtoField(FieldNumber = 2)]
    public string ProductName { get; set; } = "";

    [ProtoField(FieldNumber = 3)]
    public int Quantity { get; set; }

    [ProtoField(FieldNumber = 4)]
    public decimal UnitPrice { get; set; }
}

public enum OrderStatus
{
    PendingValidation = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3,
    Canceled = 4
}
