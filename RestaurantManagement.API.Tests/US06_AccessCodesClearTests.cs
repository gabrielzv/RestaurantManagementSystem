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
using Moq;

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

    public class OrderHubTests
    {
        [Fact]
        public async Task JoinGroup_ShouldAddConnectionToGroup_WhenWaiterIdIsValid()
        {
            // Arrange
            var mockGroups = new Mock<IGroupManager>();
            var connectionId = "conn1";
            var waiterId = "123";
            mockGroups.Setup(g => g.AddToGroupAsync(connectionId, waiterId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

            var hub = new OrderHub();
            typeof(Hub).GetProperty("Context")!.SetValue(hub, mockContext.Object);
            typeof(Hub).GetProperty("Groups")!.SetValue(hub, mockGroups.Object);

            // Act
            await hub.JoinGroup(waiterId);

            // Assert
            mockGroups.Verify(g => g.AddToGroupAsync(connectionId, waiterId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LeaveGroup_ShouldRemoveConnectionFromGroup_WhenWaiterIdIsValid()
        {
            // Arrange
            var mockGroups = new Mock<IGroupManager>();
            var connectionId = "conn2";
            var waiterId = "456";
            mockGroups.Setup(g => g.RemoveFromGroupAsync(connectionId, waiterId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

            var hub = new OrderHub();
            typeof(Hub).GetProperty("Context")!.SetValue(hub, mockContext.Object);
            typeof(Hub).GetProperty("Groups")!.SetValue(hub, mockGroups.Object);

            // Act
            await hub.LeaveGroup(waiterId);

            // Assert
            mockGroups.Verify(g => g.RemoveFromGroupAsync(connectionId, waiterId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateCode_Returns_BadRequest_For_Invalid_RestaurantId()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"DataSource=:memory:")
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            var controller = new AccessCodesController(context);

            var request = new AccessCodesController.CreateCodeRequest
            {
                RestaurantId = 0,
                TableNumber = "T1",
                WaiterId = 1
            };

            var result = await controller.CreateCode(request);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ValidateCode_Returns_BadRequest_For_Empty_Code()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"DataSource=:memory:")
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            var controller = new AccessCodesController(context);

            var result = await controller.ValidateCode(string.Empty);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task NotifyWaiter_Returns_BadRequest_For_Empty_AccessCode()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"DataSource=:memory:")
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            var controller = new AccessCodesController(context);

            var request = new AccessCodesController.NotifyWaiterRequest { AccessCode = string.Empty };
            var result = await controller.NotifyWaiter(request);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ValidateCode_Returns_BadRequest_For_Expired_Code()
        {
            var dbPath = Path.GetTempFileName();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            var controller = new AccessCodesController(context);

            // Insert an expired code directly
            var expiredTime = DateTime.UtcNow.AddMinutes(-1).ToString("o");
            using var conn = context.Database.GetDbConnection();
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

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO AccessCodes (Code, RestaurantId, TableNumber, CreatedAt, ExpiresAt, IsActive)
                                VALUES ('EXPIRED', 1, 'T1', $created, $expires, 1);";
            var p = cmd.CreateParameter(); p.ParameterName = "$created"; p.Value = DateTime.UtcNow.ToString("o"); cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$expires"; p.Value = expiredTime; cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync();

            var result = await controller.ValidateCode("EXPIRED");
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
