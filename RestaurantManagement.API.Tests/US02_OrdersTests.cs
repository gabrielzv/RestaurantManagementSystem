using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using RestaurantManagement.API.Controllers;
using RestaurantManagement.API.Hubs;
using RestaurantManagement.Infrastructure.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;

namespace RestaurantManagement.API.Tests;

public class US02_OrdersTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly string _dbPath;

    public US02_OrdersTests()
    {
        _dbPath = Path.GetTempFileName();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource={_dbPath}")
            .Options;

        _context = new ApplicationDbContext(options);
        // Ensure database file and basic creation
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        try
        {
            _context.Database.CloseConnection();
        }
        catch { }
        _context.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    // Minimal fake hub context so controller can be constructed
    private class FakeClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class FakeHubClients : IHubClients
    {
        public IClientProxy All => new FakeClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName) => new FakeClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy User(string userId) => new FakeClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
    }

    private class FakeGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private IHubContext<OrderHub> BuildFakeHubContext() => new FakeHubContext();

    private class FakeHubContext : IHubContext<OrderHub>
    {
        public IHubClients Clients { get; } = new FakeHubClients();
        public IGroupManager Groups { get; } = new FakeGroupManager();
    }

    private IConfiguration BuildConfig()
    {
        var dict = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}" }
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task CreateOrder_Persists_And_GetOrder_Returns_ItemsAndTotal()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 1,
            WaiterId = 2,
            TableNumber = "A1",
            Items = new[] {
                new OrdersController.OrderItemRequest { Name = "Pizza", Price = 10.5m, Quantity = 1 },
                new OrdersController.OrderItemRequest { Name = "Soda", Price = 2.5m, Quantity = 2 }
            }
        };

        var createResult = await controller.CreateOrder(req);
        Assert.IsType<OkObjectResult>(createResult);
        var createOk = createResult as OkObjectResult;
        Assert.NotNull(createOk?.Value);
        var idProp = createOk.Value.GetType().GetProperty("Id");
        Assert.NotNull(idProp);
        var newId = Convert.ToInt32(idProp.GetValue(createOk.Value));

        // Act: get order
        var getRes = await controller.GetOrder(newId);
        Assert.IsType<OkObjectResult>(getRes);
        var ok = getRes as OkObjectResult;
        Assert.NotNull(ok?.Value);

        // Validate returned shape
        var orderObj = ok.Value.GetType().GetProperty("Order")?.GetValue(ok.Value);
        var itemsObj = ok.Value.GetType().GetProperty("Items")?.GetValue(ok.Value) as System.Collections.IEnumerable;
        Assert.NotNull(orderObj);
        Assert.NotNull(itemsObj);

        var orderType = orderObj!.GetType();
        var status = orderType.GetProperty("Status")?.GetValue(orderObj)?.ToString();
        var total = orderType.GetProperty("Total")?.GetValue(orderObj) as decimal?;
        Assert.Equal("Draft", status);
        Assert.NotNull(total);
        Assert.Equal(10.5m + 2 * 2.5m, total.Value);

        // Ensure items count is 2
        var list = new System.Collections.Generic.List<object>();
        foreach (var it in itemsObj!) list.Add(it);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task ReplaceItems_Allows_Replacing_When_Draft()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        // Create initial order
        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 1,
            TableNumber = "T1",
            Items = new[] { new OrdersController.OrderItemRequest { Name = "A", Price = 1m, Quantity = 1 } }
        };
        var createResultObj = await controller.CreateOrder(req);
        Assert.IsType<OkObjectResult>(createResultObj);
        var createResult = createResultObj as OkObjectResult;
        Assert.NotNull(createResult?.Value);
        var idProp = createResult.Value.GetType().GetProperty("Id");
        Assert.NotNull(idProp);
        var newId = Convert.ToInt32(idProp.GetValue(createResult.Value));

        // Replace items
        var replaceItems = new[] { new OrdersController.OrderItemRequest { Name = "B", Price = 5m, Quantity = 2 } };
        var replaceRes = await controller.ReplaceItems(newId, replaceItems);
        Assert.IsType<OkResult>(replaceRes);

        // Verify items replaced
        var getRes = await controller.GetOrder(newId) as OkObjectResult;
        Assert.NotNull(getRes?.Value);
        var items = getRes.Value.GetType().GetProperty("Items")?.GetValue(getRes.Value) as System.Collections.IEnumerable;
        var list = new System.Collections.Generic.List<dynamic>();
        foreach (var it in items!) list.Add(it);
        Assert.Single(list);
        Assert.Equal("B", (string)list[0].Name);
    }

    [Fact]
    public async Task ConfirmOrder_Sets_Status_To_Sent_And_Total_Is_Calculated()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 1,
            TableNumber = "T2",
            WaiterId = 7,
            Items = new[] { new OrdersController.OrderItemRequest { Name = "X", Price = 3m, Quantity = 3 } }
        };
        var createResultObj = await controller.CreateOrder(req);
        Assert.IsType<OkObjectResult>(createResultObj);
        var createResult = createResultObj as OkObjectResult;
        Assert.NotNull(createResult?.Value);
        var idProp = createResult.Value.GetType().GetProperty("Id");
        Assert.NotNull(idProp);
        var newId = Convert.ToInt32(idProp.GetValue(createResult.Value));

        var confirmRes = await controller.ConfirmOrder(newId);
        Assert.IsType<OkObjectResult>(confirmRes);

        // Immediately after confirm, status should be Sent
        var getRes = await controller.GetOrder(newId) as OkObjectResult;
        Assert.NotNull(getRes?.Value);
        var orderObj = getRes.Value.GetType().GetProperty("Order")?.GetValue(getRes.Value);
        var status = orderObj!.GetType().GetProperty("Status")?.GetValue(orderObj)?.ToString();
        var total = orderObj.GetType().GetProperty("Total")?.GetValue(orderObj) as decimal?;
        Assert.Equal("Sent", status);
        Assert.Equal(9m, total);
    }

    [Fact]
    public async Task ReplaceItems_After_Confirm_Returns_BadRequest()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 1,
            TableNumber = "T3",
            Items = new[] { new OrdersController.OrderItemRequest { Name = "Y", Price = 2m, Quantity = 1 } }
        };
        var createResultObj = await controller.CreateOrder(req);
        Assert.IsType<OkObjectResult>(createResultObj);
        var createResult = createResultObj as OkObjectResult;
        Assert.NotNull(createResult?.Value);
        var idProp = createResult.Value.GetType().GetProperty("Id");
        Assert.NotNull(idProp);
        var newId = Convert.ToInt32(idProp.GetValue(createResult.Value));

        var confirmRes = await controller.ConfirmOrder(newId);
        Assert.IsType<OkObjectResult>(confirmRes);

        // Try to replace items after confirmation
        var replaceRes = await controller.ReplaceItems(newId, new[] { new OrdersController.OrderItemRequest { Name = "Z", Price = 1m, Quantity = 1 } });
        Assert.IsType<BadRequestObjectResult>(replaceRes);
    }

    [Fact]
    public async Task GetByWaiter_Returns_Orders_For_Waiter()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbPath}" }).Build();
        var hub = new FakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        // Create an order with waiter
        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 1,
            WaiterId = 123,
            TableNumber = "T1",
            Items = new[] { new OrdersController.OrderItemRequest { Name = "Pizza", Price = 10m, Quantity = 1 } }
        };

        var createResult = await controller.CreateOrder(req);
        Assert.IsType<OkObjectResult>(createResult);
        var okCreate = createResult as OkObjectResult;
        Assert.NotNull(okCreate?.Value);
        var idProp = okCreate.Value as dynamic;
        Assert.NotNull(idProp);
        var newId = Convert.ToInt32(idProp.Id);

        // Get by waiter
        var getResult = await controller.GetByWaiter(123);
        Assert.IsType<OkObjectResult>(getResult);
        var ok = getResult as OkObjectResult;
        Assert.NotNull(ok?.Value);
        var list = ok.Value as IEnumerable<dynamic>;
        Assert.NotNull(list);
        Assert.Single(list);
        var order = list.First();
        Assert.Equal(newId, (int)order.Id);
        Assert.Equal(123, (int)order.WaiterId);
    }

    [Fact]
    public async Task ConfirmOrder_Returns_NotFound_For_NonExistent_Order()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbPath}" }).Build();
        var hub = new FakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var result = await controller.ConfirmOrder(999);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateOrder_Returns_BadRequest_For_Invalid_RestaurantId()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 0,
            TableNumber = "T1",
            Items = new[] { new OrdersController.OrderItemRequest { Name = "Pizza", Price = 10m, Quantity = 1 } }
        };

        var result = await controller.CreateOrder(req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetOrder_Returns_NotFound_For_Invalid_Id()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var result = await controller.GetOrder(0);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ConfirmOrder_Returns_NotFound_For_Invalid_Id()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var result = await controller.ConfirmOrder(0);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByWaiter_Returns_Empty_For_Invalid_WaiterId()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var result = await controller.GetByWaiter(0);
        Assert.IsType<OkObjectResult>(result);
        var ok = result as OkObjectResult;
        var list = ok!.Value as IEnumerable<dynamic>;
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetByAccessCode_Returns_BadRequest_For_Null_Code()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var result = await controller.GetByAccessCode(null);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetOrder_Returns_NotFound_For_NonExistent_Order()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbPath}" }).Build();
        var hub = new FakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var result = await controller.GetOrder(999);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByAccessCode_Returns_Orders_For_Valid_Code()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbPath}" }).Build();
        var hub = new FakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        // Ensure AccessCodes table exists
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var createCmd = conn.CreateCommand();
        createCmd.CommandText = @"CREATE TABLE IF NOT EXISTS AccessCodes (
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
        await createCmd.ExecuteNonQueryAsync();

        // Create access code
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO AccessCodes (Code, RestaurantId, TableNumber, WaiterId, CreatedAt, IsActive) VALUES ($code, $restaurantId, $tableNumber, $waiterId, $createdAt, 1);";
        var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = "TESTCODE"; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = 1; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$tableNumber"; p.Value = "T1"; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$waiterId"; p.Value = 5; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$createdAt"; p.Value = DateTime.UtcNow.ToString("o"); cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync();

        // Create order for that table
        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 1,
            TableNumber = "T1",
            WaiterId = 5,
            Items = new[] { new OrdersController.OrderItemRequest { Name = "Burger", Price = 15m, Quantity = 1 } }
        };
        var createResult = await controller.CreateOrder(req);
        Assert.IsType<OkObjectResult>(createResult);
        var okCreate = createResult as OkObjectResult;
        var idProp = okCreate!.Value!.GetType().GetProperty("Id");
        var orderId = Convert.ToInt32(idProp!.GetValue(okCreate.Value));

        // Confirm order to make it Sent
        var confirmResult = await controller.ConfirmOrder(orderId);
        Assert.IsType<OkObjectResult>(confirmResult);

        // Get by access code
        var getResult = await controller.GetByAccessCode("TESTCODE");
        Assert.IsType<OkObjectResult>(getResult);
        var ok = getResult as OkObjectResult;
        Assert.NotNull(ok?.Value);
        var ordersProp = ok.Value.GetType().GetProperty("Orders")?.GetValue(ok.Value) as IEnumerable<dynamic>;
        Assert.NotNull(ordersProp);
        Assert.Single(ordersProp);
        var order = ordersProp.First();
        Assert.Equal(orderId, (int)order.Id);
        Assert.Equal(15m, (decimal)order.Total);
    }

    [Fact]
    public async Task GetByAccessCode_Returns_Empty_For_Invalid_Code()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbPath}" }).Build();
        var hub = new FakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var result = await controller.GetByAccessCode("INVALID");
        Assert.IsType<OkObjectResult>(result);
        var ok = result as OkObjectResult;
        Assert.NotNull(ok?.Value);
        var orders = ok.Value.GetType().GetProperty("Orders")?.GetValue(ok.Value) as IEnumerable<dynamic>;
        Assert.NotNull(orders);
        Assert.Empty(orders);
        var subtotal = ok.Value.GetType().GetProperty("Subtotal")?.GetValue(ok.Value) as decimal?;
        Assert.Equal(0m, subtotal);
    }
}
