using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ServerPilot.Api.Hubs;
using ServerPilot.Domain.Interfaces;
using ServerPilot.Infrastructure.Persistence;
using ServerPilot.Infrastructure.Services;
using ServerPilot.Infrastructure.Identity;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// DB & Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=serverpilot.db"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Auth
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "SUPER_SECRET_KEY_FOR_SERVER_PILOT_STAGE_1");

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
    // Allow JWT from query string for WebSocket connections
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["token"];
            if (!string.IsNullOrEmpty(accessToken))
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "ServerPilot API",
        Version     = "v1",
        Description = "Backend API for ServerPilot — server monitoring & management."
    });

    // JWT Authentication button in Swagger UI
    var securityScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Paste your JWT token here (without the 'Bearer' prefix)."
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSignalR();

// DI
builder.Services.AddSingleton<ISshService, SshService>();  // Singleton so SSH state persists across requests
builder.Services.AddHttpClient<ITelegramService, TelegramService>();

builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", b => b
        .SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});

var app = builder.Build();

// Migrate DB and Seed
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (!userManager.Users.Any())
    {
        // Read credentials from environment variables (set in docker-compose or system env)
        var adminEmail    = Environment.GetEnvironmentVariable("ADMIN_EMAIL")    ?? "admin@serverpilot.local";
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin123!";

        var defaultUser = new ApplicationUser 
        { 
            UserName = adminEmail, 
            Email = adminEmail,
            RequiresPasswordChange = false   // No forced change — admin sets preferred creds via env
        };
        userManager.CreateAsync(defaultUser, adminPassword).Wait();
    }
}

// Swagger available in all environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ServerPilot API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TerminalHub>("/ws/terminal");

app.Run();
