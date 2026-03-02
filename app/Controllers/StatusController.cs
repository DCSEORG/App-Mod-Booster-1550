using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusController : ControllerBase
{
    private readonly IExpenseService _service;

    public StatusController(IExpenseService service)
    {
        _service = service;
    }

    /// <summary>Get all expense statuses</summary>
    [HttpGet]
    public async Task<IActionResult> GetStatuses()
    {
        var (statuses, error) = await _service.GetStatusesAsync();
        return Ok(new { data = statuses, error });
    }
}
