using System.Threading.Tasks;

namespace ServerPilot.Domain.Interfaces
{
    public interface ITelegramService
    {
        Task<bool> SendMessageAsync(string botToken, string chatId, string message);
    }
}
