using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Infrastructure.Data;
using System.Data.Common;

namespace RestaurantManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccessCodesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AccessCodesController(ApplicationDbContext context)
    {
        _context = context;
    }

    internal async Task EnsureTableExistsAsync()
    {
        var sql = @"CREATE TABLE IF NOT EXISTS AccessCodes (
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

        await _context.Database.ExecuteSqlRawAsync(sql);

        // Ensure column UsedAt exists (for older DBs)
        var checkUsedAtSql = "PRAGMA table_info(AccessCodes);";
        using var connUsedAt = _context.Database.GetDbConnection();
        await connUsedAt.OpenAsync();
        using var cmdUsedAt = connUsedAt.CreateCommand();
        cmdUsedAt.CommandText = checkUsedAtSql;
        using var readerUsedAt = await cmdUsedAt.ExecuteReaderAsync();
        var hasUsedAt = false;
        while (await readerUsedAt.ReadAsync())
        {
            var colName = readerUsedAt.IsDBNull(1) ? null : readerUsedAt.GetString(1);
            if (string.Equals(colName, "UsedAt", StringComparison.OrdinalIgnoreCase))
            {
                hasUsedAt = true;
                break;
            }
        }

        if (!hasUsedAt)
        {
            using var alterUsedAt = connUsedAt.CreateCommand();
            alterUsedAt.CommandText = "ALTER TABLE AccessCodes ADD COLUMN UsedAt TEXT;";
            await alterUsedAt.ExecuteNonQueryAsync();
        }
    }

    private static string Generate4DigitCode()
    {
        var rnd = new Random();
        return rnd.Next(0, 10000).ToString("D4");
    }

    [HttpPost]
    public async Task<IActionResult> CreateCode([FromBody] CreateCodeRequest request)
    {
        if (request.RestaurantId <= 0)
            return BadRequest("RestaurantId is required");

        await EnsureTableExistsAsync();

        // Try to generate a unique 4-digit code (active)
        string code = null;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Generate4DigitCode();
            var exists = await CodeExistsAsync(candidate);
            if (!exists)
            {
                code = candidate;
                break;
            }
        }

        if (code == null)
            return StatusCode(500, "Unable to generate unique code, try again");

        var createdAt = DateTimeOffset.UtcNow;
        DateTimeOffset? expiresAt = null;
        if (request.TtlMinutes.HasValue)
            expiresAt = createdAt.AddMinutes(request.TtlMinutes.Value);

        var insertSql = @"INSERT INTO AccessCodes (Code, RestaurantId, TableNumber, WaiterId, CreatedAt, ExpiresAt, IsActive)
                          VALUES ($code, $restaurantId, $tableNumber, $waiterId, $createdAt, $expiresAt, 1);";

        using (var conn = _context.Database.GetDbConnection())
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = insertSql;
            var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = code; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = request.RestaurantId; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$tableNumber"; p.Value = (object?)request.TableNumber ?? DBNull.Value; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$waiterId"; p.Value = request.WaiterId.HasValue ? (object)request.WaiterId.Value : DBNull.Value; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$createdAt"; p.Value = createdAt.ToString("o"); cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$expiresAt"; p.Value = expiresAt.HasValue ? (object)expiresAt.Value.ToString("o") : DBNull.Value; cmd.Parameters.Add(p);

            await cmd.ExecuteNonQueryAsync();
        }

        return Ok(new { Code = code, ExpiresAt = expiresAt });
    }

    [HttpGet("validate/{code}")]
    public async Task<IActionResult> ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Code is required");

        await EnsureTableExistsAsync();

        var now = DateTime.UtcNow;
        var query = @"SELECT Id, Code, RestaurantId, TableNumber, WaiterId, CreatedAt, ExpiresAt, UsedAt, IsActive
                      FROM AccessCodes
                      WHERE Code = $code AND IsActive = 1
                      LIMIT 1;";

        using (var conn = _context.Database.GetDbConnection())
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = code; cmd.Parameters.Add(p);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows)
                return NotFound();

            await reader.ReadAsync();
            var expiresAtObj = reader[6];
            if (expiresAtObj != DBNull.Value)
            {
                if (DateTimeOffset.TryParse(expiresAtObj.ToString(), out var expiresAt))
                {
                    if (expiresAt <= DateTimeOffset.UtcNow)
                        return BadRequest("Code expired");
                }
            }

            var result = new
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                RestaurantId = reader.GetInt32(2),
                TableNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                WaiterId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                CreatedAt = reader.GetString(5),
                ExpiresAt = reader.IsDBNull(6) ? (string?)null : reader.GetString(6),
                UsedAt = reader.IsDBNull(7) ? (string?)null : reader.GetString(7),
                IsActive = reader.GetInt32(8) == 1
            };

            // Mark as used
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE AccessCodes SET UsedAt = $usedAt WHERE Id = $id;";
            var up = updateCmd.CreateParameter(); up.ParameterName = "$usedAt"; up.Value = DateTimeOffset.UtcNow.ToString("o"); updateCmd.Parameters.Add(up);
            up = updateCmd.CreateParameter(); up.ParameterName = "$id"; up.Value = result.Id; updateCmd.Parameters.Add(up);
            await updateCmd.ExecuteNonQueryAsync();

            return Ok(result);
        }
    }

    [HttpGet("bywaiter/{waiterId}")]
    public async Task<IActionResult> GetByWaiter(int waiterId)
    {
        await EnsureTableExistsAsync();
        var sql = "SELECT Id, Code, RestaurantId, TableNumber, CreatedAt, ExpiresAt, UsedAt, IsActive FROM AccessCodes WHERE WaiterId = $waiterId ORDER BY Id DESC;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var p = cmd.CreateParameter(); p.ParameterName = "$waiterId"; p.Value = waiterId; cmd.Parameters.Add(p);

        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<object>();
        while (await reader.ReadAsync())
        {
            list.Add(new
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                RestaurantId = reader.GetInt32(2),
                TableNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetString(4),
                ExpiresAt = reader.IsDBNull(5) ? (string?)null : reader.GetString(5),
                UsedAt = reader.IsDBNull(6) ? (string?)null : reader.GetString(6),
                IsActive = reader.GetInt32(7) == 1
            });
        }

        return Ok(list);
    }

    public class CreateCodeRequest
    {
        public int RestaurantId { get; set; }
        public string? TableNumber { get; set; }
        public int? WaiterId { get; set; }
        public int? TtlMinutes { get; set; }
    }

    private async Task<bool> CodeExistsAsync(string code)
    {
        var sql = "SELECT COUNT(1) FROM AccessCodes WHERE Code = $code AND IsActive = 1;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = code; cmd.Parameters.Add(p);
        var res = await cmd.ExecuteScalarAsync();
        if (res == null || res == DBNull.Value) return false;
        return Convert.ToInt32(res) > 0;
    }
}
