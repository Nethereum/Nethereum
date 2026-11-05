using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.IntegrationTests.Security;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.SecurityAudit71.S2.PartB
{
    public class S2LeaseReleaseTests
    {
        private sealed class GatedSnapHandler : ISnapRequestHandler
        {
            public readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => Task.FromResult(new AccountRangeMessage());

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                Entered.TrySetResult(true);
                await Gate.Task.WaitAsync(ct);
                return new StorageRangesMessage();
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage());

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage());

            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => Task.FromResult(new Nethereum.Model.P2P.Snap.BlockAccessListsMessage());
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task S2_DB2_Given_AGetStorageRangesWhoseHandlerIsStillRunning_When_TheRequestHasBeenDecoded_Then_ItsDecodeLeaseIsAlreadyReleased()
        {
            var limiter = new InFlightDecodeLimiter(8L * 1024 * 1024);
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, new DevP2PConfig
            {
                ClientId = "Nethereum/lease-release",
                HandshakeTimeoutMs = 5000,
                DecodeLimiter = limiter
            });
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var handler = new GatedSnapHandler();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                using var peer = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
                var hello = RawRlpxPeer.HelloFor(peer.Key);
                hello.Capabilities.Add(new P2PCapability { Name = "snap", Version = 1 });
                await peer.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
                await peer.ReadFrameAsync();
                var connection = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var snap = new Snap1Handler(connection, handler);
                var payload = GetStorageRangesMessageEncoder.Encode(new GetStorageRangesMessage
                {
                    RequestId = 1,
                    AccountHashes = new System.Collections.Generic.List<byte[]> { new byte[32] },
                    ResponseBytes = 1
                });
                var lease = await limiter.AcquireAsync(payload.Length);
                Assert.True(limiter.InFlightBytes > 0);

                var handling = snap.HandleSnapMessageAsync(SnapMessageIds.GetStorageRanges, payload, lease, cts.Token);
                await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(0, limiter.InFlightBytes);

                handler.Gate.TrySetResult(true);
                try { await handling; } catch { }
            }
            finally
            {
                handler.Gate.TrySetResult(true);
                await listener.StopAsync();
            }
        }
    }
}
