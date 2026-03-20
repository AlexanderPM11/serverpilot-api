using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerPilot.Domain.Entities;
using ServerPilot.Domain.Interfaces;
using System.Threading.Tasks;

namespace ServerPilot.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SshController : ControllerBase
    {
        private readonly ISshService _sshService;

        public SshController(ISshService sshService)
        {
            _sshService = sshService;
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] SshConnectRequest request)
        {
            var result = await _sshService.ConnectAsync(
                request.ConnectionId,
                request.Host, 
                request.Port, 
                request.Username, 
                request.Password, 
                request.PrivateKey, 
                request.Passphrase);

            if (result.Success) return Ok();
            return BadRequest(result.Error);
        }

        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect([FromBody] SshDisconnectRequest request)
        {
            await _sshService.DisconnectAsync(request.ConnectionId);
            return Ok();
        }

        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] SshExecuteRequest request)
        {
            var result = await _sshService.ExecuteCommandAsync(request.ConnectionId, request.Command);
            return Ok(result);
        }
    }

    public class SshDisconnectRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
    }

    public class SshExecuteRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
    }

    public class SshConnectRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? PrivateKey { get; set; }
        public string? Passphrase { get; set; }
    }
}
