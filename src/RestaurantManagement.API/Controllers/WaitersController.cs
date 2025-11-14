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
            Name TEXT NOT NULL,
            PasswordHash TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );";

        await _context.Database.ExecuteSqlRawAsync(sql);

        // Ensure column PasswordHash exists (for older DBs)
        var checkSql = "PRAGMA table_info(Waiters);";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = checkSql;
        using var reader = await cmd.ExecuteReaderAsync();
        var hasPasswordHash = false;
        while (await reader.ReadAsync())
        {
            var colName = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.Equals(colName, "PasswordHash", StringComparison.OrdinalIgnoreCase))
            {
                hasPasswordHash = true;
                break;
            }
        }

        if (!hasPasswordHash)
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Waiters ADD COLUMN PasswordHash TEXT;";
            await alterCmd.ExecuteNonQueryAsync();
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
        if (req.RestaurantId <= 0 || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("RestaurantId and Name are required");

        await EnsureTableExistsAsync();

        var insertSql = "INSERT INTO Waiters (RestaurantId, Name, PasswordHash, CreatedAt) VALUES ($restaurantId, $name, $passwordHash, $createdAt);";
        var passwordHash = (string?)null;
        if (!string.IsNullOrEmpty(req.Password))
            passwordHash = HashPassword(req.Password);

        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = insertSql;
        var p = cmd.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = req.RestaurantId; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$name"; p.Value = req.Name; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$passwordHash"; p.Value = (object?)passwordHash ?? DBNull.Value; cmd.Parameters.Add(p);
        p = cmd.CreateParameter(); p.ParameterName = "$createdAt"; p.Value = DateTime.UtcNow.ToString("o"); cmd.Parameters.Add(p);

        await cmd.ExecuteNonQueryAsync();
        return Ok();
    }

    [HttpGet("byrestaurant/{restaurantId}")]
    public async Task<IActionResult> GetByRestaurant(int restaurantId)
    {
        await EnsureTableExistsAsync();
        var sql = "SELECT Id, Name, CreatedAt FROM Waiters WHERE RestaurantId = $restaurantId ORDER BY Id;";
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
                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                CreatedAt = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        return Ok(list);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (req.RestaurantId <= 0 || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("RestaurantId and Name are required");

        await EnsureTableExistsAsync();
        // Try to find existing waiter and validate password if present
        var findSql = "SELECT Id, Name, CreatedAt, PasswordHash FROM Waiters WHERE RestaurantId = $restaurantId AND Name = $name LIMIT 1;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        int existingId = -1;
        string? existingPwdHash = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = findSql;
            var p = cmd.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = req.RestaurantId; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$name"; p.Value = req.Name; cmd.Parameters.Add(p);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                existingId = reader.GetInt32(0);
                existingPwdHash = reader.IsDBNull(3) ? null : reader.GetString(3);
            }
        }

        if (existingId != -1)
        {
            // If password is set on account, require it
            if (!string.IsNullOrEmpty(existingPwdHash))
            {
                if (string.IsNullOrEmpty(req.Password) || !VerifyPassword(req.Password, existingPwdHash))
                    return Unauthorized("Invalid password");
            }

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

            var result = new { Id = existingId, Name = req.Name, RestaurantId = req.RestaurantId, Token = token, ExpiresAt = expiresAt };
            return Ok(result);
        }

        // Not found -> create and return (store password if provided)
        var insertSql = "INSERT INTO Waiters (RestaurantId, Name, PasswordHash, CreatedAt) VALUES ($restaurantId, $name, $passwordHash, $createdAt);";
        var pwdHash = string.IsNullOrEmpty(req.Password) ? null : HashPassword(req.Password);
        using (var cmd2 = conn.CreateCommand())
        {
            cmd2.CommandText = insertSql;
            var p = cmd2.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = req.RestaurantId; cmd2.Parameters.Add(p);
            p = cmd2.CreateParameter(); p.ParameterName = "$name"; p.Value = req.Name; cmd2.Parameters.Add(p);
            p = cmd2.CreateParameter(); p.ParameterName = "$passwordHash"; p.Value = (object?)pwdHash ?? DBNull.Value; cmd2.Parameters.Add(p);
            p = cmd2.CreateParameter(); p.ParameterName = "$createdAt"; p.Value = DateTime.UtcNow.ToString("o"); cmd2.Parameters.Add(p);

            await cmd2.ExecuteNonQueryAsync();
        }

        // get last inserted id
        using var cmd3 = conn.CreateCommand();
        cmd3.CommandText = "SELECT last_insert_rowid();";
        var idObj = await cmd3.ExecuteScalarAsync();
        var newId = Convert.ToInt32(idObj);

        // create token
        await EnsureAuthTablesAsync();
        var newToken = GenerateToken();
        var newExpires = DateTime.UtcNow.AddHours(12);
        using var insertToken2 = conn.CreateCommand();
        insertToken2.CommandText = "INSERT INTO AccessTokens (Token, WaiterId, ExpiresAt) VALUES ($token, $waiterId, $expiresAt);";
        var t1 = insertToken2.CreateParameter(); t1.ParameterName = "$token"; t1.Value = newToken; insertToken2.Parameters.Add(t1);
        t1 = insertToken2.CreateParameter(); t1.ParameterName = "$waiterId"; t1.Value = newId; insertToken2.Parameters.Add(t1);
        t1 = insertToken2.CreateParameter(); t1.ParameterName = "$expiresAt"; t1.Value = newExpires.ToString("o"); insertToken2.Parameters.Add(t1);
        await insertToken2.ExecuteNonQueryAsync();

        var result2 = new { Id = newId, Name = req.Name, RestaurantId = req.RestaurantId, Token = newToken, ExpiresAt = newExpires };
        return Ok(result2);
    }

    public class CreateWaiterRequest
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Password { get; set; }
    }

    public class LoginRequest
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Password { get; set; }
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
