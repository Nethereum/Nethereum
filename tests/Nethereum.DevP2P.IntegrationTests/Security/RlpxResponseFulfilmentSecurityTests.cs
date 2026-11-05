using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxResponseFulfilmentSecurityTests
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private sealed class Session : IAsyncDisposable
        {
            public RlpxListener Listener;
            public RawRlpxPeer Attacker;
            public RlpxConnection Connection;
            public int EthOffset;

            public async ValueTask DisposeAsync()
            {
                Attacker.Dispose();
                await Listener.StopAsync();
            }
        }

        private static async Task<Session> OpenAsync(DevP2PConfig config = null)
        {
            var serverKey = EthECKey.GenerateKey();
            config ??= new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000 };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
            await attacker.ReadFrameAsync();
            var connection = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            connection.StartDispatching();
            return new Session
            {
                Listener = listener,
                Attacker = attacker,
                Connection = connection,
                EthOffset = connection.GetCapabilityOffset("eth")
            };
        }

        private static Task<int[]> RequestHeadersAsync(
            Session session, ulong requestId, RlpxResponseBound bound, Func<byte[], int[]> decode)
        {
            var request = GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage
            {
                RequestId = requestId,
                StartBlock = 1,
                Limit = 2
            });
            return session.Connection.SendRequestAsync(
                session.EthOffset + EthMessageIds.GetBlockHeaders, request,
                session.EthOffset + EthMessageIds.BlockHeaders, requestId, RequestTimeout, bound, decode);
        }

        private static byte[] Response(ulong requestId, byte[][] encodedItems)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)requestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedItems));
        }

        private static byte[][] Items(int count)
        {
            var items = new byte[count][];
            for (var i = 0; i < count; i++) items[i] = RLP.RLP.EncodeList();
            return items;
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AResponseWithinTheRequestedItemCount_When_Fulfilled_Then_ItIsDecodedAndReturned()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();

            var pending = RequestHeadersAsync(session, requestId, RlpxResponseBound.Items(2), p => new[] { p.Length });
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, Items(2)));

            var decoded = await pending.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(decoded[0] > 0);
            Assert.False(session.Connection.IsDisconnected);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AResponseWithMoreItemsThanRequested_When_Fulfilled_Then_ItIsRejectedBeforeAnyItemIsDecoded()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();
            var decoded = 0;

            var pending = RequestHeadersAsync(session, requestId, RlpxResponseBound.Items(2), p =>
            {
                Interlocked.Increment(ref decoded);
                return new int[0];
            });
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, Items(3)));

            await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.Equal(0, decoded);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_ARequestForTwoItemsAnsweredWithTwoMillionElements_When_Fulfilled_Then_TheRejectionAllocatesNoMoreThanReceivingTheSameBytes()
        {
            var one = new byte[] { 0x01 };
            var items = new byte[2_000_000][];
            for (var i = 0; i < items.Length; i++) items[i] = one;

            var received = await AllocatedBytesAsync(async (session, requestId) =>
            {
                await session.Attacker.SendFrameAsync(
                    session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, items));
            }, RlpxResponseBound.Items(items.Length), expectRejection: false);

            var rejected = await AllocatedBytesAsync(async (session, requestId) =>
            {
                await session.Attacker.SendFrameAsync(
                    session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, items));
            }, RlpxResponseBound.Items(2), expectRejection: true);

            var payloadBytes = items.Length + 8;
            Assert.True(rejected <= received + payloadBytes,
                $"rejecting a {payloadBytes} byte over-count response allocated {rejected} bytes, receiving the same bytes allocated {received}");
        }

        private static async Task<long> AllocatedBytesAsync(
            Func<Session, ulong, Task> respond, RlpxResponseBound bound, bool expectRejection)
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();
            var pending = RequestHeadersAsync(session, requestId, bound, p => new int[0]);
            await session.Attacker.ReadFrameAsync();
            GC.Collect();
            var before = GC.GetTotalAllocatedBytes(true);
            await respond(session, requestId);
            if (expectRejection)
                await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(20)));
            else
                await pending.WaitAsync(TimeSpan.FromSeconds(20));
            return GC.GetTotalAllocatedBytes(true) - before;
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AResponseCodeThatDoesNotMatchTheRequestKind_When_Fulfilled_Then_ItIsRejectedAndTheConnectionIsDropped()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();
            var decoded = 0;

            var pending = RequestHeadersAsync(session, requestId, RlpxResponseBound.Items(2), p =>
            {
                Interlocked.Increment(ref decoded);
                return new int[0];
            });
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockBodies, Response(requestId, Items(1)));

            await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.Equal(0, decoded);
            Assert.True(await WaitUntilAsync(() => session.Connection.IsDisconnected));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AResponseWithAnUnknownRequestId_When_Received_Then_TheConnectionIsDroppedWithoutDecode()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();
            var decoded = 0;
            var pushed = 0;
            session.Connection.PushMessageReceived += (_, __) => Interlocked.Increment(ref pushed);

            var pending = RequestHeadersAsync(session, requestId, RlpxResponseBound.Items(2), p =>
            {
                Interlocked.Increment(ref decoded);
                return new int[0];
            });
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId + 1000, Items(1)));

            await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.Equal(0, decoded);
            Assert.Equal(0, pushed);
            Assert.True(await WaitUntilAsync(() => session.Connection.IsDisconnected));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_ARequestAlreadyFulfilled_When_ASecondResponseWithTheSameIdArrives_Then_TheConnectionIsDropped()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();

            var pending = RequestHeadersAsync(session, requestId, RlpxResponseBound.Items(2), p => new[] { p.Length });
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, Items(1)));
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, Items(1)));

            Assert.True(await WaitUntilAsync(() => session.Connection.IsDisconnected));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_ARequestThatTimedOut_When_TheLateResponseArrives_Then_ItIsDroppedWithoutDecodeAndTheConnectionStays()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();
            var decoded = 0;
            var request = GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage { RequestId = requestId, StartBlock = 1, Limit = 2 });

            var pending = session.Connection.SendRequestAsync(
                session.EthOffset + EthMessageIds.GetBlockHeaders, request,
                session.EthOffset + EthMessageIds.BlockHeaders, requestId, TimeSpan.FromMilliseconds(300),
                RlpxResponseBound.Items(2), p =>
                {
                    Interlocked.Increment(ref decoded);
                    return new int[0];
                });
            await session.Attacker.ReadFrameAsync();
            await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));

            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, Items(1)));
            var next = session.Connection.NextRequestId();
            var followUp = RequestHeadersAsync(session, next, RlpxResponseBound.Items(2), p => new[] { p.Length });
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(next, Items(1)));
            await followUp.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(0, decoded);
            Assert.False(session.Connection.IsDisconnected);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_ARequestThatTimedOut_When_TheLateResponseArrivesAfterTheTrackingWindow_Then_TheConnectionIsDropped()
        {
            await using var session = await OpenAsync(new DevP2PConfig
            {
                ClientId = "Nethereum/security",
                HandshakeTimeoutMs = 5000,
                AbandonedRequestTrackingMs = 1500
            });
            var within = session.Connection.NextRequestId();
            var after = session.Connection.NextRequestId();

            var first = RequestExpectingTimeoutAsync(session, within);
            var second = RequestExpectingTimeoutAsync(session, after);
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.ReadFrameAsync();
            await Assert.ThrowsAsync<TimeoutException>(() => first.WaitAsync(TimeSpan.FromSeconds(10)));
            await Assert.ThrowsAsync<TimeoutException>(() => second.WaitAsync(TimeSpan.FromSeconds(10)));

            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(within, Items(1)));
            await Task.Delay(300);
            Assert.False(session.Connection.IsDisconnected);

            await Task.Delay(TimeSpan.FromMilliseconds(1800));
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(after, Items(1)));

            Assert.True(await WaitUntilAsync(() => session.Connection.IsDisconnected));
        }

        private static Task<int[]> RequestExpectingTimeoutAsync(Session session, ulong requestId)
        {
            var request = GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage { RequestId = requestId, StartBlock = 1, Limit = 2 });
            return session.Connection.SendRequestAsync(
                session.EthOffset + EthMessageIds.GetBlockHeaders, request,
                session.EthOffset + EthMessageIds.BlockHeaders, requestId, TimeSpan.FromMilliseconds(300),
                RlpxResponseBound.Items(2), p => new[] { p.Length });
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AContentBytesBound_When_TheResponseListHoldsMoreBytes_Then_ItIsRejectedBeforeDecode()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();
            var decoded = 0;

            var pending = RequestHeadersAsync(session, requestId, RlpxResponseBound.ContentBytes(10), p =>
            {
                Interlocked.Increment(ref decoded);
                return new int[0];
            });
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, Response(requestId, Items(11)));

            await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.Equal(0, decoded);
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AResponseWithoutAParsableRequestId_When_Received_Then_TheConnectionIsDropped()
        {
            await using var session = await OpenAsync();
            var requestId = session.Connection.NextRequestId();

            var pending = RequestHeadersAsync(session, requestId, RlpxResponseBound.Items(2), p => new int[0]);
            await session.Attacker.ReadFrameAsync();
            await session.Attacker.SendFrameAsync(session.EthOffset + EthMessageIds.BlockHeaders, new byte[] { 0xc1, 0xc0 });

            await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(await WaitUntilAsync(() => session.Connection.IsDisconnected));
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(25);
            }
            return condition();
        }
    }
}
