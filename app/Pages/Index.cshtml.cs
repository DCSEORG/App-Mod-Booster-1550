using ExpenseApp.Models;
using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseApp.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _service;

    public IndexModel(IExpenseService service)
    {
        _service = service;
    }

    public List<Expense>         Expenses   { get; set; } = new();
    public List<User>            Users      { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<ExpenseStatus>   Statuses   { get; set; } = new();

    [BindProperty(SupportsGet = true)] public int? StatusFilter   { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryFilter { get; set; }
    [BindProperty(SupportsGet = true)] public int? UserFilter     { get; set; }

    public async Task OnGetAsync()
    {
        await LoadReferenceDataAsync();

        var (expenses, error) = await _service.GetExpensesAsync(UserFilter, StatusFilter, CategoryFilter);
        Expenses = expenses;
        if (error != null) ViewData["ErrorMessage"] = error;
    }

    public async Task<IActionResult> OnPostCreateAsync(
        int userId, int categoryId, decimal amountGBP, DateTime expenseDate, string? description)
    {
        var amountMinor = (int)(amountGBP * 100);
        var (_, error) = await _service.CreateExpenseAsync(
            userId, categoryId, amountMinor, "GBP", expenseDate, description, null);

        if (error != null) TempData["Error"] = error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        var (_, error) = await _service.SubmitExpenseAsync(expenseId);
        if (error != null) TempData["Error"] = error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId, int reviewedBy)
    {
        var (_, error) = await _service.ApproveExpenseAsync(expenseId, reviewedBy);
        if (error != null) TempData["Error"] = error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId, int reviewedBy)
    {
        var (_, error) = await _service.RejectExpenseAsync(expenseId, reviewedBy);
        if (error != null) TempData["Error"] = error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int expenseId)
    {
        var (_, error) = await _service.DeleteExpenseAsync(expenseId);
        if (error != null) TempData["Error"] = error;
        return RedirectToPage();
    }

    private async Task LoadReferenceDataAsync()
    {
        var (categories, _) = await _service.GetCategoriesAsync();
        var (statuses, _)   = await _service.GetStatusesAsync();
        var (users, _)      = await _service.GetUsersAsync();
        Categories = categories;
        Statuses   = statuses;
        Users      = users;

        if (TempData.ContainsKey("Error"))
            ViewData["ErrorMessage"] = TempData["Error"];
    }
}
