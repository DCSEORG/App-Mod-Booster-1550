namespace ExpenseApp.Services;

public interface IChatService
{
    Task<string> ChatAsync(string userMessage, IList<(string role, string content)> history);
}
