using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServerPilot.Domain.Interfaces
{
    public interface ISshService
    {
        Task<(bool Success, string Error)> ConnectAsync(string connectionId, string host, int port, string username, string? password, string? privateKey, string? passphrase);
        Task<string> ExecuteCommandAsync(string connectionId, string command);
        Task DisconnectAsync(string connectionId);
        bool IsConnected(string connectionId);
        
        // WebSocket/SignalR support
        Task StartTerminalSessionAsync(string connectionId, Func<string, Task> onDataReceived);
        Task SendTerminalInputAsync(string connectionId, string input);
    }
}
