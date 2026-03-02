using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _service;

    public CategoriesController(IExpenseService service)
    {
        _service = service;
    }

    /// <summary>Get all expense categories</summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var (categories, error) = await _service.GetCategoriesAsync();
        return Ok(new { data = categories, error });
    }
}
