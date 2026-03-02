using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseApp.Pages;

public class ChatModel : PageModel
{
    public void OnGet()
    {
        ViewData["Title"] = "AI Assistant";
    }
}
