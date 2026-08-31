# mapsicle_samples

One e-commerce order aggregate, mapped by [Mapsicle](https://github.com/BaryoDev/Mapsicle),
[AutoMapper](https://github.com/AutoMapper/AutoMapper) and [Mapperly](https://github.com/riok/mapperly),
in a working CRUD API over SQLite.

The point is not that one wins. It is that the same graph goes through all three in the same process,
so the differences are visible without a debugger and without taking anyone's word for it.

```bash
dotnet run --project src/Shop.Api          # http://localhost:5199/swagger
curl "http://localhost:5199/compare/1"     # all three, member by member
```

## What is being mapped

`Order` into `OrderDto`. Nine types, and every shape that makes mapping hard:

| Shape | Where |
|---|---|
| Three levels of nesting | `Order.Customer.Address.Country` |
| A collection inside a collection | `Order.Lines[].Discounts[]` |
| Widening | `int Id` into `long Id` |
| Enum into a string | `OrderState.Shipped` into `"Shipped"` |
| Enum into a different enum type | `Domain.Channel` into `Contracts.Channel` |
| `DateTime` into `DateTimeOffset` | `PlacedOn` |
| Nullable | `DateTime? ShippedOn` |
| Flattening, two levels | `CustomerFullName` from `Customer.FullName` |
| Flattening, three levels | `CustomerAddressCity` from `Customer.Address.City` |
| A self referencing type | `Category.Parent` is a `Category` |
| A cycle | `Order.Customer.Orders[0]` is the order |

## What each one costs to set up

Counted from [`src/Shop.Api/Mapping/Mappers.cs`](src/Shop.Api/Mapping/Mappers.cs), which holds all
three side by side.

| | Setup code | Build warnings | Runtime cost of a missed member |
|---|---|---|---|
| **Mapsicle** | none | none | there is nothing to miss |
| **Mapperly** | 1 partial method | 12 `RMG020` | a build warning, before you ship |
| **AutoMapper** | 9 `CreateMap` calls | none | the member comes back empty |

Mapsicle needs no configuration at all: `order.MapTo<OrderDto>()` and nothing else. Mapperly needs
one declared method, and for this graph it derives the enum conversion, the widening, the
`DateTimeOffset` and both flattened members on its own, with no hand written rules. AutoMapper needs
a `CreateMap` for every pair in the graph, and if you forget one the member is silently empty at
runtime.

The 12 Mapperly warnings are its design, not a misconfiguration: it reports every source member no
destination member claims, on the theory that you should say so out loud. That is a real safety net
and a real amount of noise, and which of those it is depends on the codebase.

## Where they disagree

Run `/compare/1` and the current answer is: nowhere.

```
disagreements: 0
OK   Channel                          | Mobile        Mobile        Mobile
OK   Customer.Address.Country.Iso     | PH            PH            PH
OK   CustomerAddressCity (flattened)  | Cebu          Cebu          Cebu
...
```

That took two fixes to Mapsicle, both found by this repository:

- **Three level flattening** ([Mapsicle #56](https://github.com/BaryoDev/Mapsicle/issues/56)).
  `CustomerAddressCity` came back empty because the flattening search descended one level.
- **Enum into a different enum type.** `Channel` came back as `Web`, the zero member, because the
  conversion cascade had no rule for it and the member fell out entirely.

Both returned a wrong value rather than throwing, which is the shape that reaches production.

### The enum rule is a choice, and the two references disagree

Mapping `SrcColour.Amber = 7` into a destination enum whose `Amber = 2`:

| | Result |
|---|---|
| AutoMapper 15.1.3 | `Amber` (2), matched by name |
| Mapperly 4.1.1 | `7`, matched by value, naming no member of the destination |
| Mapsicle 2.2.0 | `Amber` (2), matched by name |

Mapsicle matches by name because the rest of its cascade already does: an enum into a string is
`ToString`, and a string into an enum is a case insensitive `Enum.TryParse`. Matching by value would
mean the same source arrives as `Amber` when it goes through a string and as `7` when it does not.

Pinned in Mapsicle by `ConversionGapTests.AnEnumIsMatchedByNameNotByValue`, which fails if the rule
ever changes to by value.

## The cycle

`Order.Customer.Orders[0]` is the order. Point a mapper at a destination that exposes the back
reference and the three do three different things. Run it yourself, one at a time, because one of
them ends the process:

```bash
dotnet run --project probes/CycleProbe -- mapsicle     # cycle expanded to 15 levels, then stopped
dotnet run --project probes/CycleProbe -- automapper   # reference preserved
dotnet run --project probes/CycleProbe -- mapperly     # Stack overflow. exit 134
```


| | On a cycle |
|---|---|
| **AutoMapper** 15.1.3 | Preserves the reference. The mapped object *is* its own `Customer.Orders[0]`, so the cycle survives the mapping intact. |
| **Mapsicle** 2.2.0 | Expands it to a depth ceiling, 15 levels, then stops. Terminates and returns a usable object, but the output holds 15 distinct copies where the input had one. |
| **Mapperly** 4.1.1 | Follows it until the stack overflows. The process aborts, exit 134. |

AutoMapper has the best answer here and it is worth saying so plainly: its output has the same shape
as its input. Mapsicle's is safe but lossy. Mapperly's takes the process with it.

That is why `CustomerDto` in this repository has no `Orders` member. It is not an oversight, it is
the only shape all three survive.

## Two Mapsicle lanes

Mapsicle maps anything with no setup, by compiling an expression tree the first time it sees a pair.
Declaring a pair moves it to generated code instead:

```csharp
[assembly: MapsicleGenerate(typeof(Order), typeof(OrderSummaryDto))]
```

The call site does not change. `order.MapTo<OrderSummaryDto>()` still compiles, and now binds to an
emitted method rather than to the engine. See
`src/Shop.Api/generated/` after a build.

`Order` into `OrderDto` is also declared, and the generator **refuses it**:

```
warning MSG001: Cannot generate a mapper from 'Order' to 'OrderDto': 'Id' is a member the
engine maps and this generator cannot emit ... The pair still maps through the runtime engine.
```

That refusal is the design. `Id` widens `int` to `long`, the emitter has no widening rule, and
emitting a mapper that drops the member would return less than the engine does. The pair keeps
working, at engine speed, and the build says so.

Two things follow that are worth knowing before you try this on your own code:

- The generated extension is **internal to the assembly that declares the pair**. Declaring it in one
  project does nothing for another; `bench/Shop.Benchmarks` declares it again for exactly that
  reason.
- If you turn on `EmitCompilerGeneratedFiles`, exclude the output folder from compilation.
  It lands inside the project directory, the SDK globs it back in, and every generated type is
  defined twice.

## Numbers

Apple M1, .NET 8.0.24, Release, BenchmarkDotNet 0.13.12 default job. `dotnet run -c Release
--project bench/Shop.Benchmarks -- --filter '*'`.

### The flat pair: `Order` into `OrderSummaryDto`

Five members, no nesting. Every entry maps the same source into the same destination, including the
baseline.

| | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| hand written | 12.91 ns | 1.00 | 64 B |
| **Mapsicle, compile-time bound** | **12.74 ns** | **1.00** | 64 B |
| Mapperly | 13.18 ns | 1.03 | 64 B |
| Mapsicle, registry lookup | 43.60 ns | 3.41 | 64 B |
| AutoMapper | 59.97 ns | 4.69 | 64 B |

The first three are the same number. Mapsicle bound at compile time, Mapperly and code a person
would write are indistinguishable at this scale, and reading anything into the ordering between them
would be reading noise: the standard deviations are around 1 ns on a 13 ns mean.

The gap that is real is the one below them. A declared pair reached through an untyped call site,
`((object)order).MapTo<OrderSummaryDto>()`, runs the identical generated delegate and costs 3.4x,
because the source's runtime type has to be looked up first. Same code, different way in.

All five allocate exactly the destination object and nothing else.

### The whole graph: `Order` into `OrderDto`

Nine types, three levels deep, two collections. Mapsicle's generator refuses this pair, so its entry
is the runtime engine. No hand written baseline: this projection is around sixty lines by hand, and
keeping it correct is the work these libraries exist to remove.

| | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Mapperly | 396.1 ns | 1.00 | 1.52 KB |
| Mapsicle, engine | 639.7 ns | 1.62 | 1.41 KB |
| AutoMapper | 852.3 ns | 2.16 | 1.48 KB |

Mapperly wins, and it should: it emits straight-line C# for every type in the graph. Mapsicle's
engine is 1.6x that with no configuration at all, and allocates the least of the three. AutoMapper
is 2.2x Mapperly, with nine `CreateMap` calls to keep in sync.

The honest summary of both tables: declare the pair and Mapsicle is hand written speed; do not, and
it sits between Mapperly and AutoMapper while asking for nothing.

## This is a sample, not a template

The API has no authentication, no authorization and no rate limiting, and it logs SQL to the
console. It exists to show three mappers producing the same object. Do not lift `Program.cs` into
anything that faces a network.

## Layout

```
src/Shop.Api/
  Domain/Aggregate.cs      the entities
  Contracts/Dtos.cs        the read model
  Mapping/Mappers.cs       all three mappers, side by side
  Data/ShopContext.cs      EF Core, SQLite, seeded on first run
  Program.cs               minimal API: CRUD plus /compare
bench/Shop.Benchmarks/     BenchmarkDotNet
probes/CycleProbe/         the cycle table above, runnable
```

## Endpoints

| | |
|---|---|
| `GET /orders?mapper=` | all orders, through `mapsicle`, `mapperly` or `automapper` |
| `GET /orders/{id}?mapper=` | one order |
| `GET /orders/{id}/summary` | the flat projection, through generated code |
| `POST /orders` | create, from a request DTO holding only what a caller may set |
| `DELETE /orders/{id}` | delete |
| `GET /compare/{id}` | all three, member by member, with disagreements counted |

`POST` takes a `CreateOrderRequest` rather than an `Order` on purpose. A convention mapper pointed at
a request body will set anything whose name lines up, `Total` and `State` included. That is true of
all three of these, and the DTO is the control.

## Running against a local Mapsicle checkout

If a Mapsicle checkout sits beside this one at `../Mapsicle`, the build uses it instead of the
published package. That is how these samples were verified before 2.2.0 shipped. Force the package
with `-p:UseLocalMapsicle=false`.

## Versions

Mapsicle 2.2.0 is not on NuGet yet. Until it is, clone
[BaryoDev/Mapsicle](https://github.com/BaryoDev/Mapsicle) beside this repository as `../Mapsicle`
and the build picks it up automatically.

Mapsicle 2.2.0, AutoMapper 15.1.3, Riok.Mapperly 4.1.1, EF Core 8.0.30, .NET 8.
