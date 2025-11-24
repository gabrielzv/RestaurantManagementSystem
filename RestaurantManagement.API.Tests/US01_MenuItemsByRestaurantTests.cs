using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.API.Controllers;
using RestaurantManagement.Infrastructure.Data;
using System.IO;
using Xunit;

namespace RestaurantManagement.API.Tests;

public class US01_MenuItemsByRestaurantTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public US01_MenuItemsByRestaurantTests()
    {
        var dbPath = Path.GetTempFileName();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource={dbPath}")
            .Options;

        _context = new ApplicationDbContext(options);
        EnsureMenuTables();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private void EnsureMenuTables()
    {
        using var conn = _context.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Create Restaurants table
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Restaurants (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Address TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );";
        cmd.ExecuteNonQuery();

        // Create MenuItems table
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS MenuItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            Price REAL NOT NULL,
            Category TEXT,
            IsAvailable INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );";
        cmd.ExecuteNonQuery();

        // Create join table
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS RestaurantMenuItems (
            RestaurantId INTEGER NOT NULL,
            MenuItemId INTEGER NOT NULL,
            PRIMARY KEY (RestaurantId, MenuItemId)
        );";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task GetMenuItemsForRestaurant_Returns_Associated_Items()
    {
        // Arrange: insert a restaurant, two menu items and associate one to the restaurant
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "INSERT INTO Restaurants (Name) VALUES ('Test Resto');";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt) VALUES ('Pizza', 'Cheesy', 10.5, 'Main', 1, datetime('now'));";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt) VALUES ('Salad', 'Fresh', 5.25, 'Starter', 1, datetime('now'));";
        cmd.ExecuteNonQuery();

        // Get inserted ids
        cmd.CommandText = "SELECT Id FROM Restaurants LIMIT 1;";
        var restId = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT Id FROM MenuItems WHERE Name='Pizza' LIMIT 1;";
        var pizzaId = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT Id FROM MenuItems WHERE Name='Salad' LIMIT 1;";
        var saladId = Convert.ToInt32(cmd.ExecuteScalar());

        // Associate only pizza with restaurant
        cmd.CommandText = $"INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId) VALUES ({restId}, {pizzaId});";
        cmd.ExecuteNonQuery();

        var controller = new RestaurantsController(_context);

        // Act
        var result = await controller.GetMenuItemsForRestaurant(restId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var ok = result as OkObjectResult;
        Assert.NotNull(ok?.Value);
        var list = ok.Value as System.Collections.IEnumerable;
        Assert.NotNull(list);

        // Ensure only one item returned and its name is Pizza
        var items = new System.Collections.Generic.List<dynamic>();
        foreach (var it in list!) items.Add(it);
        Assert.Single(items);
        Assert.Equal("Pizza", (string)items[0].Name);
    }
}
