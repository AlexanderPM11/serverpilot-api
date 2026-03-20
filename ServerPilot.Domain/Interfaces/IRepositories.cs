using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServerPilot.Domain.Entities;

namespace ServerPilot.Domain.Interfaces
{
    public interface IServerRepository
    {
        Task<IEnumerable<RemoteServer>> GetAllAsync();
        Task<RemoteServer?> GetByIdAsync(Guid id);
        Task AddAsync(RemoteServer server);
        Task UpdateAsync(RemoteServer server);
        Task DeleteAsync(Guid id);
    }

    public interface ISshKeyRepository
    {
        Task<IEnumerable<SshKey>> GetAllAsync();
        Task<SshKey?> GetByIdAsync(Guid id);
        Task AddAsync(SshKey key);
        Task DeleteAsync(Guid id);
    }
}
