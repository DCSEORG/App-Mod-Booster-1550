using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>Send a chat message to the AI assistant</summary>
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty" });

        var history = request.History ?? new List<ChatHistoryItem>();
        var historyTuples = history
            .Select(h => (h.Role, h.Content))
            .ToList();

        var response = await _chatService.ChatAsync(request.Message, historyTuples);
        return Ok(new { response });
    }
}

public record ChatRequest(string Message, List<ChatHistoryItem>? History);
public record ChatHistoryItem(string Role, string Content);
