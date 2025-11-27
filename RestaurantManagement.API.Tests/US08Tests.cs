using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RestaurantManagement.API.Controllers;
using RestaurantManagement.API.Hubs;
using RestaurantManagement.Infrastructure.Data;
using Xunit;

namespace RestaurantManagement.API.Tests;

/// <summary>
/// Tests for US08 - Client receipt view via orders by access code.
/// </summary>
public class US08Tests
{
    [Fact]
    public async Task GetByAccessCode_ShouldReturnBadRequest_WhenCodeIsMissing()
    {
        await using var context = await TestContext.CreateAsync();

        var result = await context.OrdersController.GetByAccessCode(" ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetByAccessCode_ShouldReturnEmptyPayload_WhenCodeDoesNotExist()
    {
        await using var context = await TestContext.CreateAsync();

        var result = await context.OrdersController.GetByAccessCode("NOPE");
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = TestContext.ParseReceipt(ok.Value);

        Assert.Empty(payload.Orders);
        Assert.Equal(0m, payload.Subtotal);
    }

    [Fact]
    public async Task GetByAccessCode_ShouldReturnEmptyPayload_WhenCodeHasNoTableAssigned()
    {
        await using var context = await TestContext.CreateAsync();
        var code = await context.CreateAccessCodeAsync(restaurantId: 1, tableNumber: null, code: "VOID");

        var result = await context.OrdersController.GetByAccessCode(code);
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = TestContext.ParseReceipt(ok.Value);

        Assert.Empty(payload.Orders);
        Assert.Equal(0m, payload.Subtotal);
    }

    [Fact]
    public async Task GetByAccessCode_ShouldReturnOnlySentAndReadyOrdersWithSubtotal()
    {
        await using var context = await TestContext.CreateAsync();
        var code = await context.CreateAccessCodeAsync(1, "Mesa-12", code: "M12");

        var sentId = await context.InsertOrderAsync(
            restaurantId: 1,
            tableNumber: "Mesa-12",
            status: "Sent",
            new[]
            {
                new TestContext.OrderItemData("Casado", 3500m, 1),
                new TestContext.OrderItemData("Refresco", 1200m, 2)
            });

        var readyId = await context.InsertOrderAsync(
            restaurantId: 1,
            tableNumber: "Mesa-12",
            status: "Ready",
            new[] { new TestContext.OrderItemData("Postre", 2400m, 1) });

        // Should be ignored (draft status)
        await context.InsertOrderAsync(
            restaurantId: 1,
            tableNumber: "Mesa-12",
            status: "Draft",
            new[] { new TestContext.OrderItemData("Entrada", 1000m, 1) });

        // Should be ignored (different table)
        await context.InsertOrderAsync(
            restaurantId: 1,
            tableNumber: "Mesa-99",
            status: "Sent",
            new[] { new TestContext.OrderItemData("Agua", 800m, 1) });

        // Should be ignored (different restaurant)
        await context.InsertOrderAsync(
            restaurantId: 2,
            tableNumber: "Mesa-12",
            status: "Ready",
            new[] { new TestContext.OrderItemData("Cafe", 900m, 1) });

        var result = await context.OrdersController.GetByAccessCode(code);
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = TestContext.ParseReceipt(ok.Value);

        Assert.Equal(2, payload.Orders.Count);
        Assert.Equal(8300m, payload.Subtotal);

        var sentOrder = Assert.Single(payload.Orders, o => o.Id == sentId);
        Assert.Equal("Sent", sentOrder.Status);
        Assert.Equal(5900m, sentOrder.Total);
        Assert.Equal(2, sentOrder.Items.Count);
        var beverage = Assert.Single(sentOrder.Items, i => i.Name == "Refresco");
        Assert.Equal(2, beverage.Quantity);
        Assert.Equal(1200m, beverage.Price);
        Assert.Equal(2400m, beverage.LineTotal);

        var readyOrder = Assert.Single(payload.Orders, o => o.Id == readyId);
        Assert.Equal("Ready", readyOrder.Status);
        Assert.Equal(2400m, readyOrder.Total);
        var dessert = Assert.Single(readyOrder.Items);
        Assert.Equal("Postre", dessert.Name);
        Assert.Equal(1, dessert.Quantity);
        Assert.Equal(2400m, dessert.LineTotal);
    }

    private sealed class TestContext : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly string _connectionString;
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private TestContext(string databasePath, string connectionString, ApplicationDbContext dbContext, IConfiguration configuration)
        {
            _databasePath = databasePath;
            _connectionString = connectionString;
            _dbContext = dbContext;
            _configuration = configuration;
            OrdersController = new OrdersController(dbContext, configuration, FakeHubContext.Instance);
        }

        public OrdersController OrdersController { get; }

        public static async Task<TestContext> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"us08_{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={dbPath};";
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:DefaultConnection", connectionString }
                })
                .Build();

            return new TestContext(dbPath, connectionString, dbContext, config);
        }

        public async Task<string> CreateAccessCodeAsync(int restaurantId, string? tableNumber, string? code = null, int? waiterId = null)
        {
            await EnsureAccessCodesTableExistsAsync();

            var finalCode = code ?? Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            var now = DateTime.UtcNow.ToString("o");

            await _dbContext.Database.ExecuteSqlRawAsync(
                @"INSERT INTO AccessCodes (Code, RestaurantId, TableNumber, WaiterId, CreatedAt, ExpiresAt, UsedAt, IsActive)
                  VALUES ($code, $restaurantId, $tableNumber, $waiterId, $createdAt, NULL, NULL, 1);",
                new SqliteParameter("$code", finalCode),
                new SqliteParameter("$restaurantId", restaurantId),
                new SqliteParameter("$tableNumber", tableNumber ?? (object)DBNull.Value),
                new SqliteParameter("$waiterId", waiterId ?? (object)DBNull.Value),
                new SqliteParameter("$createdAt", now));

            return finalCode;
        }

        public record struct OrderItemData(string Name, decimal Price, int Quantity);

        public async Task<int> InsertOrderAsync(int restaurantId, string tableNumber, string status, IEnumerable<OrderItemData> items)
        {
            await EnsureOrdersTablesExistAsync();

            var itemList = items.ToList();
            var createdAt = DateTime.UtcNow.ToString("o");
            var subtotal = itemList.Sum(i => i.Price * i.Quantity);

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await using (DbCommand insertOrder = connection.CreateCommand())
            {
                insertOrder.Transaction = transaction;
                insertOrder.CommandText = @"INSERT INTO Orders (RestaurantId, WaiterId, TableNumber, Status, CreatedAt, UpdatedAt, Total)
                                            VALUES ($restaurantId, NULL, $tableNumber, $status, $createdAt, NULL, $total);";
                insertOrder.Parameters.Add(new SqliteParameter("$restaurantId", restaurantId));
                insertOrder.Parameters.Add(new SqliteParameter("$tableNumber", tableNumber));
                insertOrder.Parameters.Add(new SqliteParameter("$status", status));
                insertOrder.Parameters.Add(new SqliteParameter("$createdAt", createdAt));
                insertOrder.Parameters.Add(new SqliteParameter("$total", subtotal));
                await insertOrder.ExecuteNonQueryAsync();
            }

            int orderId;
            await using (DbCommand idCmd = connection.CreateCommand())
            {
                idCmd.Transaction = transaction;
                idCmd.CommandText = "SELECT last_insert_rowid();";
                orderId = Convert.ToInt32(await idCmd.ExecuteScalarAsync());
            }

            foreach (var item in itemList)
            {
                await using DbCommand insertItem = connection.CreateCommand();
                insertItem.Transaction = transaction;
                insertItem.CommandText = @"INSERT INTO OrderItems (OrderId, MenuItemId, Name, Price, Quantity, Notes)
                                              VALUES ($orderId, NULL, $name, $price, $quantity, NULL);";
                insertItem.Parameters.Add(new SqliteParameter("$orderId", orderId));
                insertItem.Parameters.Add(new SqliteParameter("$name", item.Name));
                insertItem.Parameters.Add(new SqliteParameter("$price", item.Price));
                insertItem.Parameters.Add(new SqliteParameter("$quantity", item.Quantity));
                await insertItem.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return orderId;
        }

        public static ReceiptResponse ParseReceipt(object? value)
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var subtotal = root.TryGetProperty("subtotal", out var subElement) && subElement.ValueKind == JsonValueKind.Number
                ? subElement.GetDecimal()
                : 0m;

            var orders = new List<ReceiptOrder>();
            if (root.TryGetProperty("orders", out var ordersElement) && ordersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var orderEl in ordersElement.EnumerateArray())
                {
                    var id = orderEl.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                    var status = orderEl.TryGetProperty("status", out var statusEl) && statusEl.ValueKind != JsonValueKind.Null
                        ? statusEl.GetString()
                        : null;
                    var createdAt = orderEl.TryGetProperty("createdAt", out var createdEl) && createdEl.ValueKind != JsonValueKind.Null
                        ? createdEl.GetString()
                        : null;
                    var total = orderEl.TryGetProperty("total", out var totalEl) && totalEl.ValueKind == JsonValueKind.Number
                        ? totalEl.GetDecimal()
                        : 0m;

                    var items = new List<ReceiptItem>();
                    if (orderEl.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var itemEl in itemsEl.EnumerateArray())
                        {
                            var name = itemEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind != JsonValueKind.Null
                                ? nameEl.GetString() ?? string.Empty
                                : string.Empty;
                            var qty = itemEl.TryGetProperty("quantity", out var qtyEl) && qtyEl.ValueKind == JsonValueKind.Number
                                ? qtyEl.GetInt32()
                                : 0;
                            var price = itemEl.TryGetProperty("price", out var priceEl) && priceEl.ValueKind == JsonValueKind.Number
                                ? priceEl.GetDecimal()
                                : 0m;
                            var lineTotal = itemEl.TryGetProperty("lineTotal", out var lineEl) && lineEl.ValueKind == JsonValueKind.Number
                                ? lineEl.GetDecimal()
                                : 0m;
                            items.Add(new ReceiptItem(name, qty, price, lineTotal));
                        }
                    }

                    orders.Add(new ReceiptOrder(id, status, createdAt, total, items));
                }
            }

            return new ReceiptResponse(subtotal, orders);
        }

        public readonly record struct ReceiptResponse(decimal Subtotal, List<ReceiptOrder> Orders);
        public readonly record struct ReceiptOrder(int Id, string? Status, string? CreatedAt, decimal Total, List<ReceiptItem> Items);
        public readonly record struct ReceiptItem(string Name, int Quantity, decimal Price, decimal LineTotal);

        private async Task EnsureOrdersTablesExistAsync()
        {
            const string ordersSql = @"CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RestaurantId INTEGER NOT NULL,
                WaiterId INTEGER,
                TableNumber TEXT,
                Status TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT,
                Total NUMERIC
            );";

            const string itemsSql = @"CREATE TABLE IF NOT EXISTS OrderItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                MenuItemId INTEGER,
                Name TEXT,
                Price NUMERIC,
                Quantity INTEGER NOT NULL,
                Notes TEXT
            );";

            await _dbContext.Database.ExecuteSqlRawAsync(ordersSql);
            await _dbContext.Database.ExecuteSqlRawAsync(itemsSql);
        }

        private async Task EnsureAccessCodesTableExistsAsync()
        {
            const string sql = @"CREATE TABLE IF NOT EXISTS AccessCodes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL,
                RestaurantId INTEGER NOT NULL,
                TableNumber TEXT,
                WaiterId INTEGER,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT,
                UsedAt TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );";

            await _dbContext.Database.ExecuteSqlRawAsync(sql);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _dbContext.Database.CloseConnection();
            }
            catch
            {
                // ignore connection close failures during cleanup
            }

            await _dbContext.DisposeAsync();
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch
            {
                // ignore cleanup issues
            }
        }

        private sealed class FakeHubContext : IHubContext<OrderHub>
        {
            public static IHubContext<OrderHub> Instance { get; } = new FakeHubContext();

            public IHubClients Clients { get; } = new FakeHubClients();
            public IGroupManager Groups { get; } = new FakeGroupManager();

            private sealed class FakeHubClients : IHubClients
            {
                private static readonly IClientProxy Proxy = new FakeClientProxy();

                public IClientProxy All => Proxy;
                public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
                public IClientProxy Client(string connectionId) => Proxy;
                public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
                public IClientProxy Group(string groupName) => Proxy;
                public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
                public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
                public IClientProxy User(string userId) => Proxy;
                public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
            }

            private sealed class FakeGroupManager : IGroupManager
            {
                public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
                public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

            private sealed class FakeClientProxy : IClientProxy
            {
                public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
        }
    }
}
