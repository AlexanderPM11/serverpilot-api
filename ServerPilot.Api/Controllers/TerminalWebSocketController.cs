using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using ServerPilot.Domain.Interfaces;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;

namespace ServerPilot.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TerminalWebSocketController : ControllerBase
    {
        private readonly ISshService _sshService;

        public TerminalWebSocketController(ISshService sshService)
        {
            _sshService = sshService;
        }

        [HttpGet("connect/{serverId}")]
        public async Task Connect(string serverId)
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // The SshService needs server credentials; we look them up from the DB.
            // For now, the client sends them as query params over the secure WS.
            var host = HttpContext.Request.Query["host"].ToString();
            var port = int.TryParse(HttpContext.Request.Query["port"], out var p) ? p : 22;
            var username = HttpContext.Request.Query["username"].ToString();
            var password = HttpContext.Request.Query["password"].ToString();

            var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString();

            // Connect SSH
            var connectResult = await _sshService.ConnectAsync(connectionId, host, port, username, password, null, null);
            if (!connectResult.Success)
            {
                var errBytes = Encoding.UTF8.GetBytes($"SSH Error: {connectResult.Error}");
                await ws.SendAsync(errBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "SSH connection failed", CancellationToken.None);
                return;
            }

            // Open ShellStream
            await _sshService.StartTerminalSessionAsync(connectionId, async (data) =>
            {
                if (ws.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(data);
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            });

            // Forward browser input to SSH
            var buffer = new byte[4096];
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var input = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await _sshService.SendTerminalInputAsync(connectionId, input);
                }
            }
            finally
            {
                await _sshService.DisconnectAsync(connectionId);
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
            }
        }
    }
}
