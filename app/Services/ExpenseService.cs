using System.Runtime.CompilerServices;
using ExpenseApp.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseApp.Services;

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<SqlConnection> OpenConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static string FormatError(Exception ex,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        var fileName = Path.GetFileName(file);
        return $"Database error in {fileName} at line {line}: {ex.Message}. " +
               "If using Managed Identity, ensure the identity is assigned to the App Service and " +
               "has been granted db_datareader/db_datawriter/EXECUTE roles in the database. " +
               "For local development, set Authentication=Active Directory Default in appsettings.json and run 'az login'.";
    }

    // ─── dummy data ────────────────────────────────────────────────────────────

    private static List<Expense> GetDummyExpenses() => new()
    {
        new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel",
            StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP",
            ExpenseDate = DateTime.Today.AddDays(-14), Description = "Taxi from airport to client site",
            ReceiptFile = "/receipts/alice/taxi.jpg", SubmittedAt = DateTime.UtcNow.AddDays(-13), CreatedAt = DateTime.UtcNow.AddDays(-14) },
        new Expense { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals",
            StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Currency = "GBP",
            ExpenseDate = DateTime.Today.AddDays(-49), Description = "Client lunch meeting",
            ReceiptFile = "/receipts/alice/lunch.jpg", SubmittedAt = DateTime.UtcNow.AddDays(-48),
            ReviewedBy = 2, ReviewerName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-47),
            CreatedAt = DateTime.UtcNow.AddDays(-49) },
        new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies",
            StatusId = 1, StatusName = "Draft", AmountMinor = 799, Currency = "GBP",
            ExpenseDate = DateTime.Today.AddDays(-3), Description = "Office stationery",
            CreatedAt = DateTime.UtcNow.AddDays(-3) },
        new Expense { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 4, CategoryName = "Accommodation",
            StatusId = 3, StatusName = "Approved", AmountMinor = 12300, Currency = "GBP",
            ExpenseDate = DateTime.Today.AddDays(-84), Description = "Hotel during client visit",
            ReceiptFile = "/receipts/alice/hotel.jpg", SubmittedAt = DateTime.UtcNow.AddDays(-83),
            ReviewedBy = 2, ReviewerName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-82),
            CreatedAt = DateTime.UtcNow.AddDays(-84) }
    };

    private static List<User> GetDummyUsers() => new()
    {
        new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-100) },
        new User { UserId = 2, UserName = "Bob Manager",   Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-100) }
    };

    private static List<ExpenseCategory> GetDummyCategories() => new()
    {
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel",        IsActive = true },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals",         IsActive = true },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies",      IsActive = true },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Other",         IsActive = true }
    };

    private static List<ExpenseStatus> GetDummyStatuses() => new()
    {
        new ExpenseStatus { StatusId = 1, StatusName = "Draft"     },
        new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
        new ExpenseStatus { StatusId = 3, StatusName = "Approved"  },
        new ExpenseStatus { StatusId = 4, StatusName = "Rejected"  }
    };

    // ─── Expenses ──────────────────────────────────────────────────────────────

    public async Task<(List<Expense> expenses, string? error)> GetExpensesAsync(
        int? userId = null, int? statusId = null, int? categoryId = null)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_GetExpenses", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId",     (object?)userId     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StatusId",   (object?)statusId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);

            var expenses = new List<Expense>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                expenses.Add(MapExpense(reader));
            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExpensesAsync");
            return (GetDummyExpenses(), FormatError(ex));
        }
    }

    public async Task<(Expense? expense, string? error)> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_GetExpenseById", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (MapExpense(reader), null);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExpenseByIdAsync({ExpenseId})", expenseId);
            var dummy = GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
            return (dummy, FormatError(ex));
        }
    }

    public async Task<(int? newId, string? error)> CreateExpenseAsync(
        int userId, int categoryId, int amountMinor, string currency,
        DateTime expenseDate, string? description, string? receiptFile)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_CreateExpense", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId",      userId);
            cmd.Parameters.AddWithValue("@CategoryId",  categoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", amountMinor);
            cmd.Parameters.AddWithValue("@Currency",    currency);
            cmd.Parameters.AddWithValue("@ExpenseDate", expenseDate.Date);
            cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)receiptFile ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return (Convert.ToInt32(result), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateExpenseAsync");
            return (null, FormatError(ex));
        }
    }

    public async Task<(bool success, string? error)> UpdateExpenseAsync(
        int expenseId, int categoryId, int amountMinor, string currency,
        DateTime expenseDate, string? description, string? receiptFile)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_UpdateExpense", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId",   expenseId);
            cmd.Parameters.AddWithValue("@CategoryId",  categoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", amountMinor);
            cmd.Parameters.AddWithValue("@Currency",    currency);
            cmd.Parameters.AddWithValue("@ExpenseDate", expenseDate.Date);
            cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)receiptFile ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateExpenseAsync({ExpenseId})", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool success, string? error)> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_DeleteExpense", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteExpenseAsync({ExpenseId})", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool success, string? error)> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_SubmitExpense", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SubmitExpenseAsync({ExpenseId})", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool success, string? error)> ApproveExpenseAsync(int expenseId, int reviewedBy)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_ApproveExpense", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId",  expenseId);
            cmd.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ApproveExpenseAsync({ExpenseId})", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool success, string? error)> RejectExpenseAsync(int expenseId, int reviewedBy)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_RejectExpense", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId",  expenseId);
            cmd.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RejectExpenseAsync({ExpenseId})", expenseId);
            return (false, FormatError(ex));
        }
    }

    // ─── Users ─────────────────────────────────────────────────────────────────

    public async Task<(List<User> users, string? error)> GetUsersAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_GetUsers", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            var users = new List<User>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                users.Add(MapUser(reader));
            return (users, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUsersAsync");
            return (GetDummyUsers(), FormatError(ex));
        }
    }

    public async Task<(User? user, string? error)> GetUserByIdAsync(int userId)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_GetUserById", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (MapUser(reader), null);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserByIdAsync({UserId})", userId);
            var dummy = GetDummyUsers().FirstOrDefault(u => u.UserId == userId);
            return (dummy, FormatError(ex));
        }
    }

    public async Task<(int? newId, string? error)> CreateUserAsync(
        string userName, string email, int roleId, int? managerId)
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_CreateUser", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserName",  userName);
            cmd.Parameters.AddWithValue("@Email",     email);
            cmd.Parameters.AddWithValue("@RoleId",    roleId);
            cmd.Parameters.AddWithValue("@ManagerId", (object?)managerId ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return (Convert.ToInt32(result), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateUserAsync");
            return (null, FormatError(ex));
        }
    }

    // ─── Categories & Statuses ─────────────────────────────────────────────────

    public async Task<(List<ExpenseCategory> categories, string? error)> GetCategoriesAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_GetCategories", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            var categories = new List<ExpenseCategory>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                categories.Add(new ExpenseCategory
                {
                    CategoryId   = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive     = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            return (categories, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCategoriesAsync");
            return (GetDummyCategories(), FormatError(ex));
        }
    }

    public async Task<(List<ExpenseStatus> statuses, string? error)> GetStatusesAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("sp_GetStatuses", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            var statuses = new List<ExpenseStatus>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                statuses.Add(new ExpenseStatus
                {
                    StatusId   = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            return (statuses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetStatusesAsync");
            return (GetDummyStatuses(), FormatError(ex));
        }
    }

    // ─── Mappers ───────────────────────────────────────────────────────────────

    private static Expense MapExpense(SqlDataReader reader)
    {
        int GetOrdinal(string name) => reader.GetOrdinal(name);

        return new Expense
        {
            ExpenseId    = reader.GetInt32(GetOrdinal("ExpenseId")),
            UserId       = reader.GetInt32(GetOrdinal("UserId")),
            UserName     = reader.GetString(GetOrdinal("UserName")),
            CategoryId   = reader.GetInt32(GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(GetOrdinal("CategoryName")),
            StatusId     = reader.GetInt32(GetOrdinal("StatusId")),
            StatusName   = reader.GetString(GetOrdinal("StatusName")),
            AmountMinor  = reader.GetInt32(GetOrdinal("AmountMinor")),
            Currency     = reader.GetString(GetOrdinal("Currency")),
            ExpenseDate  = reader.GetDateTime(GetOrdinal("ExpenseDate")),
            Description  = reader.IsDBNull(GetOrdinal("Description"))  ? null : reader.GetString(GetOrdinal("Description")),
            ReceiptFile  = reader.IsDBNull(GetOrdinal("ReceiptFile"))   ? null : reader.GetString(GetOrdinal("ReceiptFile")),
            SubmittedAt  = reader.IsDBNull(GetOrdinal("SubmittedAt"))   ? null : reader.GetDateTime(GetOrdinal("SubmittedAt")),
            ReviewedBy   = reader.IsDBNull(GetOrdinal("ReviewedBy"))    ? null : reader.GetInt32(GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(GetOrdinal("ReviewerName"))  ? null : reader.GetString(GetOrdinal("ReviewerName")),
            ReviewedAt   = reader.IsDBNull(GetOrdinal("ReviewedAt"))    ? null : reader.GetDateTime(GetOrdinal("ReviewedAt")),
            CreatedAt    = reader.GetDateTime(GetOrdinal("CreatedAt"))
        };
    }

    private static User MapUser(SqlDataReader reader)
    {
        int GetOrdinal(string name) => reader.GetOrdinal(name);

        return new User
        {
            UserId    = reader.GetInt32(GetOrdinal("UserId")),
            UserName  = reader.GetString(GetOrdinal("UserName")),
            Email     = reader.GetString(GetOrdinal("Email")),
            RoleId    = reader.GetInt32(GetOrdinal("RoleId")),
            RoleName  = reader.GetString(GetOrdinal("RoleName")),
            ManagerId = reader.IsDBNull(GetOrdinal("ManagerId")) ? null : reader.GetInt32(GetOrdinal("ManagerId")),
            IsActive  = reader.GetBoolean(GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(GetOrdinal("CreatedAt"))
        };
    }
}
