namespace GenerateAll;

// Ordinary entities and DTOs. Nothing here knows a mapper exists: no attributes, no base types,
// no partial classes. That is the point of the sample.

public class Country { public string Iso { get; set; } = ""; public string Name { get; set; } = ""; }

public class Address
{
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
}

public class Product { public string Sku { get; set; } = ""; public string Title { get; set; } = ""; public decimal UnitPrice { get; set; } }

public class OrderLine { public int Quantity { get; set; } public Product Product { get; set; } = new(); }

public class Order
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = new();
    public decimal Total { get; set; }
}

public class CountryDto { public string Iso { get; set; } = ""; }
public class AddressDto { public string City { get; set; } = ""; public CountryDto Country { get; set; } = new(); }
public class CustomerDto { public string FullName { get; set; } = ""; public AddressDto Address { get; set; } = new(); }
public class ProductDto { public string Sku { get; set; } = ""; public decimal UnitPrice { get; set; } }
public class OrderLineDto { public int Quantity { get; set; } public ProductDto Product { get; set; } = new(); }

public class OrderDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public CustomerDto Customer { get; set; } = new();
    public List<OrderLineDto> Lines { get; set; } = new();
    public decimal Total { get; set; }

    // Flattened from Customer.FullName. Nobody configures this; the convention finds it.
    public string CustomerFullName { get; set; } = "";
}

public class CustomerSummaryDto { public string FullName { get; set; } = ""; public string Email { get; set; } = ""; }
