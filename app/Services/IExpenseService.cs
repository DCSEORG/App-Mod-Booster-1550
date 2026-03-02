using ExpenseApp.Models;

namespace ExpenseApp.Services;

public interface IExpenseService
{
    Task<(List<Expense> expenses, string? error)> GetExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null);
    Task<(Expense? expense, string? error)> GetExpenseByIdAsync(int expenseId);
    Task<(int? newId, string? error)> CreateExpenseAsync(int userId, int categoryId, int amountMinor, string currency, DateTime expenseDate, string? description, string? receiptFile);
    Task<(bool success, string? error)> UpdateExpenseAsync(int expenseId, int categoryId, int amountMinor, string currency, DateTime expenseDate, string? description, string? receiptFile);
    Task<(bool success, string? error)> DeleteExpenseAsync(int expenseId);
    Task<(bool success, string? error)> SubmitExpenseAsync(int expenseId);
    Task<(bool success, string? error)> ApproveExpenseAsync(int expenseId, int reviewedBy);
    Task<(bool success, string? error)> RejectExpenseAsync(int expenseId, int reviewedBy);
    Task<(List<User> users, string? error)> GetUsersAsync();
    Task<(User? user, string? error)> GetUserByIdAsync(int userId);
    Task<(int? newId, string? error)> CreateUserAsync(string userName, string email, int roleId, int? managerId);
    Task<(List<ExpenseCategory> categories, string? error)> GetCategoriesAsync();
    Task<(List<ExpenseStatus> statuses, string? error)> GetStatusesAsync();
}
