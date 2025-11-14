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

    private async Task EnsureTableExistsAsync()
    {
        var sql = @"CREATE TABLE IF NOT EXISTS AccessCodes (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Code TEXT NOT NULL,
            RestaurantId INTEGER NOT NULL,
            TableNumber TEXT,
            WaiterId INTEGER,
            CreatedAt TEXT NOT NULL,
            ExpiresAt TEXT,
            IsActive INTEGER NOT NULL DEFAULT 1
        );";

        await _context.Database.ExecuteSqlRawAsync(sql);
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
        var query = @"SELECT Id, Code, RestaurantId, TableNumber, WaiterId, CreatedAt, ExpiresAt, IsActive
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
                IsActive = reader.GetInt32(7) == 1
            };

            return Ok(result);
        }
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

    public class CreateCodeRequest
    {
        public int RestaurantId { get; set; }
        public string? TableNumber { get; set; }
        public int? WaiterId { get; set; }
        public int? TtlMinutes { get; set; }
    }
}
