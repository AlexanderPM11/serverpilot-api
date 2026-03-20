using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;
using ServerPilot.Domain.Interfaces;

namespace ServerPilot.Infrastructure.Services
{
    public class SshService : ISshService, IDisposable
    {
        private readonly ConcurrentDictionary<string, SshClient> _clients = new();
        private readonly ConcurrentDictionary<string, ShellStream> _streams = new();

        public bool IsConnected(string connectionId) => _clients.TryGetValue(connectionId, out var client) && client.IsConnected;

        public async Task<(bool Success, string Error)> ConnectAsync(string connectionId, string host, int port, string username, string? password, string? privateKey, string? passphrase)
        {
            await DisconnectAsync(connectionId);

            ConnectionInfo connectionInfo;
            if (!string.IsNullOrEmpty(privateKey))
            {
                var keyBytes = Encoding.UTF8.GetBytes(privateKey);
                using var memoryStream = new MemoryStream(keyBytes);
                var keyFile = new PrivateKeyFile(memoryStream, passphrase);
                connectionInfo = new ConnectionInfo(host, port, username, new PrivateKeyAuthenticationMethod(username, keyFile));
            }
            else
            {
                connectionInfo = new ConnectionInfo(host, port, username, new PasswordAuthenticationMethod(username, password ?? ""));
            }

            var client = new SshClient(connectionInfo);
            try
            {
                await Task.Run(() => client.Connect());
                if (client.IsConnected)
                {
                    _clients[connectionId] = client;
                    return (true, string.Empty);
                }
                return (false, "Socket closed immediately after connection.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<string> ExecuteCommandAsync(string connectionId, string command)
        {
            if (!_clients.TryGetValue(connectionId, out var client) || !client.IsConnected)
                return "Not connected";

            return await Task.Run(() =>
            {
                var cmd = client.CreateCommand(command);
                return cmd.Execute();
            });
        }

        public async Task StartTerminalSessionAsync(string connectionId, Func<string, Task> onDataReceived)
        {
            if (!_clients.TryGetValue(connectionId, out var client) || !client.IsConnected) return;

            var stream = client.CreateShellStream("xterm", 80, 24, 800, 600, 1024);
            _streams[connectionId] = stream;

            stream.DataReceived += (sender, e) =>
            {
                var output = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"[SSH RECEIVED]: {output}");
                _ = onDataReceived(output);
            };

            stream.ErrorOccurred += (sender, e) =>
            {
                Console.WriteLine($"[SSH SYSTEM ERR]: {e.Exception.Message}");
            };

            // Force the Windows SSH Server to render the initial prompt
            await Task.Delay(500);
            stream.WriteLine("");
        }

        public async Task SendTerminalInputAsync(string connectionId, string input)
        {
            if (_streams.TryGetValue(connectionId, out var stream))
            {
                Console.WriteLine($"[SSH SENDING]: {input}");
                var bytes = Encoding.UTF8.GetBytes(input);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
        }

        public async Task DisconnectAsync(string connectionId)
        {
            if (_streams.TryRemove(connectionId, out var stream))
            {
                stream.Dispose();
            }

            if (_clients.TryRemove(connectionId, out var client))
            {
                await Task.Run(() =>
                {
                    if (client.IsConnected) client.Disconnect();
                    client.Dispose();
                });
            }
        }

        public void Dispose()
        {
            foreach (var key in _clients.Keys)
            {
                DisconnectAsync(key).Wait();
            }
        }
    }
}
