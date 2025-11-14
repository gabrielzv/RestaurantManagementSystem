using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.API.Controllers;
using RestaurantManagement.Infrastructure.Data;
using System.Data.Common;
using System.IO;
using Xunit;

namespace RestaurantManagement.API.Tests;

public class US03Tests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public US03Tests()
    {
        var dbPath = Path.GetTempFileName();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource={dbPath}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task US03Tests_WaitersController_Login_ReturnsToken_WhenValid()
    {
        // Arrange
        var controller = new WaitersController(_context);

        // Ensure tables exist
        await controller.EnsureTableExistsAsync();
        await controller.EnsureAuthTablesAsync();

        var request = new WaitersController.LoginRequest { RestaurantId = 1, Name = "Test", Password = "pass" };

        // Act
        var result = await controller.Login(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult?.Value);
    }

    [Fact]
    public async Task US03Tests_AccessCodesController_ValidateCode_ReturnsOk_WhenValid()
    {
        // Arrange
        var controller = new AccessCodesController(_context);

        // Ensure table exists
        await controller.EnsureTableExistsAsync();

        // First create a code
        var createRequest = new AccessCodesController.CreateCodeRequest { RestaurantId = 1 };
        var createResult = await controller.CreateCode(createRequest);
        Assert.IsType<OkObjectResult>(createResult);
        var createOk = createResult as OkObjectResult;
        Assert.NotNull(createOk?.Value);
        var codeObj = createOk.Value.GetType().GetProperty("Code")?.GetValue(createOk.Value);
        Assert.NotNull(codeObj);
        var code = codeObj.ToString();

        // Act
        var result = await controller.ValidateCode(code!);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task US03Tests_AccessCodesController_GetByWaiter_ReturnsCodes()
    {
        // Arrange
        var controller = new AccessCodesController(_context);

        // Ensure table exists
        await controller.EnsureTableExistsAsync();

        // Create a code with waiterId
        var createRequest = new AccessCodesController.CreateCodeRequest { RestaurantId = 1, WaiterId = 1 };
        await controller.CreateCode(createRequest);

        // Act
        var result = await controller.GetByWaiter(1);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult?.Value);
    }
}
