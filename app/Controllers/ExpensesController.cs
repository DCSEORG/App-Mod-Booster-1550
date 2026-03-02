using ExpenseApp.Models;
using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _service;

    public ExpensesController(IExpenseService service)
    {
        _service = service;
    }

    /// <summary>Get all expenses with optional filters</summary>
    [HttpGet]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] int? userId     = null,
        [FromQuery] int? statusId   = null,
        [FromQuery] int? categoryId = null)
    {
        var (expenses, error) = await _service.GetExpensesAsync(userId, statusId, categoryId);
        return Ok(new { data = expenses, error });
    }

    /// <summary>Get a single expense by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetExpense(int id)
    {
        var (expense, error) = await _service.GetExpenseByIdAsync(id);
        if (expense == null && error == null) return NotFound();
        return Ok(new { data = expense, error });
    }

    /// <summary>Create a new expense</summary>
    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        var (newId, error) = await _service.CreateExpenseAsync(
            request.UserId, request.CategoryId, request.AmountMinor,
            request.Currency ?? "GBP", request.ExpenseDate,
            request.Description, request.ReceiptFile);

        if (error != null) return BadRequest(new { error });
        return CreatedAtAction(nameof(GetExpense), new { id = newId }, new { data = newId, error });
    }

    /// <summary>Update an expense</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        var (success, error) = await _service.UpdateExpenseAsync(
            id, request.CategoryId, request.AmountMinor,
            request.Currency ?? "GBP", request.ExpenseDate,
            request.Description, request.ReceiptFile);

        if (!success) return BadRequest(new { error });
        return Ok(new { success, error });
    }

    /// <summary>Delete an expense</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var (success, error) = await _service.DeleteExpenseAsync(id);
        if (!success) return BadRequest(new { error });
        return Ok(new { success, error });
    }

    /// <summary>Submit an expense for approval</summary>
    [HttpPut("{id:int}/submit")]
    public async Task<IActionResult> SubmitExpense(int id)
    {
        var (success, error) = await _service.SubmitExpenseAsync(id);
        if (!success) return BadRequest(new { error });
        return Ok(new { success, error });
    }

    /// <summary>Approve a submitted expense</summary>
    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> ApproveExpense(int id, [FromBody] ReviewRequest request)
    {
        var (success, error) = await _service.ApproveExpenseAsync(id, request.ReviewedBy);
        if (!success) return BadRequest(new { error });
        return Ok(new { success, error });
    }

    /// <summary>Reject a submitted expense</summary>
    [HttpPut("{id:int}/reject")]
    public async Task<IActionResult> RejectExpense(int id, [FromBody] ReviewRequest request)
    {
        var (success, error) = await _service.RejectExpenseAsync(id, request.ReviewedBy);
        if (!success) return BadRequest(new { error });
        return Ok(new { success, error });
    }
}

public record CreateExpenseRequest(
    int UserId, int CategoryId, int AmountMinor,
    string? Currency, DateTime ExpenseDate,
    string? Description, string? ReceiptFile);

public record UpdateExpenseRequest(
    int CategoryId, int AmountMinor,
    string? Currency, DateTime ExpenseDate,
    string? Description, string? ReceiptFile);

public record ReviewRequest(int ReviewedBy);
