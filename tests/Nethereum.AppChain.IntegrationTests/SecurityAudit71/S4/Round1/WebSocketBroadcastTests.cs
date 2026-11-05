using System.Net.WebSockets;
using System.Text;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Subscriptions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1
{
    public class WebSocketBroadcastTests
    {
        private sealed class ScriptedWebSocket : WebSocket
        {
            private readonly bool _neverCompletesSend;
            private WebSocketState _state = WebSocketState.Open;

            public ScriptedWebSocket(bool neverCompletesSend) { _neverCompletesSend = neverCompletesSend; }

            private int _sendCalls;

            public int SendCalls => Volatile.Read(ref _sendCalls);

            public List<string> Sent { get; } = new();

            public override WebSocketCloseStatus? CloseStatus => null;
            public override string? CloseStatusDescription => null;
            public override WebSocketState State => _state;
            public override string? SubProtocol => null;

            public override void Abort() { _state = WebSocketState.Aborted; }

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                _state = WebSocketState.Closed;
                return Task.CompletedTask;
            }

            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                _state = WebSocketState.CloseSent;
                return Task.CompletedTask;
            }

            public override void Dispose() { }

            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
                new TaskCompletionSource<WebSocketReceiveResult>().Task;

            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _sendCalls);
                if (_neverCompletesSend) return new TaskCompletionSource().Task;
                lock (Sent) Sent.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
                return Task.CompletedTask;
            }
        }

        private static BlockHeader Header() => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            LogsBloom = new byte[256],
            MixHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            Nonce = new byte[8],
            BlockNumber = 1,
            GasLimit = 30_000_000,
            Timestamp = 1,
        };

        private static WebSocketRpcHandler NewHandler(SubscriptionManager manager) =>
            new WebSocketRpcHandler(manager, new RpcHandlerRegistry(), new RpcContext((Func<IChainNode>)(() => null!), 1, null!));

        [Fact]
        [Trait("Finding", "S4-R1-08")]
        public async Task Given_OneSubscriberThatNeverReads_When_ABlockIsBroadcast_Then_TheBroadcastStillCompletesAndTheOtherSubscribersAreServed()
        {
            var manager = new SubscriptionManager();
            var handler = NewHandler(manager);
            var stuck = new ScriptedWebSocket(neverCompletesSend: true);
            var healthy = new ScriptedWebSocket(neverCompletesSend: false);
            handler.Connections["stuck"] = stuck;
            handler.Connections["healthy"] = healthy;
            manager.Subscribe("stuck", SubscriptionType.NewHeads);
            manager.Subscribe("healthy", SubscriptionType.NewHeads);

            var broadcast = handler.BroadcastBlockAsync(Header(), new byte[32], new List<Nethereum.CoreChain.Models.FilteredLog>());
            var finished = await Task.WhenAny(broadcast, Task.Delay(TimeSpan.FromSeconds(5))) == broadcast;

            Assert.True(finished, "one subscriber that never reads holds the block broadcast forever");
            Assert.Single(healthy.Sent);
        }

        [Fact]
        [Trait("Finding", "S4-R1-08")]
        public async Task Given_ASubscriberWhoseWriteNeverCompletes_When_TheWriteDeadlinePasses_Then_ItsConnectionIsAbortedAndForgotten()
        {
            var manager = new SubscriptionManager();
            var handler = NewHandler(manager);
            var stuck = new ScriptedWebSocket(neverCompletesSend: true);
            handler.Connections["stuck"] = stuck;
            manager.Subscribe("stuck", SubscriptionType.NewHeads);

            await handler.BroadcastBlockAsync(Header(), new byte[32], new List<Nethereum.CoreChain.Models.FilteredLog>());
            var hardCap = DateTime.UtcNow.AddSeconds(25);
            while (stuck.State != WebSocketState.Aborted && DateTime.UtcNow < hardCap)
                await Task.Delay(250);

            Assert.Equal(WebSocketState.Aborted, stuck.State);
            Assert.False(handler.Connections.ContainsKey("stuck"));
            Assert.Equal(0, manager.ActiveCount);
        }

        [Fact]
        [Trait("Finding", "S4-R1-08")]
        public async Task Given_ManyBlocksBroadcastToASubscriberThatNeverReads_When_TheyAreQueued_Then_OnlyOneSendIsEverInFlight()
        {
            var manager = new SubscriptionManager();
            var handler = NewHandler(manager);
            var stuck = new ScriptedWebSocket(neverCompletesSend: true);
            handler.Connections["stuck"] = stuck;
            manager.Subscribe("stuck", SubscriptionType.NewHeads);

            for (var block = 0; block < 50; block++)
                await handler.BroadcastBlockAsync(Header(), new byte[32], new List<Nethereum.CoreChain.Models.FilteredLog>());

            Assert.Equal(1, stuck.SendCalls);
        }
    }
}
