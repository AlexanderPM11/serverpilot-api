using Microsoft.AspNetCore.Identity;

namespace ServerPilot.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? TelegramBotToken { get; set; }
        public string? TelegramChatId { get; set; }
        public bool RequiresPasswordChange { get; set; } = true;
    }
}
