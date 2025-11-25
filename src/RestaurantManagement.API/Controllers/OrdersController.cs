using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using RestaurantManagement.API.Hubs;
using RestaurantManagement.Infrastructure.Data;
using System.Data.Common;

namespace RestaurantManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<OrderHub> _hubContext;

    public OrdersController(ApplicationDbContext context, IConfiguration configuration, IHubContext<OrderHub> hubContext)
    {
        _context = context;
        _configuration = configuration;
        _hubContext = hubContext;
    }
    // Ensure Orders and OrderItems tables exist
    internal async Task EnsureTableExistsAsync()
    {
        var sqlOrders = @"CREATE TABLE IF NOT EXISTS Orders (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RestaurantId INTEGER NOT NULL,
            WaiterId INTEGER,
            TableNumber TEXT,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT,
            Total NUMERIC
        );";

        var sqlItems = @"CREATE TABLE IF NOT EXISTS OrderItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OrderId INTEGER NOT NULL,
            MenuItemId INTEGER,
            Name TEXT,
            Price NUMERIC,
            Quantity INTEGER NOT NULL,
            Notes TEXT
        );";

        await _context.Database.ExecuteSqlRawAsync(sqlOrders);
        await _context.Database.ExecuteSqlRawAsync(sqlItems);
    }

    // Create a new order
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        if (req.RestaurantId <= 0) return BadRequest("RestaurantId is required");

        await EnsureTableExistsAsync();

        var createdAt = DateTimeOffset.UtcNow.ToString("o");
        var insertOrderSql = "INSERT INTO Orders (RestaurantId, WaiterId, TableNumber, Status, CreatedAt, Total) VALUES ($restaurantId, $waiterId, $tableNumber, $status, $createdAt, $total);";

        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            // Insert order
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = insertOrderSql;
            var p = cmd.CreateParameter(); p.ParameterName = "$restaurantId"; p.Value = req.RestaurantId; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$waiterId"; p.Value = req.WaiterId.HasValue ? (object)req.WaiterId.Value : DBNull.Value; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$tableNumber"; p.Value = (object?)req.TableNumber ?? DBNull.Value; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$status"; p.Value = "Draft"; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$createdAt"; p.Value = createdAt; cmd.Parameters.Add(p);
            p = cmd.CreateParameter(); p.ParameterName = "$total";
            decimal? computedTotal = null;
            if (req.Items != null && req.Items.Any())
                computedTotal = req.Items.Sum(i => i.Price * i.Quantity);
            p.Value = computedTotal.HasValue ? (object)computedTotal.Value : DBNull.Value;
            cmd.Parameters.Add(p);

            await cmd.ExecuteNonQueryAsync();

            using var idCmd = conn.CreateCommand();
            idCmd.Transaction = tx;
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var newIdObj = await idCmd.ExecuteScalarAsync();
            var orderId = Convert.ToInt32(newIdObj);

            if (req.Items != null && req.Items.Any())
            {
                // Insert items
                foreach (var it in req.Items)
                {
                    using var itemCmd = conn.CreateCommand();
                    itemCmd.Transaction = tx;
                    itemCmd.CommandText = "INSERT INTO OrderItems (OrderId, MenuItemId, Name, Price, Quantity, Notes) VALUES ($orderId, $menuItemId, $name, $price, $quantity, $notes);";
                    var ip = itemCmd.CreateParameter(); ip.ParameterName = "$orderId"; ip.Value = orderId; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$menuItemId"; ip.Value = it.MenuItemId.HasValue ? (object)it.MenuItemId.Value : DBNull.Value; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$name"; ip.Value = it.Name ?? (object)DBNull.Value; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$price"; ip.Value = it.Price; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$quantity"; ip.Value = it.Quantity; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$notes"; ip.Value = it.Notes ?? (object)DBNull.Value; itemCmd.Parameters.Add(ip);
                    await itemCmd.ExecuteNonQueryAsync();
                }
            }

            tx.Commit();

            return Ok(new { Id = orderId });
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { }
            return StatusCode(500, ex.Message);
        }
    }

    // Replace items in an existing order
    [HttpPut("{id}/items")]
    public async Task<IActionResult> ReplaceItems(int id, [FromBody] OrderItemRequest[] items)
    {
        await EnsureTableExistsAsync();
        // Check status
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmdCheck = conn.CreateCommand();
        cmdCheck.CommandText = "SELECT Status FROM Orders WHERE Id = $id LIMIT 1;";
        var p = cmdCheck.CreateParameter(); p.ParameterName = "$id"; p.Value = id; cmdCheck.Parameters.Add(p);
        var st = await cmdCheck.ExecuteScalarAsync();
        if (st == null) return NotFound();
        var status = st.ToString();
        if (!string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only Draft orders can be modified");

        using var tx = conn.BeginTransaction();
        try
        {
            // Delete existing items
            using var del = conn.CreateCommand(); del.Transaction = tx; del.CommandText = "DELETE FROM OrderItems WHERE OrderId = $orderId;";
            var dp = del.CreateParameter(); dp.ParameterName = "$orderId"; dp.Value = id; del.Parameters.Add(dp);
            await del.ExecuteNonQueryAsync();

            if (items != null && items.Any())
            {
                // Insert new items
                foreach (var it in items)
                {
                    using var itemCmd = conn.CreateCommand();
                    itemCmd.Transaction = tx;
                    itemCmd.CommandText = "INSERT INTO OrderItems (OrderId, MenuItemId, Name, Price, Quantity, Notes) VALUES ($orderId, $menuItemId, $name, $price, $quantity, $notes);";
                    var ip = itemCmd.CreateParameter(); ip.ParameterName = "$orderId"; ip.Value = id; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$menuItemId"; ip.Value = it.MenuItemId.HasValue ? (object)it.MenuItemId.Value : DBNull.Value; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$name"; ip.Value = it.Name ?? (object)DBNull.Value; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$price"; ip.Value = it.Price; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$quantity"; ip.Value = it.Quantity; itemCmd.Parameters.Add(ip);
                    ip = itemCmd.CreateParameter(); ip.ParameterName = "$notes"; ip.Value = it.Notes ?? (object)DBNull.Value; itemCmd.Parameters.Add(ip);
                    await itemCmd.ExecuteNonQueryAsync();
                }
            }
            // Update UpdatedAt
            using var upd = conn.CreateCommand(); upd.Transaction = tx; upd.CommandText = "UPDATE Orders SET UpdatedAt = $updatedAt WHERE Id = $id;";
            var up = upd.CreateParameter(); up.ParameterName = "$updatedAt"; up.Value = DateTimeOffset.UtcNow.ToString("o"); upd.Parameters.Add(up);
            up = upd.CreateParameter(); up.ParameterName = "$id"; up.Value = id; upd.Parameters.Add(up);
            await upd.ExecuteNonQueryAsync();

            tx.Commit();
            return Ok();
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { }
            return StatusCode(500, ex.Message);
        }
    }

    // Confirm order
    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmOrder(int id)
    {
        // Check status
        await EnsureTableExistsAsync();
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmdCheck = conn.CreateCommand();
        cmdCheck.CommandText = "SELECT Status FROM Orders WHERE Id = $id LIMIT 1;";
        var p = cmdCheck.CreateParameter(); p.ParameterName = "$id"; p.Value = id; cmdCheck.Parameters.Add(p);
        var st = await cmdCheck.ExecuteScalarAsync();
        if (st == null) return NotFound();
        var status = st.ToString();
        if (!string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only Draft orders can be confirmed");

        // Calculate total
        using var sumCmd = conn.CreateCommand();
        sumCmd.CommandText = "SELECT SUM(Price * Quantity) FROM OrderItems WHERE OrderId = $orderId;";
        var sp = sumCmd.CreateParameter(); sp.ParameterName = "$orderId"; sp.Value = id; sumCmd.Parameters.Add(sp);
        var totalObj = await sumCmd.ExecuteScalarAsync();
        decimal total = 0m;
        if (totalObj != null && totalObj != DBNull.Value)
            total = Convert.ToDecimal(totalObj);
        
        // Update order status to Sent
        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE Orders SET Status = $status, UpdatedAt = $updatedAt, Total = $total WHERE Id = $id;";
        var up = upd.CreateParameter(); up.ParameterName = "$status"; up.Value = "Sent"; upd.Parameters.Add(up);
        up = upd.CreateParameter(); up.ParameterName = "$updatedAt"; up.Value = DateTimeOffset.UtcNow.ToString("o"); upd.Parameters.Add(up);
        up = upd.CreateParameter(); up.ParameterName = "$total"; up.Value = total; upd.Parameters.Add(up);
        up = upd.CreateParameter(); up.ParameterName = "$id"; up.Value = id; upd.Parameters.Add(up);
        await upd.ExecuteNonQueryAsync();

        // Start background simulation of kitchen using a new connection
        // Also notify waiter via SignalR when order is ready
        // The kitchen simulation will wait 5 seconds then mark order as Ready
        var waiterIdForNotify = (int?)null;
        using (var connCheck = _context.Database.GetDbConnection())
        {
            await connCheck.OpenAsync();
            using var ccmd = connCheck.CreateCommand();
            ccmd.CommandText = "SELECT WaiterId FROM Orders WHERE Id = $id LIMIT 1;";
            var param = ccmd.CreateParameter(); param.ParameterName = "$id"; param.Value = id; ccmd.Parameters.Add(param);
            var wid = await ccmd.ExecuteScalarAsync();
            if (wid != null && wid != DBNull.Value) waiterIdForNotify = Convert.ToInt32(wid);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Simulate kitchen processing time
                await Task.Delay(TimeSpan.FromSeconds(5));
                var connString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=restaurant.db";
                using var sqliteConn = new SqliteConnection(connString);
                await sqliteConn.OpenAsync();
                using var finishCmd = sqliteConn.CreateCommand();
                finishCmd.CommandText = "UPDATE Orders SET Status = $status, UpdatedAt = $updatedAt WHERE Id = $id;";
                finishCmd.Parameters.Add(new SqliteParameter("$status", "Ready"));
                finishCmd.Parameters.Add(new SqliteParameter("$updatedAt", DateTimeOffset.UtcNow.ToString("o")));
                finishCmd.Parameters.Add(new SqliteParameter("$id", id));
                await finishCmd.ExecuteNonQueryAsync();

                // Notify using SignalR
                if (waiterIdForNotify.HasValue)
                {
                    try
                    {
                        await _hubContext.Clients.Group(waiterIdForNotify.Value.ToString()).SendAsync("OrderReady", id);
                    }
                    catch
                    {
                        // Ignore SignalR errors for now
                        
                    }
                }
            }
            catch
            {
                // Ignore background errors for now
            }
        });

        return Ok(new { Message = "Order confirmed and sent to kitchen" });
    }

    // Get order details
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        await EnsureTableExistsAsync();
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, RestaurantId, WaiterId, TableNumber, Status, CreatedAt, UpdatedAt, Total FROM Orders WHERE Id = $id LIMIT 1;";
        var p = cmd.CreateParameter(); p.ParameterName = "$id"; p.Value = id; cmd.Parameters.Add(p);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows) return NotFound();
        await reader.ReadAsync();
        var order = new
        {
            Id = reader.GetInt32(0),
            RestaurantId = reader.GetInt32(1),
            WaiterId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
            TableNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
            Status = reader.GetString(4),
            CreatedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
            UpdatedAt = reader.IsDBNull(6) ? null : reader.GetString(6),
            Total = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7)
        };

        using var itemsCmd = conn.CreateCommand();
        itemsCmd.CommandText = "SELECT Id, MenuItemId, Name, Price, Quantity, Notes FROM OrderItems WHERE OrderId = $orderId ORDER BY Id;";
        var ip = itemsCmd.CreateParameter(); ip.ParameterName = "$orderId"; ip.Value = id; itemsCmd.Parameters.Add(ip);
        using var r2 = await itemsCmd.ExecuteReaderAsync();
        var items = new List<object>();
        while (await r2.ReadAsync())
        {
            items.Add(new
            {
                Id = r2.GetInt32(0),
                MenuItemId = r2.IsDBNull(1) ? (int?)null : r2.GetInt32(1),
                Name = r2.IsDBNull(2) ? null : r2.GetString(2),
                Price = r2.IsDBNull(3) ? (decimal?)null : r2.GetDecimal(3),
                Quantity = r2.GetInt32(4),
                Notes = r2.IsDBNull(5) ? null : r2.GetString(5)
            });
        }

        return Ok(new { Order = order, Items = items });
    }

    // List orders by waiter
    [HttpGet("bywaiter/{waiterId}")]
    public async Task<IActionResult> GetByWaiter(int waiterId)
    {
        await EnsureTableExistsAsync();
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, RestaurantId, WaiterId, TableNumber, Status, CreatedAt, UpdatedAt, Total FROM Orders WHERE WaiterId = $waiterId ORDER BY Id DESC;";
        var p = cmd.CreateParameter(); p.ParameterName = "$waiterId"; p.Value = waiterId; cmd.Parameters.Add(p);
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<object>();
        while (await reader.ReadAsync())
        {
            list.Add(new
            {
                Id = reader.GetInt32(0),
                RestaurantId = reader.GetInt32(1),
                WaiterId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                TableNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = reader.GetString(4),
                CreatedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
                UpdatedAt = reader.IsDBNull(6) ? null : reader.GetString(6),
                Total = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7)
            });
        }

        return Ok(list);
    }

    public class CreateOrderRequest
    {
        public int RestaurantId { get; set; }
        public int? WaiterId { get; set; }
        public string? TableNumber { get; set; }
        public OrderItemRequest[]? Items { get; set; }
    }

    public class OrderItemRequest
    {
        public int? MenuItemId { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }
}
