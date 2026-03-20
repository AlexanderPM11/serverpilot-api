using Microsoft.AspNetCore.SignalR;
using ServerPilot.Domain.Interfaces;
using System;
using System.Threading.Tasks;

namespace ServerPilot.Api.Hubs
{
    public class TerminalHub : Hub
    {
        private readonly ISshService _sshService;

        public TerminalHub(ISshService sshService)
        {
            _sshService = sshService;
        }

        public async Task StartTerminal()
        {
            var connectionId = Context.ConnectionId;
            await _sshService.StartTerminalSessionAsync(connectionId, async (data) =>
            {
                await Clients.Client(connectionId).SendAsync("TerminalData", data);
            });
        }

        public async Task SendCommand(string input)
        {
            await _sshService.SendTerminalInputAsync(Context.ConnectionId, input);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _sshService.DisconnectAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
