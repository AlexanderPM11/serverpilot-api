using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ServerPilot.Domain.Interfaces;
using ServerPilot.Infrastructure.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ServerPilot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ITelegramService _telegramService;

        public AuthController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, 
            IConfiguration configuration,
            ITelegramService telegramService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _telegramService = telegramService;
        }

        [HttpPost("register")]
        public IActionResult Register()
        {
            return NotFound("Registration is disabled. Contact the administrator.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user != null && await _userManager.CheckPasswordAsync(user, request.Password))
            {
                var token = GenerateJwtToken(user);
                return Ok(new { Token = token, Email = user.Email, RequiresPasswordChange = user.RequiresPasswordChange });
            }

            return Unauthorized();
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            return Ok(new { 
                Email = user.Email,
                TelegramBotToken = user.TelegramBotToken,
                TelegramChatId = user.TelegramChatId,
                RequiresPasswordChange = user.RequiresPasswordChange
            });
        }

        [Authorize]
        [HttpPut("profile/telegram")]
        public async Task<IActionResult> UpdateTelegramConfig([FromBody] TelegramConfigRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            user.TelegramBotToken = request.BotToken;
            user.TelegramChatId = request.ChatId;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded) return Ok(new { Message = "Telegram configuration updated." });
            return BadRequest(result.Errors);
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (result.Succeeded)
            {
                user.RequiresPasswordChange = false;
                await _userManager.UpdateAsync(user);
                return Ok(new { Message = "Password changed successfully." });
            }
            
            return BadRequest(result.Errors);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) 
            {
                // Don't reveal that the user does not exist
                return Ok(new { Message = "If that email exists, a recovery link has been sent via Telegram." });
            }

            if (string.IsNullOrEmpty(user.TelegramBotToken) || string.IsNullOrEmpty(user.TelegramChatId))
            {
                return BadRequest("Telegram recovery is not configured for this account.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            
            // Build reset URL. E.g. http://localhost:5174/reset-password?token=...&email=...
            var clientUrl = Environment.GetEnvironmentVariable("ClientUrl") ?? _configuration["ClientUrl"] ?? "http://localhost:5174";
            var encodedToken = Uri.EscapeDataString(token);
            var encodedEmail = Uri.EscapeDataString(user.Email!);
            var resetLink = $"{clientUrl}/reset-password?token={encodedToken}&email={encodedEmail}";

            var message = $"<b>ServerPilot SECURITY ALERT</b>\n\n" +
                          $"A password reset was requested for your account ({user.Email}).\n\n" +
                          $"Click the link below to set a new security key:\n" +
                          $"{resetLink}\n\n" +
                          $"<i>If you didn't request this, ignore this message.</i>";

            var sent = await _telegramService.SendMessageAsync(user.TelegramBotToken, user.TelegramChatId, message);

            if (!sent) return StatusCode(500, "Failed to send Telegram message. Please check bot token and chat ID.");

            return Ok(new { Message = "Recovery link sent via Telegram." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return BadRequest("Invalid request.");

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (result.Succeeded) return Ok(new { Message = "Password has been reset successfully." });

            return BadRequest(result.Errors);
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var keyStr = jwtSettings["Key"] ?? "SUPER_SECRET_KEY_FOR_SERVER_PILOT_STAGE_1";
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(keyStr));
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class RegisterRequest { public string Email { get; set; } = null!; public string Password { get; set; } = null!; }
    public class LoginRequest { public string Email { get; set; } = null!; public string Password { get; set; } = null!; }
    public class TelegramConfigRequest { public string BotToken { get; set; } = string.Empty; public string ChatId { get; set; } = string.Empty; }
    public class ChangePasswordRequest { public string CurrentPassword { get; set; } = null!; public string NewPassword { get; set; } = null!; }
    public class ForgotPasswordRequest { public string Email { get; set; } = null!; }
    public class ResetPasswordRequest { public string Email { get; set; } = null!; public string Token { get; set; } = null!; public string NewPassword { get; set; } = null!; }
}
