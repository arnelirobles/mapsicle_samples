using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Mapsicle;
using Mapster;
using Riok.Mapperly.Abstractions;
using Shop.Api.Contracts;
using Shop.Api.Data;
using Shop.Api.Domain;
using Shop.Api.Mapping;

[assembly: MapsicleGenerate(typeof(Order), typeof(OrderSummaryDto))]

BenchmarkRunner.Run([typeof(FlatProjection), typeof(WholeAggregate)]);

// Two benchmarks, because one number would be a choice about which story to tell.
//
// Every mapper in a class maps the identical source into the identical destination. A baseline that
// maps a smaller type than the entries it is compared against is not a baseline, it is a headline.

/// <summary>Order into the flat summary. Five members, no nesting, no collections.</summary>
/// <remarks>
/// This is the pair Mapsicle's generator accepts, so it is where the compile-time lane can be
/// compared against hand written code and against Mapperly, which always emits code.
/// </remarks>
[MemoryDiagnoser]
public class FlatProjection
{
    private Order _order = null!;
    private IMapper _autoMapper = null!;
    private SummaryMapper _mapperly = null!;

    [GlobalSetup]
    public void Setup()
    {
        _order = Seed.BuildOrder();

        _autoMapper = new MapperConfiguration(
            c => c.CreateMap<Order, OrderSummaryDto>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
        _mapperly = new SummaryMapper();

        _ = _order.MapTo<OrderSummaryDto>();
        _ = ((object)_order).MapTo<OrderSummaryDto>();
        _ = _autoMapper.Map<OrderSummaryDto>(_order);
        _ = _mapperly.Map(_order);
    }

    [Benchmark(Baseline = true, Description = "hand written")]
    public OrderSummaryDto Handwritten() => new()
    {
        Id = _order.Id,
        Reference = _order.Reference,
        Total = _order.Total,
        Currency = _order.Currency,
        PlacedOn = _order.PlacedOn,
    };

    // Bound at compile time. The declaration above emits a MapTo taking an Order, the compiler
    // prefers it over the engine's MapTo taking an object, and no lookup happens at all.
    [Benchmark(Description = "Mapsicle, compile-time bound")]
    public OrderSummaryDto? MapsicleBound() => _order.MapTo<OrderSummaryDto>();

    // The same generated delegate, reached the way an untyped call site reaches it: a dictionary
    // lookup on the source's runtime type. This is what a pair the generator refused also costs to
    // dispatch, minus the expression tree behind it.
    [Benchmark(Description = "Mapsicle, registry lookup")]
    public OrderSummaryDto? MapsicleLookup() => ((object)_order).MapTo<OrderSummaryDto>();

    [Benchmark(Description = "Mapperly")]
    public OrderSummaryDto Mapperly() => _mapperly.Map(_order);

    [Benchmark(Description = "AutoMapper")]
    public OrderSummaryDto AutoMapper() => _autoMapper.Map<OrderSummaryDto>(_order);
}

/// <summary>Order into the full read model: three levels of nesting, two collections, a cycle.</summary>
/// <remarks>
/// No hand written baseline here on purpose. Writing this projection by hand is around sixty lines
/// across nine types, and keeping it correct as the model changes is the work these libraries exist
/// to remove. Mapsicle's generator refuses this pair, so its entry is the runtime engine.
/// </remarks>
[MemoryDiagnoser]
public class WholeAggregate
{
    private Order _order = null!;
    private IMapper _autoMapper = null!;
    private MapperlyOrderMapper _mapperly = null!;

    [GlobalSetup]
    public void Setup()
    {
        _order = Seed.BuildOrder();

        _autoMapper = new MapperConfiguration(
            c => c.AddProfile<ShopProfile>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
        _mapperly = new MapperlyOrderMapper();

        _ = ((object)_order).MapTo<OrderDto>();
        _ = _autoMapper.Map<OrderDto>(_order);
        _ = _mapperly.Map(_order);
    }

    [Benchmark(Baseline = true, Description = "Mapperly")]
    public OrderDto Mapperly() => _mapperly.Map(_order);

    [Benchmark(Description = "Mapsicle, engine")]
    public OrderDto? Mapsicle() => ((object)_order).MapTo<OrderDto>();

    [Benchmark(Description = "Mapster")]
    public OrderDto MapsterLane() => _order.Adapt<OrderDto>();

    [Benchmark(Description = "AutoMapper")]
    public OrderDto AutoMapper() => _autoMapper.Map<OrderDto>(_order);
}

// Fully qualified, because "using Mapster" alongside AutoMapper and Mapsicle makes the bare name
// Mapper ambiguous and this attribute stops resolving. Worth knowing before you put all four in one
// project.
[Riok.Mapperly.Abstractions.Mapper]
public partial class SummaryMapper
{
    public partial OrderSummaryDto Map(Order source);
}
