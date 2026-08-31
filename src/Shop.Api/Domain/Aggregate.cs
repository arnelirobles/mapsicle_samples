namespace Shop.Api.Domain;

// The aggregate the samples map. Every shape here is one that separates the three mappers:
// three levels of nesting, a collection inside a collection, a polymorphic list, an enum, a
// value object, a dictionary, a nullable, a self referencing category, and a cycle from the
// order back to itself through its customer.

public enum OrderState { Draft, Placed, Shipped, Refunded }
public enum Channel { Web, Mobile, InStore }

public class Country
{
    public int Id { get; set; }
    public string Iso { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Address
{
    public int Id { get; set; }
    public string Line1 { get; set; } = "";
    public string City { get; set; } = "";
    public Country Country { get; set; } = new();
}

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public Address Address { get; set; } = new();

    // The cycle. An order points at its customer and the customer lists its orders.
    public List<Order> Orders { get; set; } = new();
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Category? Parent { get; set; }   // self referencing
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Title { get; set; } = "";
    public Category Category { get; set; } = new();
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "PHP";
}

public class Discount
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public decimal Percent { get; set; }
}

public class OrderLine
{
    public int Id { get; set; }
    public int Quantity { get; set; }
    public Product Product { get; set; } = new();
    public List<Discount> Discounts { get; set; } = new();   // a collection inside a collection
}

public class Payment
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";     // card, wallet, transfer
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PHP";
}

public class Order
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public OrderState State { get; set; }
    public Channel Channel { get; set; }
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public decimal Total { get; set; }
    public string Currency { get; set; } = "PHP";
    public DateTime PlacedOn { get; set; }
    public DateTime? ShippedOn { get; set; }                  // nullable
}
