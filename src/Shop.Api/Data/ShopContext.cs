using Microsoft.EntityFrameworkCore;
using Shop.Api.Domain;

namespace Shop.Api.Data;

public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Country> Countries => Set<Country>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>().Property(o => o.Total).HasColumnType("decimal(18,2)");
        b.Entity<Product>().Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
        b.Entity<Payment>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
        b.Entity<Discount>().Property(d => d.Percent).HasColumnType("decimal(5,2)");

        // The cycle, made explicit. Customer lists its orders and each order points back.
        b.Entity<Customer>().HasMany(c => c.Orders).WithOne(o => o.Customer);

        // Lines, payments and discounts belong to their parent and go with it. Without this,
        // deleting an order leaves rows behind and SQLite refuses with a foreign key error.
        b.Entity<Order>().HasMany(o => o.Lines).WithOne().IsRequired().OnDelete(DeleteBehavior.Cascade);
        b.Entity<Order>().HasMany(o => o.Payments).WithOne().IsRequired().OnDelete(DeleteBehavior.Cascade);
        b.Entity<OrderLine>().HasMany(l => l.Discounts).WithOne().IsRequired().OnDelete(DeleteBehavior.Cascade);
    }
}

public static class Seed
{
    /// <summary>Puts one deliberately awkward order in the database.</summary>
    /// <remarks>
    /// Small on purpose. The value here is not volume, it is that every row participates in a
    /// mapping shape the three mappers disagree about: a category that points at a parent
    /// category, a line with discounts, two payments of different kinds, and a customer whose
    /// city is three levels below the order.
    /// </remarks>
    public static void Populate(ShopContext db)
    {
        if (db.Orders.Any()) return;

        var order = BuildOrder();
        var second = new Order
        {
            Reference = "SO-1002",
            State = OrderState.Draft,
            Channel = Domain.Channel.Web,
            Customer = order.Customer,
            Total = 4999.00m,
            PlacedOn = new DateTime(2026, 8, 30, 11, 0, 0, DateTimeKind.Utc),
            Lines = { new OrderLine { Quantity = 1, Product = order.Lines[1].Product } },
        };

        db.Orders.AddRange(order, second);
        db.SaveChanges();
    }

    /// <summary>The same graph the seeder stores, built in memory so the benchmarks can use it.</summary>
    public static Order BuildOrder()
    {
        var ph = new Country { Iso = "PH", Name = "Philippines" };
        var electronics = new Category { Name = "Electronics" };
        var phones = new Category { Name = "Phones", Parent = electronics };

        var customer = new Customer
        {
            FullName = "Ada Lovelace",
            Email = "ada@example.test",
            Address = new Address { Line1 = "1 Analytical Way", City = "Cebu", Country = ph },
        };

        var handset = new Product
        {
            Sku = "PH-1", Title = "Handset", Category = phones, UnitPrice = 9999.75m,
        };
        var charger = new Product
        {
            Sku = "AC-9", Title = "Charger", Category = electronics, UnitPrice = 4999.00m,
        };

        var order = new Order
        {
            Reference = "SO-1001",
            State = OrderState.Shipped,
            Channel = Domain.Channel.Mobile,
            Customer = customer,
            Total = 24998.50m,
            PlacedOn = new DateTime(2026, 8, 29, 9, 30, 0, DateTimeKind.Utc),
            ShippedOn = new DateTime(2026, 8, 31, 14, 0, 0, DateTimeKind.Utc),
            Lines =
            {
                new OrderLine
                {
                    Quantity = 2, Product = handset,
                    Discounts = { new Discount { Code = "LAUNCH", Percent = 10m } },
                },
                new OrderLine { Quantity = 1, Product = charger },
            },
            Payments =
            {
                new Payment { Kind = "card", Amount = 20000m },
                new Payment { Kind = "wallet", Amount = 4998.50m },
            },
        };

        // The cycle: the customer lists the order that points back at it.
        customer.Orders.Add(order);

        return order;
    }

    /// <summary>Loads an order with the whole graph, because a mapper can only map what is loaded.</summary>
    public static IQueryable<Order> WithGraph(this DbSet<Order> orders) =>
        orders
            .Include(o => o.Customer).ThenInclude(c => c.Address).ThenInclude(a => a.Country)
            .Include(o => o.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Category)
            .Include(o => o.Lines).ThenInclude(l => l.Discounts)
            .Include(o => o.Payments);
}
