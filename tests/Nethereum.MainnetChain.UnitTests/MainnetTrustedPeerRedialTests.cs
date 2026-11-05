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
    public class MainnetTrustedPeerRedialTests
    {
        [Fact]
        public async Task Given_ATrustedPeerThatClosesEveryConnectionAtOnce_When_TheNodeStarts_Then_StartAsyncDoesNotDialItAgain()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "mainnet-trusted-redial-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);
            var trusted = new TcpListener(IPAddress.Loopback, 0);
            trusted.Start();
            var accepts = 0;
            using var stop = new CancellationTokenSource();
            var acceptLoop = Task.Run(() => AcceptAndCloseAsync(trusted, stop.Token, () => Interlocked.Increment(ref accepts)));

            try
            {
                var port = ((IPEndPoint)trusted.LocalEndpoint).Port;
                var enode = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";
                await using var provider = await BuildProviderAsync(dataDir, enode);
                var runtime = provider.GetServices<IHostedService>().Single(s => s.GetType().Name == "MainnetNodeRuntime");

                await WaitUntilAsync(() => Volatile.Read(ref accepts) >= 1);
                await Task.Delay(TimeSpan.FromSeconds(1));
                await runtime.StartAsync(CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(3));

                Assert.Equal(1, Volatile.Read(ref accepts));
                await runtime.StopAsync(CancellationToken.None);
            }
            finally
            {
                stop.Cancel();
                trusted.Stop();
                await acceptLoop;
                try { Directory.Delete(dataDir, recursive: true); }
                catch { }
            }
        }

        private static async Task<ServiceProvider> BuildProviderAsync(string dataDir, string trustedEnode)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var config = new MainnetChainServerConfig
            {
                DataDir = dataDir,
                ListenPort = -1,
                DisableDiscv4 = true,
                DisableDiscv5 = true,
                TargetPeers = 1,
                TrustedPeer = trustedEnode,
            };
            await services.AddMainnetNodeAsync(config, NullLoggerFactory.Instance);
            return services.BuildServiceProvider();
        }

        private static async Task AcceptAndCloseAsync(TcpListener listener, CancellationToken ct, Action onAccept)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(ct);
                    onAccept();
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
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
