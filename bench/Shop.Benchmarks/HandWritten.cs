using Shop.Api.Contracts;
using Shop.Api.Domain;

/// <summary>The whole graph, written out. This is the number everything else is measured against.</summary>
/// <remarks>
/// Deliberately the obvious version: allocate the destination, assign each member, loop the
/// collections with a for over an indexable list and a pre-sized destination. No cleverness that a
/// generator could not also emit, because the point is to find what generated code should cost, not
/// to win a micro-optimisation contest against it.
/// </remarks>
public static class HandWritten
{
    public static OrderDto Map(Order o)
    {
        var lines = new List<OrderLineDto>(o.Lines.Count);
        for (var i = 0; i < o.Lines.Count; i++)
        {
            lines.Add(Line(o.Lines[i]));
        }

        var payments = new List<PaymentDto>(o.Payments.Count);
        for (var i = 0; i < o.Payments.Count; i++)
        {
            payments.Add(Payment(o.Payments[i]));
        }

        return new OrderDto
        {
            Id = o.Id,
            Reference = o.Reference,
            State = o.State.ToString(),
            Channel = (Shop.Api.Contracts.Channel)o.Channel,
            Customer = Customer(o.Customer),
            Lines = lines,
            Payments = payments,
            Total = o.Total,
            PlacedOn = o.PlacedOn,
            ShippedOn = o.ShippedOn,
            CustomerFullName = o.Customer.FullName,
            CustomerAddressCity = o.Customer.Address.City,
        };
    }

    private static CustomerDto Customer(Customer c) => new()
    {
        Id = c.Id,
        FullName = c.FullName,
        Email = c.Email,
        Address = Address(c.Address),
    };

    private static AddressDto Address(Address a) => new()
    {
        Line1 = a.Line1,
        City = a.City,
        Country = Country(a.Country),
    };

    private static CountryDto Country(Country c) => new() { Iso = c.Iso, Name = c.Name };

    private static OrderLineDto Line(OrderLine l)
    {
        var discounts = new List<DiscountDto>(l.Discounts.Count);
        for (var i = 0; i < l.Discounts.Count; i++)
        {
            discounts.Add(new DiscountDto { Code = l.Discounts[i].Code, Percent = l.Discounts[i].Percent });
        }

        return new OrderLineDto { Quantity = l.Quantity, Product = Product(l.Product), Discounts = discounts };
    }

    private static ProductDto Product(Product p) => new()
    {
        Sku = p.Sku,
        Title = p.Title,
        Category = new CategoryDto { Name = p.Category.Name },
        UnitPrice = p.UnitPrice,
    };

    private static PaymentDto Payment(Payment p) => new() { Kind = p.Kind, Amount = p.Amount };
}

/// <summary>Structurally identical to OrderDto and deliberately never declared.</summary>
/// <remarks>
/// The engine lane needs a pair with no generated mapper behind it. OrderDto is declared, and a
/// registration is process wide, so measuring the engine through it would measure generated code.
/// This exists so that row can be taken in the same process as every other row rather than in a
/// second run, which is how a benchmark ends up comparing numbers that were never comparable.
/// </remarks>
public class UndeclaredOrderDto
{
    public long Id { get; set; }
    public string Reference { get; set; } = "";
    public string State { get; set; } = "";
    public Shop.Api.Contracts.Channel Channel { get; set; }
    public Shop.Api.Contracts.CustomerDto Customer { get; set; } = new();
    public List<Shop.Api.Contracts.OrderLineDto> Lines { get; set; } = new();
    public List<Shop.Api.Contracts.PaymentDto> Payments { get; set; } = new();
    public decimal Total { get; set; }
    public DateTimeOffset PlacedOn { get; set; }
    public DateTime? ShippedOn { get; set; }
    public string CustomerFullName { get; set; } = "";
    public string CustomerAddressCity { get; set; } = "";
}
