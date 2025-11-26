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

public class US05_OrdersAdditionalTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly string _dbPath;

    public US05_OrdersAdditionalTests()
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

    private class FakeClientProxy : IClientProxy { public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private class FakeHubClients : IHubClients { public IClientProxy All => new FakeClientProxy(); public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy(); public IClientProxy Client(string connectionId) => new FakeClientProxy(); public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy(); public IClientProxy Group(string groupName) => new FakeClientProxy(); public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy(); public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy(); public IClientProxy User(string userId) => new FakeClientProxy(); public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy(); }
    private class FakeGroupManager : IGroupManager { public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private IHubContext<OrderHub> BuildFakeHubContext() => new FakeHubContext();
    private class FakeHubContext : IHubContext<OrderHub> { public IHubClients Clients { get; } = new FakeHubClients(); public IGroupManager Groups { get; } = new FakeGroupManager(); }

    private IConfiguration BuildConfig()
    {
        var dict = new Dictionary<string, string?> { { "ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}" } };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task CreateOrder_WithoutItems_Sets_Total_Null_And_No_Items()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var req = new OrdersController.CreateOrderRequest { RestaurantId = 1, TableNumber = "NoItems" };
        var createRes = await controller.CreateOrder(req);
        Assert.IsType<OkObjectResult>(createRes);
        var created = createRes as OkObjectResult;
        var idProp = created!.Value.GetType().GetProperty("Id");
        var id = Convert.ToInt32(idProp!.GetValue(created.Value));

        var getRes = await controller.GetOrder(id) as OkObjectResult;
        Assert.NotNull(getRes?.Value);
        var orderObj = getRes.Value.GetType().GetProperty("Order")?.GetValue(getRes.Value);
        var total = orderObj!.GetType().GetProperty("Total")?.GetValue(orderObj) as decimal?;
        Assert.Null(total);

        var items = getRes.Value.GetType().GetProperty("Items")?.GetValue(getRes.Value) as System.Collections.IEnumerable;
        var list = new System.Collections.Generic.List<object>();
        foreach (var it in items!) list.Add(it);
        Assert.Empty(list);
    }

    [Fact]
    public async Task ReplaceItems_WithEmptyArray_Removes_All_Items()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var req = new OrdersController.CreateOrderRequest
        {
            RestaurantId = 1,
            TableNumber = "R1",
            Items = new[] { new OrdersController.OrderItemRequest { Name = "One", Price = 1m, Quantity = 1 }, new OrdersController.OrderItemRequest { Name = "Two", Price = 2m, Quantity = 1 } }
        };
        var createRes = await controller.CreateOrder(req) as OkObjectResult;
        var id = Convert.ToInt32(createRes!.Value.GetType().GetProperty("Id")!.GetValue(createRes.Value));

        // Replace with empty array
        var replace = await controller.ReplaceItems(id, new OrdersController.OrderItemRequest[] { });
        Assert.IsType<OkResult>(replace);

        var getRes = await controller.GetOrder(id) as OkObjectResult;
        var items = getRes!.Value.GetType().GetProperty("Items")?.GetValue(getRes.Value) as System.Collections.IEnumerable;
        var list = new System.Collections.Generic.List<object>();
        foreach (var it in items!) list.Add(it);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetByWaiter_Returns_Only_That_Waiters_Orders()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        // Create two orders for waiter 5 and one for waiter 6
        var r1 = new OrdersController.CreateOrderRequest { RestaurantId = 1, WaiterId = 5, TableNumber = "W5-1" };
        var r2 = new OrdersController.CreateOrderRequest { RestaurantId = 1, WaiterId = 5, TableNumber = "W5-2" };
        var r3 = new OrdersController.CreateOrderRequest { RestaurantId = 1, WaiterId = 6, TableNumber = "W6-1" };
        await controller.CreateOrder(r1);
        await controller.CreateOrder(r2);
        await controller.CreateOrder(r3);

        var res = await controller.GetByWaiter(5) as OkObjectResult;
        Assert.NotNull(res?.Value);
        var list = res.Value as System.Collections.IEnumerable;
        var count = 0;
        foreach (var _ in list!) count++;
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ConfirmOrder_NonExistent_Returns_NotFound()
    {
        var config = BuildConfig();
        var hub = BuildFakeHubContext();
        var controller = new OrdersController(_context, config, hub);

        var res = await controller.ConfirmOrder(99999);
        Assert.IsType<NotFoundResult>(res);
    }
}
