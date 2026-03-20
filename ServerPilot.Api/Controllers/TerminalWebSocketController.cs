using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerPilot.Domain.Interfaces;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

            var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();

            // ── Security: credentials arrive in the FIRST WebSocket message (JSON) ──
            // This keeps the SSH password out of URLs, server logs, and browser history.
            var buffer = new byte[4096];
            var initResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (initResult.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed before init", CancellationToken.None);
                return;
            }

            SshCredentials? creds;
            try
            {
                var json = Encoding.UTF8.GetString(buffer, 0, initResult.Count);
                creds = JsonSerializer.Deserialize<SshCredentials>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var err = Encoding.UTF8.GetBytes("Invalid credential format.");
                await ws.SendAsync(err, WebSocketMessageType.Text, true, CancellationToken.None);
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Bad init message", CancellationToken.None);
                return;
            }

            if (creds is null || string.IsNullOrEmpty(creds.Host) || string.IsNullOrEmpty(creds.Username))
            {
                var err = Encoding.UTF8.GetBytes("Missing required SSH credentials.");
                await ws.SendAsync(err, WebSocketMessageType.Text, true, CancellationToken.None);
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Missing credentials", CancellationToken.None);
                return;
            }

            var connectionId = Guid.NewGuid().ToString();

            // Connect SSH using credentials from the secure init message
            var connectResult = await _sshService.ConnectAsync(
                connectionId, creds.Host, creds.Port, creds.Username, creds.Password ?? "", null, null);

            if (!connectResult.Success)
            {
                var errBytes = Encoding.UTF8.GetBytes($"SSH Error: {connectResult.Error}");
                await ws.SendAsync(errBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "SSH connection failed", CancellationToken.None);
                return;
            }

            // Open ShellStream and pipe output → WebSocket
            await _sshService.StartTerminalSessionAsync(connectionId, async (data) =>
            {
                if (ws.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(data);
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            });

            // Forward browser keystrokes → SSH
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

    /// <summary>Credentials sent as the first WebSocket message after connection.</summary>
    public record SshCredentials(string Host, int Port, string Username, string? Password);
}
