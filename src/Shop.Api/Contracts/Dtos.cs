namespace Shop.Api.Contracts;

// The read model. The interesting members are the ones that are not a straight copy:
// a widening id, an enum arriving as a string, a timestamp arriving as an offset, and two
// flattened names, one of which reaches three levels down.

public class CountryDto
{
    public string Iso { get; set; } = "";
    public string Name { get; set; } = "";
}

public class AddressDto
{
    public string Line1 { get; set; } = "";
    public string City { get; set; } = "";
    public CountryDto Country { get; set; } = new();
}

public class CustomerDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public AddressDto Address { get; set; } = new();
}

public class CategoryDto
{
    public string Name { get; set; } = "";
}

public class ProductDto
{
    public string Sku { get; set; } = "";
    public string Title { get; set; } = "";
    public CategoryDto Category { get; set; } = new();
    public decimal UnitPrice { get; set; }
}

public class DiscountDto
{
    public string Code { get; set; } = "";
    public decimal Percent { get; set; }
}

public class OrderLineDto
{
    public int Quantity { get; set; }
    public ProductDto Product { get; set; } = new();
    public List<DiscountDto> Discounts { get; set; } = new();
}

public class PaymentDto
{
    public string Kind { get; set; } = "";
    public decimal Amount { get; set; }
}

public class OrderDto
{
    public long Id { get; set; }                          // int widens to long
    public string Reference { get; set; } = "";
    public string State { get; set; } = "";               // enum arrives as a string
    public Channel Channel { get; set; }
    public CustomerDto Customer { get; set; } = new();
    public List<OrderLineDto> Lines { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
    public decimal Total { get; set; }
    public DateTimeOffset PlacedOn { get; set; }          // DateTime arrives as an offset
    public DateTime? ShippedOn { get; set; }

    public string CustomerFullName { get; set; } = "";    // flattened, two levels
    public string CustomerAddressCity { get; set; } = ""; // flattened, three levels
}

// What a caller sends. Deliberately smaller than the entity, which is the pattern the README
// recommends for untrusted input: a DTO holding only the members a caller may set.
public class CreateOrderRequest
{
    public string Reference { get; set; } = "";
    public int CustomerId { get; set; }
    public Channel Channel { get; set; }
    public List<CreateOrderLine> Lines { get; set; } = new();
}

public class CreateOrderLine
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public enum Channel { Web, Mobile, InStore }

/// <summary>The flat projection, kept as the smallest case the generator handles.</summary>
/// <remarks>
/// This type exists for a reason worth recording. OrderDto above used to be refused for generation,
/// because its Id widens int to long and the emitter had no widening rule, so this was invented to
/// give the samples something that would generate at all. The emitter handles the whole aggregate
/// now and OrderDto generates, but the flat pair is still the clearest place to see what declaring
/// a pair costs and buys, so it stays.
/// </remarks>
public class OrderSummaryDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
    public DateTime PlacedOn { get; set; }
}
