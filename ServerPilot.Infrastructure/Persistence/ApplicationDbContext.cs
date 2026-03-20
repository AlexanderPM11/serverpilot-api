using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServerPilot.Domain.Entities;
using ServerPilot.Infrastructure.Identity;

namespace ServerPilot.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<RemoteServer> Servers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var encryptionConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<string?, string?>(
                v => ServerPilot.Infrastructure.Services.AesEncryptionProvider.Encrypt(v),
                v => ServerPilot.Infrastructure.Services.AesEncryptionProvider.Decrypt(v)
            );

            builder.Entity<RemoteServer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Host).IsRequired();
                entity.Property(e => e.Username).IsRequired();

                // Cifrado transparente DB <-> Entidad
                entity.Property(e => e.Password).HasConversion(encryptionConverter);
                entity.Property(e => e.PrivateKey).HasConversion(encryptionConverter);
            });
        }
    }
}
