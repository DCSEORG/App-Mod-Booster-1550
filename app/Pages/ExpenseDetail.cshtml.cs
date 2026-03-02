using ExpenseApp.Models;
using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseApp.Pages;

public class ExpenseDetailModel : PageModel
{
    private readonly IExpenseService _service;

    public ExpenseDetailModel(IExpenseService service)
    {
        _service = service;
    }

    public Expense?              Expense    { get; set; }
    public List<ExpenseCategory> Categories { get; set; } = new();

    public async Task OnGetAsync(int id)
    {
        await LoadDataAsync(id);
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        int expenseId, int categoryId, decimal amountGBP, DateTime expenseDate,
        string? description, string? receiptFile)
    {
        var amountMinor = (int)(amountGBP * 100);
        var (success, error) = await _service.UpdateExpenseAsync(
            expenseId, categoryId, amountMinor, "GBP", expenseDate, description, receiptFile);

        if (error != null) TempData["Error"] = error;
        return RedirectToPage(new { id = expenseId });
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        var (_, error) = await _service.SubmitExpenseAsync(expenseId);
        if (error != null) TempData["Error"] = error;
        return RedirectToPage(new { id = expenseId });
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId, int reviewedBy)
    {
        var (_, error) = await _service.ApproveExpenseAsync(expenseId, reviewedBy);
        if (error != null) TempData["Error"] = error;
        return RedirectToPage(new { id = expenseId });
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId, int reviewedBy)
    {
        var (_, error) = await _service.RejectExpenseAsync(expenseId, reviewedBy);
        if (error != null) TempData["Error"] = error;
        return RedirectToPage(new { id = expenseId });
    }

    private async Task LoadDataAsync(int expenseId)
    {
        var (expense, expError) = await _service.GetExpenseByIdAsync(expenseId);
        Expense = expense;

        var (categories, _) = await _service.GetCategoriesAsync();
        Categories = categories;

        var errorMsg = expError ?? TempData["Error"]?.ToString();
        if (errorMsg != null) ViewData["ErrorMessage"] = errorMsg;
    }
}
