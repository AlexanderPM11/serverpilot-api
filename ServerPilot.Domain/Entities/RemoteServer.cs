using System;

namespace ServerPilot.Domain.Entities
{
    public class RemoteServer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? PrivateKey { get; set; }
        public AuthMethod AuthMethod { get; set; } = AuthMethod.Password;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastConnected { get; set; }
    }

    public enum AuthMethod
    {
        Password,
        SshKey
    }
}
