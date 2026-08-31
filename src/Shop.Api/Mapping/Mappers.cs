using AutoMapper;
using Mapsicle;
using Riok.Mapperly.Abstractions;
using Shop.Api.Contracts;
using Shop.Api.Domain;

// Declare the pair for Mapsicle's generator. One line, at the assembly, and the call site does
// not change. See README for what that buys and what it does not.
[assembly: MapsicleGenerate(typeof(Order), typeof(OrderDto))]      // refused, see MSG001 at build time
[assembly: MapsicleGenerate(typeof(Order), typeof(OrderSummaryDto))]

namespace Shop.Api.Mapping;

/// <summary>The three mappers, side by side, mapping the same aggregate.</summary>
/// <remarks>
/// This file is most of the point of the repository. The domain and the DTO are fixed; what
/// differs is how much you have to write to get from one to the other, and what happens when the
/// mapper meets a shape it was not told about.
/// </remarks>
public interface IOrderMapper
{
    string Name { get; }
    OrderDto Map(Order order);
}

// ---------------------------------------------------------------------------------------------
// Mapsicle. No configuration at all. The assembly attribute above is optional and only affects
// speed; delete it and this still works, through the runtime engine.
// ---------------------------------------------------------------------------------------------

public sealed class MapsicleOrderMapper : IOrderMapper
{
    public string Name => "Mapsicle";

    public OrderDto Map(Order order) => order.MapTo<OrderDto>()!;
}

// ---------------------------------------------------------------------------------------------
// AutoMapper. A profile, and a CreateMap per pair in the graph. Miss one and the member comes
// back empty at runtime.
// ---------------------------------------------------------------------------------------------

public sealed class ShopProfile : Profile
{
    public ShopProfile()
    {
        CreateMap<Order, OrderDto>();
        CreateMap<Customer, CustomerDto>();
        CreateMap<Address, AddressDto>();
        CreateMap<Country, CountryDto>();
        CreateMap<OrderLine, OrderLineDto>();
        CreateMap<Product, ProductDto>();
        CreateMap<Category, CategoryDto>();
        CreateMap<Discount, DiscountDto>();
        CreateMap<Payment, PaymentDto>();
    }
}

public sealed class AutoMapperOrderMapper(IMapper mapper) : IOrderMapper
{
    public string Name => "AutoMapper";

    public OrderDto Map(Order order) => mapper.Map<OrderDto>(order);
}

// ---------------------------------------------------------------------------------------------
// Mapperly. A partial class and one declared method. For this graph it needs no hand written rules
// at all: it derives the enum to string, the int to long, the DateTime to DateTimeOffset and both
// flattened members on its own, and emits ordinary C# for all of them.
//
// What it does cost is 12 RMG020 warnings, one per source member no destination member claims. That
// is the design, not a misconfiguration: Mapperly treats an unmapped member as something you should
// have said out loud. Silence it per member with [MapperIgnoreSource], or project-wide with NoWarn.
//
// Note what is missing: no method maps Customer.Orders. Mapperly follows a cycle rather than
// stopping at it, so the DTO deliberately does not expose it. See README.
// ---------------------------------------------------------------------------------------------

[Mapper]
public partial class MapperlyOrderMapper : IOrderMapper
{
    public string Name => "Mapperly";

    public OrderDto Map(Order order) => ToDto(order);

    private partial OrderDto ToDto(Order source);

}
