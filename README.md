# mapsicle_samples

One e-commerce order aggregate, mapped by [Mapsicle](https://github.com/BaryoDev/Mapsicle),
[AutoMapper](https://github.com/AutoMapper/AutoMapper), [Mapperly](https://github.com/riok/mapperly)
and [Mapster](https://github.com/MapsterMapper/Mapster), in a working CRUD API over SQLite.

The point is not that one wins. It is that the same graph goes through all four in the same process,
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
| **Mapsicle** | none, or one line per pair to bind it at compile time, or one line for the whole assembly | none | there is nothing to miss |
| **Mapster** | none | none | there is nothing to miss |
| **Mapperly** | 1 partial method | 12 `RMG020` | a build warning, before you ship |
| **AutoMapper** | 9 `CreateMap` calls | none | the member comes back empty |

Mapster is the closest neighbour here and the most interesting comparison: it also maps by
convention with no setup at all, it is also MIT, and its configuration API is deliberately shaped
like AutoMapper's to make a port mechanical. The differences show up on shapes rather than on
ceremony, which is what `/compare` and the cycle probe are for.

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
dotnet run --project probes/CycleProbe -- mapster      # Stack overflow. exit 134
dotnet run --project probes/CycleProbe -- mapster-safe # PreserveReference on: reference preserved
```


| | On a cycle, default settings | With cycle handling on |
|---|---|---|
| **AutoMapper** 15.1.3 | Preserves the reference. The cycle survives the mapping intact. | same |
| **Mapsicle** 2.2.0 | Stops on a repeated instance and returns a usable object. | same |
| **Mapperly** 4.1.1 | **Stack overflow.** The process aborts, exit 134. | preserves the reference |
| **Mapster** 7.4.0 | **Stack overflow.** The process aborts, exit 134. | preserves the reference |

Both source generators handle every cycle shape correctly with one line, `UseReferenceHandling` on
Mapperly and `PreserveReference` on Mapster, so this is a story about a default rather than about
two broken libraries. It is worth knowing which default you have, because an Entity Framework entity
with a navigation back to its parent is the most ordinary graph in .NET and it is the shape that
does it.

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

`Order` into `OrderDto` is declared too, and the whole nine type graph generates: the nesting, both
collections, the widening, the enum into a string, the enum into a different enum, and both
flattened paths. Read `src/Shop.Api/generated/` after a build to see exactly what came out.

It did not always. Until the generator was widened it refused that pair on `Id`, which widens `int`
to `long`, and `OrderSummaryDto` exists because something had to be left to demonstrate. That is
worth keeping in view when reading any generator's marketing: the shapes it handles and the shapes
your DTOs actually have are different questions.

Three things are still refused on purpose, and each is a refusal rather than a gap:

- **A cyclic graph.** Generated code has no depth ceiling and the engine has one, so emitting a
  mapper that follows a cycle would produce a lane that aborts the process where the other returns.
- **A destination member with a non-public setter.** Reflection writes one and generated code cannot.
- **Anything into a string that is not an enum.** The engine formats through
  `CultureInfo.InvariantCulture`, and re-deriving that in the emitter is how two implementations of
  one rule start disagreeing.

A refused pair keeps mapping through the engine, the build carries on, and the call site does not
change.

Two things worth knowing before you try this on your own code:

- The generated extension is **internal to the assembly that declares the pair**. Declaring it in one
  project does nothing for another; `bench/Shop.Benchmarks` declares it again for exactly that
  reason.
- If you turn on `EmitCompilerGeneratedFiles`, exclude the output folder from compilation. It lands
  inside the project directory, the SDK globs it back in, and every generated type is defined twice.

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
would be reading noise: the standard deviations are around 1 ns on a 13 ns mean, and a second full
run put Mapsicle at 11.81 ns and hand written at 13.57 ns, swapping them. The claim this table
supports is "the same", not "faster".

The gap that is real is the one below them. A declared pair reached through an untyped call site,
`((object)order).MapTo<OrderSummaryDto>()`, runs the identical generated delegate and costs 3.4x,
because the source's runtime type has to be looked up first. Same code, different way in.

All five allocate exactly the destination object and nothing else.

### The whole graph: `Order` into `OrderDto`

Nine types, three levels deep, two collections, a widening, an enum into a string, an enum into a
different enum and two flattened paths. Measured on a quiet machine against the same projection
written out by hand, which is the only baseline worth having.

| | Mean | vs hand written | Allocated |
|---|---:|---:|---:|
| hand written | 288.1 ns | 1.00 | 1.41 KB |
| **Mapsicle, generated** | **288.4 ns** | **1.00** | **1.41 KB (1.00)** |
| Mapsicle, generated via an untyped call site | 309.7 ns | 1.07 | 1.41 KB (1.00) |
| Mapperly | 321.5 ns | 1.12 | 1.50 KB (1.07) |

Three things in that table are worth more than the ordering.

**Generated code is level with hand written.** Not "close to". The same 1.41 KB, and 0.3 ns apart on
a 288 ns call, with standard deviations near 3 ns. There is nothing left to win here.

**The third row is the same code reached a different way.** `((object)order).MapTo<OrderDto>()` runs
the identical generated method; it just pays a `GetType`, a dictionary probe for the delegate, a
second probe to decide on depth tracking and a cast to get there. That 21 ns is what declaring the
pair buys you, and it is the whole reason the generator exists.

**Mapperly's 1.12 is one habit.** Its collection helpers take `IReadOnlyCollection<T>` where the
source member is a `List<T>`, so every `foreach` boxes the struct enumerator on the heap and
dispatches through an interface. Measured in isolation on four collections holding five items, that
costs 38.5 ns and 120 bytes against an indexed loop over the concrete type. The whole-graph gap is
33.4 ns and 90 bytes. It is the same thing, and it is why the allocation column is the only one
where Mapperly is above the baseline.

None of this is a criticism of Mapperly, which is an excellent library and was 11 percent from the
speed limit before anyone went looking. It is a reminder that a generated mapper is only as good as
the loop it decides to write.

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
samples/GenerateAll/       one attribute instead of one declaration per pair
```

## Endpoints

| | |
|---|---|
| `GET /orders?mapper=` | all orders, through `mapsicle`, `mapperly`, `mapster` or `automapper` |
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

Mapsicle 2.2.0, AutoMapper 15.1.3, Riok.Mapperly 4.1.1, Mapster 7.4.0, EF Core 8.0.30, .NET 8.
