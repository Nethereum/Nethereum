using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [CollectionDefinition("NoGcRegionExclusive", DisableParallelization = true)]
    public class NoGcRegionExclusiveCollection { }

    [Collection("NoGcRegionExclusive")]
    public class StaticPeerConnectorSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_PeerAnsweringTheAuthWithAnOutOfRangeAck_When_ConnectFails_Then_TheSocketIsClosedRightAwayWithNoGcAbleToFinaliseIt()
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            var serverKey = EthECKey.GenerateKey();
            var observedClose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverSide = Task.Run(async () =>
            {
                using var client = await tcpListener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                var buffer = new byte[4096];
                await stream.ReadAsync(buffer);
                await stream.WriteAsync(new byte[] { 0x00, 0x01, 0x00 });
                try
                {
                    var read = await stream.ReadAsync(buffer);
                    observedClose.TrySetResult(read == 0);
                }
                catch (System.IO.IOException)
                {
                    observedClose.TrySetResult(true);
                }
            });
            var noGc = false;
            try { noGc = GC.TryStartNoGCRegion(64 * 1024 * 1024); } catch (InvalidOperationException) { }
            try
            {
                var connector = new StaticPeerConnector(EthECKey.GenerateKey(), new DevP2PConfig { HandshakeTimeoutMs = 3000 });
                var enode = $"enode://{Convert.ToHexString(serverKey.GetPubKeyNoPrefix()).ToLowerInvariant()}@127.0.0.1:{port}";

                var thrown = await Assert.ThrowsAnyAsync<Exception>(() => connector.ConnectAsync(enode));
                Assert.True(thrown is System.Security.Cryptography.CryptographicException, thrown.ToString());

                var closed = await observedClose.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(closed);
            }
            finally
            {
                if (noGc && System.Runtime.GCSettings.LatencyMode == System.Runtime.GCLatencyMode.NoGCRegion)
                    GC.EndNoGCRegion();
                tcpListener.Stop();
                try { await Task.WhenAny(serverSide, Task.Delay(TimeSpan.FromSeconds(2))); } catch { }
            }
        }
    }
}
