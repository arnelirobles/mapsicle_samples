# One attribute instead of one per pair

```bash
dotnet run --project samples/GenerateAll
```

## The setup, in full

```csharp
[assembly: MapsicleGenerateAll]
```

That is it. No profile, no partial class, no `CreateMap`, and no attributes on any of the entities
or DTOs. [`Domain.cs`](Domain.cs) is ordinary classes that do not know a mapper exists.

## What it does

The generator walks this assembly's call sites for `.MapTo<TDest>()` and emits a mapper for every
pair whose source type it can read there. The calls in [`Program.cs`](Program.cs) are the calls you
would write anyway:

```csharp
OrderDto full              = order.MapTo<OrderDto>()!;
CustomerSummaryDto summary = order.Customer.MapTo<CustomerSummaryDto>()!;
List<OrderLineDto> lines   = order.Lines.MapTo<OrderLineDto>();
```

Delete the attribute and every line above still works, through the runtime engine, about 1.8x
slower. That is the whole design: the call site does not change either way.

## What gets emitted

Two pairs are registered from those call sites:

```
Customer  into  CustomerSummaryDto
Order     into  OrderDto
```

`Address`, `Country`, `Product` and `OrderLine` are not separate registrations. They are emitted as
private helpers inside the `Order` mapper, which is why one call site produces one mapper rather
than a wall of generated types:

```csharp
internal static OrderDto P1_Map(Order source)
{
    return new OrderDto
    {
        @Id = source.@Id,
        @Reference = source.@Reference,
        @Customer = P1_Object0(source.@Customer)!,
        @Lines = P1_List3(source.@Lines),
        @Total = source.@Total,
        @CustomerFullName = (source.@Customer is null ? default(string) : source.@Customer.@FullName)
    };
}
```

Note the last line. Nobody configured `CustomerFullName`; the convention found `Customer.FullName`
and wrote the null guard for it.

Build the project and read `generated/` to see all of it.

## What it will not find, and why that is fine

```csharp
object fromConfigurationOrAPlugin = order.Customer;
var dto = fromConfigurationOrAPlugin.MapTo<CustomerSummaryDto>();
```

The receiver is `object`, so the type is genuinely not known until that line runs and there is
nothing for a compile-time scan to read. This is not a gap to close: it is the case the runtime
engine exists for, and the mapping works with no declaration at all.

The same applies to a type resolved from configuration, loaded from a plugin, or arriving from
reflection. For those, name the pair yourself:

```csharp
[assembly: MapsicleGenerate(typeof(Order), typeof(OrderDto))]
```

Both doors work together, and a pair reached both ways is emitted once.

## The three ways to use Mapsicle

| | Setup | Speed on this graph |
| :--- | :--- | :--- |
| Nothing at all | none | 1.77x hand written |
| Name the hot pairs | one line per pair | **1.00x hand written** |
| `MapsicleGenerateAll` | one line per assembly | **1.00x hand written** |

Start at the top. Move down when a pair turns out to matter, without touching a call site.

## If a pair is not generated

A pair you named that cannot be emitted reports `MSG001` as a warning: you asked for it, so a silent
refusal would be a broken promise. A pair scanning found reports `MSG002` at information level,
because turning one attribute on should not fill your build log with notices about members you never
mentioned. Either way the pair keeps mapping through the engine.
