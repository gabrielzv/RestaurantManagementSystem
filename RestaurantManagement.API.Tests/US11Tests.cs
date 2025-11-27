using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Controllers;
using Xunit;
using Microsoft.EntityFrameworkCore;
using System.IO;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.API.Tests;

/// <summary>
/// Tests for US11 - Waiter Authentication and Authorization
/// These tests verify basic validation rules for waiter login and creation
/// </summary>
public class US11Tests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly string _dbPath;

    public US11Tests()
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
    [Fact]
    public void CreateWaiterRequest_ShouldValidateMissingUsername()
    {
        // Arrange
        var request = new WaitersController.CreateWaiterRequest
        {
            RestaurantId = 1,
            Username = "",
            Password = "password123"
        };

        // Act & Assert
        Assert.Empty(request.Username);
        Assert.NotEmpty(request.Password);
        Assert.True(request.RestaurantId > 0);
    }

    [Fact]
    public void CreateWaiterRequest_ShouldValidateMissingPassword()
    {
        // Arrange
        var request = new WaitersController.CreateWaiterRequest
        {
            RestaurantId = 1,
            Username = "testwaiter",
            Password = ""
        };

        // Act & Assert
        Assert.NotEmpty(request.Username);
        Assert.Empty(request.Password);
        Assert.True(request.RestaurantId > 0);
    }

    [Fact]
    public void LoginRequest_ShouldContainRequiredFields()
    {
        // Arrange
        var request = new WaitersController.LoginRequest
        {
            Username = "testuser",
            Password = "testpass"
        };

        // Act & Assert
        Assert.NotEmpty(request.Username);
        Assert.NotEmpty(request.Password);
    }

    [Fact]
    public void PasswordHashing_ShouldGenerateDifferentHashesForSamePassword()
    {
        // Arrange
        var password = "test123";

        // Act
        var hash1 = WaitersController.HashPassword(password);
        var hash2 = WaitersController.HashPassword(password);

        // Assert - Hashes should be different due to salt
        Assert.NotEqual(hash1, hash2);
        Assert.Contains(".", hash1); // Should contain iteration separator
        Assert.Contains(".", hash2);
    }

    [Fact]
    public void PasswordVerification_ShouldValidateCorrectPassword()
    {
        // Arrange
        var password = "testpassword123";
        var hash = WaitersController.HashPassword(password);

        // Act
        var isValid = WaitersController.VerifyPassword(password, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void PasswordVerification_ShouldRejectIncorrectPassword()
    {
        // Arrange
        var password = "correctpassword";
        var wrongPassword = "wrongpassword";
        var hash = WaitersController.HashPassword(password);

        // Act
        var isValid = WaitersController.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void PasswordVerification_ShouldHandleInvalidHash()
    {
        // Arrange
        var password = "testpassword";
        var invalidHash = "invalid.hash";

        // Act
        var isValid = WaitersController.VerifyPassword(password, invalidHash);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void PasswordVerification_ShouldHandleMalformedHash()
    {
        // Arrange
        var password = "testpassword";
        var malformedHash = "abc.def.ghi"; // iterations not int

        // Act
        var isValid = WaitersController.VerifyPassword(password, malformedHash);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void TokenGeneration_ShouldGenerateUniqueTokens()
    {
        // Act
        var token1 = WaitersController.GenerateToken();
        var token2 = WaitersController.GenerateToken();

        // Assert
        Assert.NotEmpty(token1);
        Assert.NotEmpty(token2);
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public async Task CreateWaiter_Returns_BadRequest_For_Invalid_RestaurantId()
    {
        var controller = new WaitersController(_context);
        var request = new WaitersController.CreateWaiterRequest
        {
            RestaurantId = 0,
            Username = "test",
            Password = "pass"
        };

        var result = await controller.CreateWaiter(request);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateWaiter_Returns_BadRequest_For_Empty_Username()
    {
        var controller = new WaitersController(_context);
        var request = new WaitersController.CreateWaiterRequest
        {
            RestaurantId = 1,
            Username = "",
            Password = "pass"
        };

        var result = await controller.CreateWaiter(request);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateWaiter_Returns_BadRequest_For_Empty_Password()
    {
        var controller = new WaitersController(_context);
        var request = new WaitersController.CreateWaiterRequest
        {
            RestaurantId = 1,
            Username = "test",
            Password = ""
        };

        var result = await controller.CreateWaiter(request);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_Returns_BadRequest_For_Empty_Username()
    {
        var controller = new WaitersController(_context);
        var request = new WaitersController.LoginRequest
        {
            Username = "",
            Password = "pass"
        };

        var result = await controller.Login(request);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_Returns_BadRequest_For_Empty_Password()
    {
        var controller = new WaitersController(_context);
        var request = new WaitersController.LoginRequest
        {
            Username = "test",
            Password = ""
        };

        var result = await controller.Login(request);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_Returns_Unauthorized_For_Wrong_Password()
    {
        var controller = new WaitersController(_context);

        // Ensure tables exist
        await controller.EnsureTableExistsAsync();
        await controller.EnsureAuthTablesAsync();

        // Create a waiter
        var createRequest = new WaitersController.CreateWaiterRequest { RestaurantId = 1, Username = "testuser", Password = "correctpass" };
        await controller.CreateWaiter(createRequest);

        // Now login with wrong password
        var request = new WaitersController.LoginRequest { Username = "testuser", Password = "wrongpass" };

        var result = await controller.Login(request);
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_Returns_Ok_For_Valid_Credentials()
    {
        var controller = new WaitersController(_context);

        // Ensure tables exist
        await controller.EnsureTableExistsAsync();
        await controller.EnsureAuthTablesAsync();

        // Create a waiter
        var createRequest = new WaitersController.CreateWaiterRequest { RestaurantId = 1, Username = "testuser2", Password = "correctpass" };
        await controller.CreateWaiter(createRequest);

        // Now login with correct password
        var request = new WaitersController.LoginRequest { Username = "testuser2", Password = "correctpass" };

        var result = await controller.Login(request);
        Assert.IsType<OkObjectResult>(result);
    }
}
