using System;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.API.Controllers;
using RestaurantManagement.Infrastructure.Data;
using Xunit;

namespace RestaurantManagement.API.Tests;

/// <summary>
/// Tests for US07 - Waiter notifications when clients request assistance.
/// </summary>
public class US07Tests
{
    [Fact]
    public async Task NotifyWaiter_ShouldStoreNotificationWithDefaultMessage()
    {
        await using var testContext = await TestContext.CreateAsync();
        var controller = testContext.Controller;

        var accessCode = await testContext.CreateAccessCodeAsync("Mesa-15", waiterId: 42);

        var notifyResult = await controller.NotifyWaiter(new AccessCodesController.NotifyWaiterRequest
        {
            AccessCode = accessCode
        });

        Assert.IsType<OkObjectResult>(notifyResult);

        var notifications = await testContext.GetNotificationsAsync(accessCode);
        var notification = Assert.Single(notifications);
        Assert.Equal("El cliente de la mesa Mesa-15 le está llamando", notification.Message);
        Assert.Equal("Mesa-15", notification.TableNumber);
    }

    [Fact]
    public async Task NotifyWaiter_ShouldAccumulateMultipleUnreadNotifications()
    {
        await using var testContext = await TestContext.CreateAsync();
        var controller = testContext.Controller;

        var accessCode = await testContext.CreateAccessCodeAsync("Mesa-5", waiterId: 7);

        await controller.NotifyWaiter(new AccessCodesController.NotifyWaiterRequest
        {
            AccessCode = accessCode,
            Message = "El cliente pidió agua"
        });

        await controller.NotifyWaiter(new AccessCodesController.NotifyWaiterRequest
        {
            AccessCode = accessCode,
            Message = "El cliente necesita la cuenta"
        });

        var notifications = await testContext.GetNotificationsAsync(accessCode);
        Assert.Equal(2, notifications.Count);

        // Most recent notification should appear first (ordered by CreatedAt DESC)
        Assert.Equal("El cliente necesita la cuenta", notifications[0].Message);
        Assert.Equal("El cliente pidió agua", notifications[1].Message);
    }

    [Fact]
    public async Task MarkNotificationAsRead_ShouldRemoveItFromUnreadList()
    {
        await using var testContext = await TestContext.CreateAsync();
        var controller = testContext.Controller;

        var accessCode = await testContext.CreateAccessCodeAsync("Mesa-21", waiterId: 11);

        await controller.NotifyWaiter(new AccessCodesController.NotifyWaiterRequest
        {
            AccessCode = accessCode,
            Message = "Cliente solicita asistencia"
        });

        var notifications = await testContext.GetNotificationsAsync(accessCode);
        var notification = Assert.Single(notifications);

        var markResult = await controller.MarkNotificationAsRead(notification.Id);
        Assert.IsType<OkObjectResult>(markResult);

        var afterMark = await testContext.GetNotificationsAsync(accessCode);
        Assert.Empty(afterMark);
    }

    private sealed class TestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _dbContext;
        private readonly string _databasePath;
        private static readonly JsonSerializerOptions WebSerializerOptions = new(JsonSerializerDefaults.Web);

        private TestContext(string databasePath, SqliteConnection connection, ApplicationDbContext dbContext)
        {
            _databasePath = databasePath;
            _connection = connection;
            _dbContext = dbContext;
            Controller = new AccessCodesController(dbContext);
        }

        public AccessCodesController Controller { get; }

        public static async Task<TestContext> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"us07_{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={databasePath};";
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            await dbContext.Database.OpenConnectionAsync();

            return new TestContext(databasePath, connection, dbContext);
        }

        public async Task<string> CreateAccessCodeAsync(string tableNumber, int waiterId)
        {
            await EnsureAccessCodesTableExistsAsync();
            var code = Guid.NewGuid().ToString("N").Substring(0, 4);
            var now = DateTime.UtcNow.ToString("o");

            await _dbContext.Database.ExecuteSqlRawAsync(
                @"INSERT INTO AccessCodes (Code, RestaurantId, TableNumber, WaiterId, CreatedAt, ExpiresAt, UsedAt, IsActive)
                  VALUES ($code, $restaurantId, $tableNumber, $waiterId, $createdAt, $expiresAt, $usedAt, 1);",
                new SqliteParameter("$code", code),
                new SqliteParameter("$restaurantId", 1),
                new SqliteParameter("$tableNumber", tableNumber),
                new SqliteParameter("$waiterId", waiterId),
                new SqliteParameter("$createdAt", now),
                new SqliteParameter("$expiresAt", DBNull.Value),
                new SqliteParameter("$usedAt", DBNull.Value));

            return code;
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

        public async Task<List<NotificationDto>> GetNotificationsAsync(string accessCode)
        {
            var result = await Controller.GetNotifications(accessCode);
            var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value, WebSerializerOptions);
            using var document = JsonDocument.Parse(json);

            var list = new List<NotificationDto>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                list.Add(new NotificationDto(
                    element.GetProperty("id").GetInt32(),
                    element.GetProperty("message").GetString() ?? string.Empty,
                    element.GetProperty("tableNumber").GetString() ?? string.Empty,
                    element.GetProperty("createdAt").GetString() ?? string.Empty));
            }

            return list;
        }

        public async ValueTask DisposeAsync()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
            TryDeleteDatabaseFile();
        }

        internal sealed record NotificationDto(int Id, string Message, string TableNumber, string CreatedAt);

        private void TryDeleteDatabaseFile()
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch
            {
                // ignore cleanup errors in tests
            }
        }
    }
}
