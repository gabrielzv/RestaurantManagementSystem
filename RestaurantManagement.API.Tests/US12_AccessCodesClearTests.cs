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

public class US12_AccessCodesClearTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly string _dbPath;

    public US12_AccessCodesClearTests()
    {
        _dbPath = Path.GetTempFileName();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource={_dbPath}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        try { _context.Database.CloseConnection(); } catch { }
        _context.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    // Minimal fake hub context so controllers can be constructed
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
    public async Task ClearCode_Removes_AccessCode_And_Associated_Orders()
    {
        // Arrange
        var accessController = new AccessCodesController(_context);
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var ordersController = new OrdersController(_context, config, hub);

        // Create access code for restaurant 42 and table T99
        var createReq = new AccessCodesController.CreateCodeRequest { RestaurantId = 42, TableNumber = "T99", WaiterId = 1 };
        var createRes = await accessController.CreateCode(createReq) as OkObjectResult;
        Assert.NotNull(createRes?.Value);
        var codeProp = createRes.Value.GetType().GetProperty("Code");
        Assert.NotNull(codeProp);
        var code = codeProp.GetValue(createRes.Value)?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        // Create an order for the same restaurant/table and confirm it so it's visible by access code
        var orderReq = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 42,
            TableNumber = "T99",
            Items = new[] { new OrdersController.OrderItemRequest { Name = "Test", Price = 5m, Quantity = 1 } }
        };
        var createOrderRes = await ordersController.CreateOrder(orderReq) as OkObjectResult;
        Assert.NotNull(createOrderRes?.Value);
        var idProp = createOrderRes.Value.GetType().GetProperty("Id");
        Assert.NotNull(idProp);
        var orderId = Convert.ToInt32(idProp.GetValue(createOrderRes.Value));

        var confirmRes = await ordersController.ConfirmOrder(orderId) as OkObjectResult;
        Assert.NotNull(confirmRes);

        // Verify order is returned by GetByAccessCode
        var before = await ordersController.GetByAccessCode(code) as OkObjectResult;
        Assert.NotNull(before?.Value);
        var ordersProp = before.Value.GetType().GetProperty("Orders");
        Assert.NotNull(ordersProp);
        var ordersEnum = ordersProp.GetValue(before.Value) as System.Collections.IEnumerable;
        var listBefore = new List<object>();
        if (ordersEnum != null) foreach (var o in ordersEnum) listBefore.Add(o);
        Assert.True(listBefore.Count > 0, "Expected orders to exist before clearing code");

        // Act: clear the code
        var clearRes = await accessController.ClearCode(code) as OkObjectResult;
        Assert.NotNull(clearRes);

        // Assert: access code no longer valid (ValidateCode should return NotFound)
        var validateRes = await accessController.ValidateCode(code);
        Assert.IsType<NotFoundResult>(validateRes);

        // Assert: orders by access code now empty
        var after = await ordersController.GetByAccessCode(code) as OkObjectResult;
        Assert.NotNull(after?.Value);
        var ordersPropAfter = after.Value.GetType().GetProperty("Orders");
        Assert.NotNull(ordersPropAfter);
        var ordersEnumAfter = ordersPropAfter.GetValue(after.Value) as System.Collections.IEnumerable;
        var listAfter = new List<object>();
        if (ordersEnumAfter != null) foreach (var o in ordersEnumAfter) listAfter.Add(o);
        Assert.Empty(listAfter);
    }

    [Fact]
    public async Task ClearCode_Returns_NotFound_For_Unknown_Code()
    {
        var accessController = new AccessCodesController(_context);
        var res = await accessController.ClearCode("ZZZZ");
        Assert.IsType<NotFoundObjectResult>(res);
    }
}
