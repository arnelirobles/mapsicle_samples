using Mapster;
using Shop.Api.Contracts;
using Shop.Api.Domain;

namespace Shop.Api.Mapping;

/// <summary>Mapster, the closest comparison in this repository.</summary>
/// <remarks>
/// Its own file, because <c>using Mapster;</c> alongside AutoMapper and Mapsicle makes the bare name
/// <c>Mapper</c> ambiguous and Mapperly's <c>[Mapper]</c> attribute stops resolving. That is worth
/// knowing before you put all four in one project.
///
/// Like Mapsicle it needs no configuration at all, which is what makes it the interesting neighbour:
/// both map by convention and neither asks for a line of setup, so the differences show up on shapes
/// rather than on ceremony. The <c>/compare</c> endpoint is where you see them, and the cycle probe
/// is where the biggest one lives.
/// </remarks>
public sealed class MapsterOrderMapper : IOrderMapper
{
    public string Name => "Mapster";

    public OrderDto Map(Order order) => order.Adapt<OrderDto>();
}
