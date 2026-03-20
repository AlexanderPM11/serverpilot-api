using System;

namespace ServerPilot.Domain.Entities
{
    public class SshKey
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string? PublicKey { get; set; }
        public string? Passphrase { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
