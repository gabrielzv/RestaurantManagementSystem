using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RestaurantsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RestaurantsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRestaurant(int id)
    {
        var sql = "SELECT Id, Name, Address, CreatedAt FROM Restaurants WHERE Id = $id LIMIT 1;";
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var p = cmd.CreateParameter(); p.ParameterName = "$id"; p.Value = id; cmd.Parameters.Add(p);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows) return NotFound();
        await reader.ReadAsync();
        var result = new
        {
            Id = reader.GetInt32(0),
            Name = reader.IsDBNull(1) ? null : reader.GetString(1),
            Address = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = reader.IsDBNull(3) ? null : reader.GetString(3)
        };
        return Ok(result);
    }
}
