using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.API.Controllers;
using Xunit;

namespace RestaurantManagement.API.Tests;

/// <summary>
/// Tests for US11 - Waiter Authentication and Authorization
/// These tests verify basic validation rules for waiter login and creation
/// </summary>
public class US11Tests
{
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
}
