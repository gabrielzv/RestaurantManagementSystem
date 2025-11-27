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
        string? code = null;
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

    [HttpDelete("clear/{code}")]
    public async Task<IActionResult> ClearCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Code is required");

        await EnsureTableExistsAsync();

        // Remove the access code and also delete the orders and their items

        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();

        // Uses transaction to ensure all-or-nothing
        using var tx = conn.BeginTransaction();
        try
        {
            // Get RestaurantId and TableNumber for this code
            string? tableNumber = null;
            int? restaurantId = null;
            using (var q = conn.CreateCommand())
            {
                q.Transaction = tx;
                q.CommandText = "SELECT RestaurantId, TableNumber FROM AccessCodes WHERE Code = $code LIMIT 1;";
                var qp = q.CreateParameter(); qp.ParameterName = "$code"; qp.Value = code; q.Parameters.Add(qp);
                using var reader = await q.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    restaurantId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                    tableNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
            }

            // If table info, delete order items and orders for that table
            if (restaurantId.HasValue && !string.IsNullOrWhiteSpace(tableNumber))
            {
                // Ensure Orders/OrderItems tables exist
                var createOrders = @"CREATE TABLE IF NOT EXISTS Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RestaurantId INTEGER NOT NULL,
                    WaiterId INTEGER,
                    TableNumber TEXT,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    Total NUMERIC
                );";
                var createItems = @"CREATE TABLE IF NOT EXISTS OrderItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL,
                    MenuItemId INTEGER,
                    Name TEXT,
                    Price NUMERIC,
                    Quantity INTEGER NOT NULL,
                    Notes TEXT
                );";
                using (var createCmd = conn.CreateCommand()) { createCmd.Transaction = tx; createCmd.CommandText = createOrders; await createCmd.ExecuteNonQueryAsync(); }
                using (var createCmd = conn.CreateCommand()) { createCmd.Transaction = tx; createCmd.CommandText = createItems; await createCmd.ExecuteNonQueryAsync(); }

                // Delete items for orders matching this table/restaurant
                var delItemsSql = @"DELETE FROM OrderItems WHERE OrderId IN (
                    SELECT Id FROM Orders WHERE RestaurantId = $restaurantId AND TableNumber = $tableNumber
                );";
                using (var delItems = conn.CreateCommand())
                {
                    delItems.Transaction = tx;
                    delItems.CommandText = delItemsSql;
                    var p1 = delItems.CreateParameter(); p1.ParameterName = "$restaurantId"; p1.Value = restaurantId.Value; delItems.Parameters.Add(p1);
                    var p2 = delItems.CreateParameter(); p2.ParameterName = "$tableNumber"; p2.Value = tableNumber; delItems.Parameters.Add(p2);
                    await delItems.ExecuteNonQueryAsync();
                }

                // Delete orders for that table/restaurant
                var delOrdersSql = "DELETE FROM Orders WHERE RestaurantId = $restaurantId AND TableNumber = $tableNumber;";
                using (var delOrders = conn.CreateCommand())
                {
                    delOrders.Transaction = tx;
                    delOrders.CommandText = delOrdersSql;
                    var p1 = delOrders.CreateParameter(); p1.ParameterName = "$restaurantId"; p1.Value = restaurantId.Value; delOrders.Parameters.Add(p1);
                    var p2 = delOrders.CreateParameter(); p2.ParameterName = "$tableNumber"; p2.Value = tableNumber; delOrders.Parameters.Add(p2);
                    await delOrders.ExecuteNonQueryAsync();
                }
            }

            // Delete the access code itself
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM AccessCodes WHERE Code = $code;";
                var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = code; cmd.Parameters.Add(p);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                {
                    tx.Rollback();
                    return NotFound("Code not found");
                }
            }

            tx.Commit();
            return Ok(new { Message = "Code and associated orders deleted successfully" });
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { }
            return StatusCode(500, ex.Message);
        }
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

    internal async Task EnsureNotificationsTableAsync()
    {
        var sql = @"CREATE TABLE IF NOT EXISTS WaiterNotifications (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            WaiterId INTEGER NOT NULL,
            TableNumber TEXT NOT NULL,
            AccessCode TEXT NOT NULL,
            Message TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            IsRead INTEGER NOT NULL DEFAULT 0
        );";
        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    [HttpPost("notify")]
    public async Task<IActionResult> NotifyWaiter([FromBody] NotifyWaiterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccessCode))
            return BadRequest("AccessCode is required");

        await EnsureTableExistsAsync();
        await EnsureNotificationsTableAsync();

        // Get waiter info from access code
        var query = @"SELECT WaiterId, TableNumber FROM AccessCodes WHERE Code = $code AND IsActive = 1 LIMIT 1;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        
        int? waiterId = null;
        string? tableNumber = null;
        
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = query;
            var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = request.AccessCode; cmd.Parameters.Add(p);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                waiterId = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                tableNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }

        if (!waiterId.HasValue)
            return NotFound("No waiter assigned to this table");

        // Insert notification
        var insertSql = @"INSERT INTO WaiterNotifications (WaiterId, TableNumber, AccessCode, Message, CreatedAt, IsRead)
                          VALUES ($waiterId, $tableNumber, $accessCode, $message, $createdAt, 0);";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = insertSql;
            var p = cmd.CreateParameter(); p.ParameterName = "$waiterId"; p.Value = waiterId.Value; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$tableNumber"; p.Value = tableNumber ?? "N/A"; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$accessCode"; p.Value = request.AccessCode; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$message"; p.Value = request.Message ?? $"El cliente de la mesa {tableNumber ?? "N/A"} le está llamando"; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$createdAt"; p.Value = DateTime.UtcNow.ToString("o"); cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok(new { Message = "Notification sent" });
    }

    [HttpGet("notifications/{accessCode}")]
    public async Task<IActionResult> GetNotifications(string accessCode)
    {
        if (string.IsNullOrWhiteSpace(accessCode))
            return BadRequest("AccessCode is required");

        await EnsureTableExistsAsync();
        await EnsureNotificationsTableAsync();

        // Get WaiterId from access code
        var getWaiterSql = @"SELECT WaiterId FROM AccessCodes WHERE Code = $code LIMIT 1;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        
        int? waiterId = null;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = getWaiterSql;
            var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = accessCode; cmd.Parameters.Add(p);
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value)
                waiterId = Convert.ToInt32(res);
        }

        if (!waiterId.HasValue)
            return Ok(new List<object>()); // No waiter, no notifications

        // Get unread notifications
        var sql = @"SELECT Id, TableNumber, Message, CreatedAt FROM WaiterNotifications 
                    WHERE AccessCode = $code AND IsRead = 0 
                    ORDER BY CreatedAt DESC;";
        var list = new List<object>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            var p = cmd.CreateParameter(); p.ParameterName = "$code"; p.Value = accessCode; cmd.Parameters.Add(p);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new
                {
                    Id = reader.GetInt32(0),
                    TableNumber = reader.GetString(1),
                    Message = reader.GetString(2),
                    CreatedAt = reader.GetString(3)
                });
            }
        }

        return Ok(list);
    }

    [HttpPost("notifications/markread/{notificationId}")]
    public async Task<IActionResult> MarkNotificationAsRead(int notificationId)
    {
        await EnsureNotificationsTableAsync();
        
        var sql = "UPDATE WaiterNotifications SET IsRead = 1 WHERE Id = $id;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var p = cmd.CreateParameter(); p.ParameterName = "$id"; p.Value = notificationId; cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync();

        return Ok(new { Message = "Notification marked as read" });
    }

    public class NotifyWaiterRequest
    {
        public string AccessCode { get; set; } = string.Empty;
        public string? Message { get; set; }
    }
}
