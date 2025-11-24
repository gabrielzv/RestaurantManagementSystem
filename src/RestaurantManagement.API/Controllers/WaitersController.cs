using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace RestaurantManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WaitersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WaitersController(ApplicationDbContext context)
    {
        _context = context;
    }

    internal async Task EnsureTableExistsAsync()
    {
        // Create table with PasswordHash column if not exists
        var sql = @"CREATE TABLE IF NOT EXISTS Waiters (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RestaurantId INTEGER NOT NULL,
            Username TEXT NOT NULL,
            PasswordHash TEXT NOT NULL,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );";

        await _context.Database.ExecuteSqlRawAsync(sql);

        // Ensure column Username exists (for older DBs)
        var checkSql = "PRAGMA table_info(Waiters);";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = checkSql;
        using var reader = await cmd.ExecuteReaderAsync();
        var hasUsername = false;
        var hasPasswordHash = false;
        while (await reader.ReadAsync())
        {
            var colName = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.Equals(colName, "Username", StringComparison.OrdinalIgnoreCase))
            {
                hasUsername = true;
            }
            if (string.Equals(colName, "PasswordHash", StringComparison.OrdinalIgnoreCase))
            {
                hasPasswordHash = true;
            }
        }

        // If old Name column exists, rename it to Username
        if (!hasUsername && hasPasswordHash)
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Waiters RENAME COLUMN Name TO Username;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        // Ensure PasswordHash is NOT NULL
        if (hasPasswordHash)
        {
            // Update any NULL PasswordHash values with a default hash
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE Waiters SET PasswordHash = 'AQAAAAEAACcQAAAAEBLjouNqAeNrMZtq7hSxgGF2dFMHkq3R8zYQH8Q2dQkJ5QK8qQ==' WHERE PasswordHash IS NULL;";
            await updateCmd.ExecuteNonQueryAsync();
        }
    }

    internal async Task EnsureAuthTablesAsync()
    {
        var sql = @"CREATE TABLE IF NOT EXISTS AccessTokens (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Token TEXT NOT NULL UNIQUE,
            WaiterId INTEGER NOT NULL,
            ExpiresAt TEXT
        );";
        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWaiter([FromBody] CreateWaiterRequest req)
    {
        if (req.RestaurantId <= 0 || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("RestaurantId, Username and Password are required");

        await EnsureTableExistsAsync();

        var insertSql = "INSERT INTO Waiters (RestaurantId, Username, PasswordHash, CreatedAt) VALUES ($restaurantId, $username, $passwordHash, $createdAt);";
        var passwordHash = HashPassword(req.Password);

        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = insertSql;
        var p = cmd.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = req.RestaurantId; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$username"; p.Value = req.Username; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$passwordHash"; p.Value = passwordHash; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$createdAt"; p.Value = DateTime.UtcNow.ToString("o"); cmd.Parameters.Add(p);

        await cmd.ExecuteNonQueryAsync();
        return Ok();
    }

    [HttpGet("byrestaurant/{restaurantId}")]
    public async Task<IActionResult> GetByRestaurant(int restaurantId)
    {
        await EnsureTableExistsAsync();
        var sql = "SELECT Id, Username, CreatedAt FROM Waiters WHERE RestaurantId = $restaurantId ORDER BY Id;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var p = cmd.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = restaurantId; cmd.Parameters.Add(p);

        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<object>();
        while (await reader.ReadAsync())
        {
            list.Add(new
            {
                Id = reader.GetInt32(0),
                Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                CreatedAt = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        return Ok(list);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Username and Password are required");

        await EnsureTableExistsAsync();
        // Try to find existing waiter and validate password
        var findSql = "SELECT Id, Username, RestaurantId, CreatedAt, PasswordHash FROM Waiters WHERE Username = $username LIMIT 1;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        int existingId = -1;
        int restaurantId = -1;
        string? existingPwdHash = null;
        string? existingUsername = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = findSql;
            var p = cmd.CreateParameter(); p.ParameterName = "$username"; p.Value = req.Username; cmd.Parameters.Add(p);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                existingId = reader.GetInt32(0);
                existingUsername = reader.IsDBNull(1) ? null : reader.GetString(1);
                restaurantId = reader.GetInt32(2);
                existingPwdHash = reader.IsDBNull(4) ? null : reader.GetString(4);
            }
        }

        if (existingId != -1)
        {
            // Validate password (required)
            if (string.IsNullOrEmpty(existingPwdHash) || !VerifyPassword(req.Password, existingPwdHash))
                return Unauthorized("Invalid username or password");

            // create auth token
            await EnsureAuthTablesAsync();
            var token = GenerateToken();
            var expiresAt = DateTime.UtcNow.AddHours(12);
            using var insertToken = conn.CreateCommand();
            insertToken.CommandText = "INSERT INTO AccessTokens (Token, WaiterId, ExpiresAt) VALUES ($token, $waiterId, $expiresAt);";
            var tp = insertToken.CreateParameter(); tp.ParameterName = "$token"; tp.Value = token; insertToken.Parameters.Add(tp);
            tp = insertToken.CreateParameter(); tp.ParameterName = "$waiterId"; tp.Value = existingId; insertToken.Parameters.Add(tp);
            tp = insertToken.CreateParameter(); tp.ParameterName = "$expiresAt"; tp.Value = expiresAt.ToString("o"); insertToken.Parameters.Add(tp);
            await insertToken.ExecuteNonQueryAsync();

            var result = new { Id = existingId, Username = existingUsername, RestaurantId = restaurantId, Token = token, ExpiresAt = expiresAt };
            return Ok(result);
        }

        // User not found
        return Unauthorized("Invalid username or password");
    }

    public class CreateWaiterRequest
    {
        public int RestaurantId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // Password hashing helpers and token generation
    internal static string HashPassword(string password)
    {
        // PBKDF2 with SHA256
        const int saltSize = 16;
        const int keySize = 32;
        const int iterations = 100_000;

        var salt = new byte[saltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var subkey = pbkdf2.GetBytes(keySize);

        // store as: iterations.salt.base64.subkey.base64
        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
    }

    internal static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.', 3);
            if (parts.Length != 3) return false;
            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var expectedSubkey = Convert.FromBase64String(parts[2]);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var actualSubkey = pbkdf2.GetBytes(expectedSubkey.Length);

            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }
        catch
        {
            return false;
        }
    }

    internal static string GenerateToken()
    {
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        // base64url
        var b64 = Convert.ToBase64String(tokenBytes).TrimEnd('=');
        return b64.Replace('+', '-').Replace('/', '_');
    }
}
