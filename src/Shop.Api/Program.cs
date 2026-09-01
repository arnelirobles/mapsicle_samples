using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Shop.Api.Contracts;
using Shop.Api.Data;
using Shop.Api.Domain;
using Shop.Api.Mapping;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShopContext>(o => o.UseSqlite("Data Source=shop.db"));
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<ShopProfile>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// All three, registered side by side. The endpoints pick one by name so the same request can be
// served through any of them, which is what makes the comparison honest: same data, same route.
builder.Services.AddSingleton<MapsicleOrderMapper>();
builder.Services.AddSingleton<MapperlyOrderMapper>();
builder.Services.AddSingleton<MapsterOrderMapper>();
builder.Services.AddSingleton<AutoMapperOrderMapper>();
builder.Services.AddSingleton<MapperRegistry>();

var app = builder.Build();

// Compiled once so the first request does not pay for it, the same courtesy the other three get.
Mapster.TypeAdapterConfig.GlobalSettings.Compile();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
    db.Database.EnsureCreated();
    Seed.Populate(db);
}

app.UseSwagger();
app.UseSwaggerUI();

// ---- read -------------------------------------------------------------------------------------

app.MapGet("/orders", async (ShopContext db, MapperRegistry mappers, string? mapper) =>
{
    var chosen = mappers.Resolve(mapper);
    if (chosen is null) return Results.BadRequest(MapperRegistry.Names);

    var orders = await db.Orders.WithGraph().AsNoTracking().ToListAsync();
    return Results.Ok(orders.Select(chosen.Map));
});

app.MapGet("/orders/{id:int}", async (int id, ShopContext db, MapperRegistry mappers, string? mapper) =>
{
    var chosen = mappers.Resolve(mapper);
    if (chosen is null) return Results.BadRequest(MapperRegistry.Names);

    var order = await db.Orders.WithGraph().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    return order is null ? Results.NotFound() : Results.Ok(chosen.Map(order));
});

// ---- write ------------------------------------------------------------------------------------

// The request type holds only what a caller may set. Mapping a request body straight onto an entity
// lets a caller set anything whose name lines up, Total and State included. That is true of every
// convention mapper, Mapsicle included, and the DTO is the control.
app.MapPost("/orders", async (CreateOrderRequest request, ShopContext db, MapperRegistry mappers, string? mapper) =>
{
    var chosen = mappers.Resolve(mapper);
    if (chosen is null) return Results.BadRequest(MapperRegistry.Names);

    var customer = await db.Customers.FindAsync(request.CustomerId);
    if (customer is null) return Results.BadRequest($"no customer {request.CustomerId}");

    var order = new Order
    {
        Reference = request.Reference,
        State = OrderState.Draft,
        Channel = (Shop.Api.Domain.Channel)request.Channel,
        Customer = customer,
        PlacedOn = DateTime.UtcNow,
    };

    foreach (var line in request.Lines)
    {
        var product = await db.Products.FindAsync(line.ProductId);
        if (product is null) return Results.BadRequest($"no product {line.ProductId}");
        order.Lines.Add(new OrderLine { Product = product, Quantity = line.Quantity });
    }

    order.Total = order.Lines.Sum(l => l.Quantity * l.Product.UnitPrice);

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    var saved = await db.Orders.WithGraph().AsNoTracking().FirstAsync(o => o.Id == order.Id);
    return Results.Created($"/orders/{order.Id}", chosen.Map(saved));
});

app.MapDelete("/orders/{id:int}", async (int id, ShopContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound();
    db.Orders.Remove(order);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// The pair the generator accepts. Same call as everywhere else, but for this pair the compiler
// bound it to emitted code rather than to the engine, so no expression tree is ever built for it.
app.MapGet("/orders/{id:int}/summary", async (int id, ShopContext db) =>
{
    var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    return order is null ? Results.NotFound() : Results.Ok(order.MapTo<OrderSummaryDto>());
});

// ---- the comparison -----------------------------------------------------------------------------

/// Maps the same order with all three and reports where they disagree. This is the endpoint the
/// article points at: the differences are real and they are visible without a debugger.
app.MapGet("/compare/{id:int}", async (int id, ShopContext db, MapperRegistry mappers) =>
{
    var order = await db.Orders.WithGraph().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    if (order is null) return Results.NotFound();

    var results = mappers.All.ToDictionary(m => m.Name, m => Probe(m, order));
    var members = results.Values.SelectMany(r => r.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal);

    var rows = members.Select(member => new
    {
        Member = member,
        Values = results.ToDictionary(r => r.Key, r => r.Value.GetValueOrDefault(member)),
        Agrees = results.Values.Select(r => r.GetValueOrDefault(member)).Distinct().Count() == 1,
    }).ToList();

    return Results.Ok(new { OrderId = id, Disagreements = rows.Count(r => !r.Agrees), Members = rows });
});

app.Run();

/// <summary>Reads the members the three mappers are most likely to disagree about.</summary>
static Dictionary<string, string?> Probe(IOrderMapper mapper, Order order)
{
    var dto = mapper.Map(order);
    return new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Id"] = dto.Id.ToString(),
        ["State"] = dto.State,
        ["Channel"] = dto.Channel.ToString(),
        ["PlacedOn"] = dto.PlacedOn.ToString("O"),
        ["ShippedOn"] = dto.ShippedOn?.ToString("O"),
        ["Customer.FullName"] = dto.Customer.FullName,
        ["Customer.Address.City"] = dto.Customer.Address.City,
        ["Customer.Address.Country.Iso"] = dto.Customer.Address.Country.Iso,
        ["CustomerFullName (flattened)"] = dto.CustomerFullName,
        ["CustomerAddressCity (flattened)"] = dto.CustomerAddressCity,
        ["Lines.Count"] = dto.Lines.Count.ToString(),
        ["Lines[0].Product.Category.Name"] = dto.Lines.FirstOrDefault()?.Product.Category.Name,
        ["Lines[0].Discounts.Count"] = dto.Lines.FirstOrDefault()?.Discounts.Count.ToString(),
        ["Payments.Count"] = dto.Payments.Count.ToString(),
    };
}

/// <summary>Picks a mapper by name, so one route can be served by any of the three.</summary>
public sealed class MapperRegistry(
    MapsicleOrderMapper mapsicle,
    MapperlyOrderMapper mapperly,
    MapsterOrderMapper mapster,
    AutoMapperOrderMapper autoMapper)
{
    public IReadOnlyList<IOrderMapper> All { get; } = [mapsicle, mapperly, mapster, autoMapper];

    public static string Names => "mapper must be one of: mapsicle, mapperly, mapster, automapper";

    public IOrderMapper? Resolve(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? mapsicle
            : All.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
}
