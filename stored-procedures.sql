-- stored-procedures.sql
-- All stored procedures for the Expense Management application
-- Uses CREATE OR ALTER PROCEDURE to be idempotent
-- All app data access goes through these stored procedures ONLY

SET NOCOUNT ON;
GO

-- ============================================================
-- sp_GetExpenses: List expenses with optional filters
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetExpenses]
    @UserId     INT = NULL,
    @StatusId   INT = NULL,
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewerName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT  JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    WHERE
        (@UserId IS NULL     OR e.UserId = @UserId)
        AND (@StatusId IS NULL   OR e.StatusId = @StatusId)
        AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
    ORDER BY e.CreatedAt DESC;
END
GO

-- ============================================================
-- sp_GetExpenseById: Single expense with all joins
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetExpenseById]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewerName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT  JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- sp_CreateExpense: Insert a new expense (Draft status)
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_CreateExpense]
    @UserId      INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES
        (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS NewExpenseId;
END
GO

-- ============================================================
-- sp_UpdateExpense: Update an existing expense
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_UpdateExpense]
    @ExpenseId   INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Expenses
    SET
        CategoryId  = @CategoryId,
        AmountMinor = @AmountMinor,
        Currency    = @Currency,
        ExpenseDate = @ExpenseDate,
        Description = @Description,
        ReceiptFile = @ReceiptFile
    WHERE ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- sp_DeleteExpense: Delete an expense by ID
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_DeleteExpense]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- sp_SubmitExpense: Set status to Submitted, record SubmittedAt
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_SubmitExpense]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    UPDATE dbo.Expenses
    SET
        StatusId    = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- sp_ApproveExpense: Set status to Approved, record reviewer
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ApproveExpense]
    @ExpenseId   INT,
    @ReviewedBy  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';

    UPDATE dbo.Expenses
    SET
        StatusId   = @ApprovedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- sp_RejectExpense: Set status to Rejected, record reviewer
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_RejectExpense]
    @ExpenseId   INT,
    @ReviewedBy  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';

    UPDATE dbo.Expenses
    SET
        StatusId   = @RejectedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- sp_GetUsers: List all users with their role name
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetUsers]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    ORDER BY u.UserName;
END
GO

-- ============================================================
-- sp_GetUserById: Single user by ID
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetUserById]
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.UserId = @UserId;
END
GO

-- ============================================================
-- sp_CreateUser: Insert a new user
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_CreateUser]
    @UserName  NVARCHAR(100),
    @Email     NVARCHAR(255),
    @RoleId    INT,
    @ManagerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Users (UserName, Email, RoleId, ManagerId, IsActive, CreatedAt)
    VALUES (@UserName, @Email, @RoleId, @ManagerId, 1, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS NewUserId;
END
GO

-- ============================================================
-- sp_GetCategories: List all expense categories
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetCategories]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- ============================================================
-- sp_GetStatuses: List all expense statuses
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetStatuses]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO
