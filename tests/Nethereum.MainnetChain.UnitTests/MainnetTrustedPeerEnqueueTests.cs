using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection("Sequential")]
    public class MainnetTrustedPeerEnqueueTests
    {
        private sealed class AcceptCounter
        {
            private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly List<TcpClient> _held = new List<TcpClient>();
            private readonly CancellationTokenSource _stop = new CancellationTokenSource();
            private readonly Task _acceptLoop;
            private int _accepts;

            public AcceptCounter()
            {
                _listener.Start();
                _acceptLoop = Task.Run(AcceptAsync);
            }

            public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
            public int Accepts => Volatile.Read(ref _accepts);

            private async Task AcceptAsync()
            {
                try
                {
                    while (!_stop.IsCancellationRequested)
                    {
                        var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                        lock (_held) _held.Add(client);
                        Interlocked.Increment(ref _accepts);
                    }
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
            }

            public async Task StopAsync()
            {
                _stop.Cancel();
                _listener.Stop();
                await _acceptLoop;
                lock (_held) foreach (var client in _held) client.Dispose();
                _stop.Dispose();
            }
        }

        [Fact]
        public async Task Given_TrustedPeersAndAMainnetStartAsync_When_TheNodeStarts_Then_EachTrustedEnodeIsEnqueuedExactlyOnceByTheSyncStack()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "mainnet-trusted-enqueue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);
            var trusted = new AcceptCounter();

            try
            {
                var enode = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{trusted.Port}";
                var services = new ServiceCollection();
                services.AddLogging();
                var config = new MainnetChainServerConfig
                {
                    DataDir = dataDir,
                    ListenPort = -1,
                    DisableDiscv4 = true,
                    DisableDiscv5 = true,
                    TargetPeers = 1,
                    TrustedPeer = enode,
                };
                await services.AddMainnetNodeAsync(config, NullLoggerFactory.Instance);
                await using var provider = services.BuildServiceProvider();
                var runtime = provider.GetServices<IHostedService>().Single(s => s.GetType().Name == "MainnetNodeRuntime");

                await WaitUntilAsync(() => trusted.Accepts >= 1);
                await runtime.StartAsync(CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(3));

                Assert.Equal(1, trusted.Accepts);
                await runtime.StopAsync(CancellationToken.None);
            }
            finally
            {
                await trusted.StopAsync();
                try { Directory.Delete(dataDir, recursive: true); }
                catch { }
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            Assert.True(condition(), "the trusted peer was never dialled within 30s");
        }
    }
}
