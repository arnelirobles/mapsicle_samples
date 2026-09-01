using Mapsicle;

// ============================================================================================
// This is the entire setup. One line, for the whole assembly.
//
// The generator walks this assembly's call sites for .MapTo<TDest>() and emits a mapper for
// every pair whose source type it can read there. Delete the line and everything below still
// works, through the runtime engine, about 1.8x slower.
// ============================================================================================
[assembly: MapsicleGenerateAll]

namespace GenerateAll;

public static class Program
{
    public static void Main()
    {
        var order = Sample();

        // Ordinary calls. None of these changed when the attribute was added, and none of them
        // would change if it were removed. That is what "the call site does not change" means.
        OrderDto full = order.MapTo<OrderDto>()!;
        CustomerSummaryDto summary = order.Customer.MapTo<CustomerSummaryDto>()!;
        List<OrderLineDto> lines = order.Lines.MapTo<OrderLineDto>();

        Console.WriteLine("mapped with no configuration of any kind:");
        Console.WriteLine($"  order      {full.Reference}  {full.Total}");
        Console.WriteLine($"  customer   {full.Customer.FullName}, {full.Customer.Address.City} ({full.Customer.Address.Country.Iso})");
        Console.WriteLine($"  flattened  {full.CustomerFullName}   <- derived from Customer.FullName");
        Console.WriteLine($"  lines      {full.Lines.Count}, first is {full.Lines[0].Product.Sku}");
        Console.WriteLine($"  summary    {summary.FullName} <{summary.Email}>");
        Console.WriteLine($"  collection {lines.Count} line DTOs mapped directly");

        // The one case scanning cannot help with, and does not need to. The receiver is object,
        // so the type is not known until this line runs. There is nothing for a compile-time
        // scan to read, and the pair maps through the runtime engine exactly as before.
        object fromConfigurationOrAPlugin = order.Customer;
        var lateBound = fromConfigurationOrAPlugin.MapTo<CustomerSummaryDto>()!;
        Console.WriteLine($"  late bound {lateBound.FullName}   <- engine handled it, no declaration needed");

        Console.WriteLine();
        Console.WriteLine("Read samples/GenerateAll/generated/ after a build to see what was emitted.");
    }

    private static Order Sample() => new()
    {
        Id = 1001,
        Reference = "SO-1001",
        Total = 24998.50m,
        Customer = new Customer
        {
            Id = 7,
            FullName = "Ada Lovelace",
            Email = "ada@example.test",
            Address = new Address
            {
                Line1 = "1 Analytical Way",
                City = "Cebu",
                Country = new Country { Iso = "PH", Name = "Philippines" },
            },
        },
        Lines =
        {
            new OrderLine { Quantity = 2, Product = new Product { Sku = "PH-1", Title = "Handset", UnitPrice = 9999.75m } },
            new OrderLine { Quantity = 1, Product = new Product { Sku = "AC-9", Title = "Charger", UnitPrice = 4999.00m } },
        },
    };
}
