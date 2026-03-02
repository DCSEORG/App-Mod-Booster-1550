using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _service;

    public UsersController(IExpenseService service)
    {
        _service = service;
    }

    /// <summary>Get all users</summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var (users, error) = await _service.GetUsersAsync();
        return Ok(new { data = users, error });
    }

    /// <summary>Get a single user by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var (user, error) = await _service.GetUserByIdAsync(id);
        if (user == null && error == null) return NotFound();
        return Ok(new { data = user, error });
    }

    /// <summary>Create a new user</summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var (newId, error) = await _service.CreateUserAsync(
            request.UserName, request.Email, request.RoleId, request.ManagerId);
        if (error != null) return BadRequest(new { error });
        return CreatedAtAction(nameof(GetUser), new { id = newId }, new { data = newId, error });
    }
}

public record CreateUserRequest(string UserName, string Email, int RoleId, int? ManagerId);
