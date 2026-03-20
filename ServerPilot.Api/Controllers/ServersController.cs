using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerPilot.Domain.Entities;
using ServerPilot.Infrastructure.Persistence;
using System.Security.Claims;

namespace ServerPilot.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ServersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ServersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetServers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var servers = await _context.Servers.Where(s => s.UserId == userId).ToListAsync();
            return Ok(servers);
        }

        [HttpPost]
        public async Task<IActionResult> CreateServer([FromBody] RemoteServer server)
        {
            server.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            server.CreatedAt = DateTime.UtcNow;
            _context.Servers.Add(server);
            await _context.SaveChangesAsync();
            return Ok(server);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateServer(Guid id, [FromBody] RemoteServer server)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = await _context.Servers.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            
            if (existing == null) return NotFound();

            existing.Name = server.Name;
            existing.Host = server.Host;
            existing.Port = server.Port;
            existing.Username = server.Username;
            existing.Password = server.Password;
            existing.PrivateKey = server.PrivateKey;
            existing.AuthMethod = server.AuthMethod;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServer(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var server = await _context.Servers.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            
            if (server == null) return NotFound();

            _context.Servers.Remove(server);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
