using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseApp.Models;

namespace ExpenseApp.Services;

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IConfiguration configuration,
        IExpenseService expenseService,
        ILogger<ChatService> logger)
    {
        _configuration  = configuration;
        _expenseService = expenseService;
        _logger         = logger;
    }

    public async Task<string> ChatAsync(string userMessage, IList<(string role, string content)> history)
    {
        var endpoint   = _configuration["OpenAI:Endpoint"];
        var deployment = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("REPLACE"))
        {
            return "⚠️ **Azure OpenAI is not configured.** " +
                   "To enable the AI assistant, deploy the GenAI infrastructure by running:\n\n" +
                   "```bash\nbash deploy-with-chat.sh\n```\n\n" +
                   "This will provision Azure OpenAI and AI Search resources and configure the app automatically.";
        }

        try
        {
            // Use ManagedIdentityCredential with explicit client ID for user-assigned identity
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            Azure.Core.TokenCredential credential;

            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new OpenAIClient(new Uri(endpoint), credential);

            // Build function tool definitions
            var tools = new List<ChatCompletionsFunctionToolDefinition>
            {
                new ChatCompletionsFunctionToolDefinition
                {
                    Name        = "get_expenses",
                    Description = "Retrieves a list of expenses from the database with optional filters.",
                    Parameters  = BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "userId":     { "type": "integer", "description": "Filter by user ID (optional)" },
                            "statusId":   { "type": "integer", "description": "Filter by status ID: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected (optional)" },
                            "categoryId": { "type": "integer", "description": "Filter by category ID (optional)" }
                        },
                        "required": []
                    }
                    """)
                },
                new ChatCompletionsFunctionToolDefinition
                {
                    Name        = "get_users",
                    Description = "Retrieves a list of all users in the system.",
                    Parameters  = BinaryData.FromString("""{ "type": "object", "properties": {}, "required": [] }""")
                },
                new ChatCompletionsFunctionToolDefinition
                {
                    Name        = "create_expense",
                    Description = "Creates a new expense in Draft status.",
                    Parameters  = BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "userId":      { "type": "integer", "description": "User ID for the expense owner" },
                            "categoryId":  { "type": "integer", "description": "Category ID (1=Travel,2=Meals,3=Supplies,4=Accommodation,5=Other)" },
                            "amountMinor": { "type": "integer", "description": "Amount in pence (e.g. £12.34 = 1234)" },
                            "expenseDate": { "type": "string",  "description": "Date of the expense in YYYY-MM-DD format" },
                            "description": { "type": "string",  "description": "Description of the expense" }
                        },
                        "required": ["userId", "categoryId", "amountMinor", "expenseDate"]
                    }
                    """)
                },
                new ChatCompletionsFunctionToolDefinition
                {
                    Name        = "approve_expense",
                    Description = "Approves a submitted expense.",
                    Parameters  = BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId":  { "type": "integer", "description": "The ID of the expense to approve" },
                            "reviewedBy": { "type": "integer", "description": "The user ID of the manager approving the expense" }
                        },
                        "required": ["expenseId", "reviewedBy"]
                    }
                    """)
                },
                new ChatCompletionsFunctionToolDefinition
                {
                    Name        = "reject_expense",
                    Description = "Rejects a submitted expense.",
                    Parameters  = BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId":  { "type": "integer", "description": "The ID of the expense to reject" },
                            "reviewedBy": { "type": "integer", "description": "The user ID of the manager rejecting the expense" }
                        },
                        "required": ["expenseId", "reviewedBy"]
                    }
                    """)
                }
            };

            // Build chat messages
            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(
                    "You are a helpful expense management assistant. You have access to real functions to interact with the database. " +
                    "Available capabilities:\n" +
                    "- get_expenses: List and filter expenses\n" +
                    "- get_users: List all users\n" +
                    "- create_expense: Create a new expense (amounts in pence, e.g. £12.34 = 1234)\n" +
                    "- approve_expense: Approve a submitted expense\n" +
                    "- reject_expense: Reject a submitted expense\n\n" +
                    "When showing lists, format them clearly with bullet points. Be concise and helpful.")
            };

            // Add history
            foreach (var (role, content) in history)
            {
                if (role == "user")
                    messages.Add(new ChatRequestUserMessage(content));
                else if (role == "assistant")
                    messages.Add(new ChatRequestAssistantMessage(content));
            }
            messages.Add(new ChatRequestUserMessage(userMessage));

            // Agentic loop
            for (int iteration = 0; iteration < 5; iteration++)
            {
                var options = new ChatCompletionsOptions(deployment, messages)
                {
                    Temperature = 0.7f,
                    MaxTokens   = 1024
                };
                foreach (var tool in tools)
                    options.Tools.Add(tool);

                var response = await client.GetChatCompletionsAsync(options);
                var choice   = response.Value.Choices[0];

                if (choice.FinishReason == CompletionsFinishReason.ToolCalls)
                {
                    // Add the assistant message with tool calls
                    var assistantMsg = new ChatRequestAssistantMessage(choice.Message);
                    messages.Add(assistantMsg);

                    foreach (var toolCall in choice.Message.ToolCalls.OfType<ChatCompletionsFunctionToolCall>())
                    {
                        var result = await ExecuteFunctionAsync(toolCall.Name, toolCall.Arguments);
                        messages.Add(new ChatRequestToolMessage(result, toolCall.Id));
                    }
                }
                else
                {
                    return choice.Message.Content ?? "I'm sorry, I couldn't generate a response.";
                }
            }

            return "I wasn't able to complete that request after multiple attempts.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatService");
            return $"⚠️ An error occurred: {ex.Message}. Please check the Azure OpenAI configuration.";
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            using var doc  = JsonDocument.Parse(arguments);
            var args = doc.RootElement;

            switch (functionName)
            {
                case "get_expenses":
                {
                    int? userId     = args.TryGetProperty("userId",     out var u) && u.ValueKind != JsonValueKind.Null ? u.GetInt32() : null;
                    int? statusId   = args.TryGetProperty("statusId",   out var s) && s.ValueKind != JsonValueKind.Null ? s.GetInt32() : null;
                    int? categoryId = args.TryGetProperty("categoryId", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetInt32() : null;
                    var (expenses, _) = await _expenseService.GetExpensesAsync(userId, statusId, categoryId);
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId, e.UserName, e.CategoryName, e.StatusName,
                        AmountGBP = e.AmountGBP.ToString("F2"), e.Currency,
                        ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"), e.Description
                    }));
                }
                case "get_users":
                {
                    var (users, _) = await _expenseService.GetUsersAsync();
                    return JsonSerializer.Serialize(users.Select(u => new
                    {
                        u.UserId, u.UserName, u.Email, u.RoleName, u.IsActive
                    }));
                }
                case "create_expense":
                {
                    int    userId      = args.GetProperty("userId").GetInt32();
                    int    categoryId  = args.GetProperty("categoryId").GetInt32();
                    int    amountMinor = args.GetProperty("amountMinor").GetInt32();
                    string dateStr     = args.GetProperty("expenseDate").GetString()!;
                    string? desc       = args.TryGetProperty("description", out var d) ? d.GetString() : null;
                    var date = DateTime.Parse(dateStr);
                    var (newId, error) = await _expenseService.CreateExpenseAsync(userId, categoryId, amountMinor, "GBP", date, desc, null);
                    return newId.HasValue
                        ? $"{{\"success\": true, \"newExpenseId\": {newId.Value}}}"
                        : $"{{\"success\": false, \"error\": \"{error}\"}}";
                }
                case "approve_expense":
                {
                    int expenseId  = args.GetProperty("expenseId").GetInt32();
                    int reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                    var (success, error) = await _expenseService.ApproveExpenseAsync(expenseId, reviewedBy);
                    return $"{{\"success\": {success.ToString().ToLower()}, \"error\": \"{error}\"}}";
                }
                case "reject_expense":
                {
                    int expenseId  = args.GetProperty("expenseId").GetInt32();
                    int reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                    var (success, error) = await _expenseService.RejectExpenseAsync(expenseId, reviewedBy);
                    return $"{{\"success\": {success.ToString().ToLower()}, \"error\": \"{error}\"}}";
                }
                default:
                    return $"{{\"error\": \"Unknown function: {functionName}\"}}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }
}
