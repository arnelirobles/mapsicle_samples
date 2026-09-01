using AutoMapper;
using Mapsicle;
using Mapster;
using Riok.Mapperly.Abstractions;

// The three answers to a cycle, run one at a time because one of them ends the process.
//
//   dotnet run --project probes/CycleProbe -- mapsicle
//   dotnet run --project probes/CycleProbe -- automapper
//   dotnet run --project probes/CycleProbe -- mapperly     <- aborts, and that is the result
//   dotnet run --project probes/CycleProbe -- mapster      <- aborts too
//
// Both source generators handle this correctly with one line of configuration, so the point is the
// default rather than the library: UseReferenceHandling on Mapperly, PreserveReference on Mapster.
//
// Unlike the DTOs the API uses, the destination here exposes the back reference, so the cycle is
// there for the mapper to follow rather than cut off by the shape of the destination.

public class Cust { public string Name { get; set; } = ""; public List<Ord> Orders { get; set; } = new(); }
public class Ord { public int Id { get; set; } public Cust Customer { get; set; } = new(); }
public class CustDto { public string Name { get; set; } = ""; public List<OrdDto> Orders { get; set; } = new(); }
public class OrdDto { public int Id { get; set; } public CustDto Customer { get; set; } = new(); }

[Riok.Mapperly.Abstractions.Mapper]
public partial class CycleMapper
{
    public partial OrdDto Map(Ord source);
}

public static class Probe
{
    private const int WalkLimit = 200_000;

    public static void Main(string[] args)
    {
        var which = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        switch (which)
        {
            case "mapsicle":
                Report("Mapsicle", () => ((object)Build()).MapTo<OrdDto>());
                break;

            case "automapper":
                var automapper = new MapperConfiguration(
                    c => { c.CreateMap<Ord, OrdDto>(); c.CreateMap<Cust, CustDto>(); },
                    Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
                Report("AutoMapper", () => automapper.Map<OrdDto>(Build()));
                break;

            case "mapperly":
                Console.WriteLine("Mapperly follows the cycle by default. Expect a stack overflow and a non-zero exit.");
                Report("Mapperly", () => new CycleMapper().Map(Build()));
                break;

            case "mapster":
                Console.WriteLine("Mapster follows the cycle by default. Expect a stack overflow and a non-zero exit.");
                Report("Mapster", () => Build().Adapt<OrdDto>());
                break;

            case "mapster-safe":
                TypeAdapterConfig.GlobalSettings.Default.PreserveReference(true);
                Report("Mapster, PreserveReference on", () => Build().Adapt<OrdDto>());
                break;

            default:
                Console.WriteLine("pass one of: mapsicle, automapper, mapperly, mapster, mapster-safe");
                break;
        }
    }

    private static Ord Build()
    {
        var customer = new Cust { Name = "Ada" };
        var order = new Ord { Id = 1, Customer = customer };
        customer.Orders.Add(order);
        return order;
    }

    private static void Report(string name, Func<OrdDto?> map)
    {
        var mapped = map();

        // A mapper that preserved the reference gives back an object that is its own
        // Customer.Orders[0], and the walk below never ends. One that expanded the cycle gives a
        // chain of distinct copies, and the walk counts them.
        var preserved = ReferenceEquals(mapped, mapped?.Customer?.Orders.FirstOrDefault());

        var depth = 0;
        var cursor = mapped;
        while (cursor?.Customer?.Orders is { Count: > 0 } && depth < WalkLimit)
        {
            cursor = cursor.Customer.Orders[0];
            depth++;
        }

        Console.WriteLine(preserved
            ? $"{name}: reference preserved. The mapped object is its own Customer.Orders[0], "
              + "so the cycle survived the mapping intact."
            : $"{name}: cycle expanded to {depth} levels, then stopped. Terminates, but the output "
              + $"holds {depth} distinct copies where the input had one.");
    }
}
