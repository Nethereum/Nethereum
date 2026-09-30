using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Server.Endpoints;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class LiveBlockFinalityEndpointTests
    {
        [Fact]
        public async Task Given_ANodeWithNoFinalitySource_When_ABlockHeaderIsRead_Then_ItIsNotReportedAsFinalized()
        {
            await using var host = await StartAsync(withTracker: false);

            var finality = await host.BlockFinalityAsync(1);

            Assert.Equal("unknown", finality);
        }

        [Fact]
        public async Task Given_ABlockAtOrBelowTheFinalisedTip_When_ItsFinalityIsRead_Then_ItIsFinalized()
        {
            await using var host = await StartAsync(withTracker: true);
            await host.Tracker.MarkAsFinalizedAsync(2);

            Assert.Equal("finalized", await host.BlockFinalityAsync(1));
            Assert.Equal("finalized", await host.BlockFinalityAsync(2));
        }

        [Fact]
        public async Task Given_ABlockAboveTheFinalisedTip_When_ItsFinalityIsRead_Then_ItIsSoft()
        {
            await using var host = await StartAsync(withTracker: true);
            await host.Tracker.MarkAsFinalizedAsync(1);
            await host.Tracker.MarkAsSoftAsync(3);

            Assert.Equal("soft", await host.BlockFinalityAsync(3));
        }

        [Fact]
        public async Task Given_AnUnmarkedBlock_When_ItsFinalityIsRead_Then_ItIsUnknownRatherThanFinalized()
        {
            await using var host = await StartAsync(withTracker: true);

            Assert.Equal("unknown", await host.BlockFinalityAsync(3));
        }

        [Fact]
        public async Task Given_TheSameBlock_When_AskedViaTheHeaderAndViaTheFinalityEndpoint_Then_BothAnswerTheSame()
        {
            await using var host = await StartAsync(withTracker: true);
            await host.Tracker.MarkAsFinalizedAsync(1);

            foreach (var blockNumber in new long[] { 1, 2, 3 })
            {
                Assert.Equal(
                    await host.BlockFinalityAsync(blockNumber),
                    await host.FinalityStatusAsync(blockNumber));
            }
        }

        private static async Task<EndpointHost> StartAsync(bool withTracker)
        {
            var blocks = new InMemoryBlockStore();
            for (long i = 0; i <= 3; i++)
            {
                var header = BuildHeader(i);
                await blocks.SaveAsync(header, BlockHashCalculator.ForHeader(header));
            }

            var tracker = new InMemoryFinalityTracker();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Services.AddSingleton<IBlockStore>(blocks);
            if (withTracker) builder.Services.AddSingleton<IFinalityTracker>(tracker);

            var port = FindFreePort();
            var app = builder.Build();
            app.MapLiveBlockEndpoints();
            _ = app.RunAsync($"http://127.0.0.1:{port}");
            await Task.Delay(300);

            return new EndpointHost(app, port, tracker);
        }

        private static BlockHeader BuildHeader(long blockNumber) => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            LogsBloom = new byte[256],
            Difficulty = EvmUInt256.One,
            BlockNumber = blockNumber,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 1700000000,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8]
        };

        private static int FindFreePort()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private sealed class EndpointHost : IAsyncDisposable
        {
            private readonly WebApplication _app;
            private readonly HttpClient _client;

            public EndpointHost(WebApplication app, int port, InMemoryFinalityTracker tracker)
            {
                _app = app;
                Tracker = tracker;
                _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            }

            public InMemoryFinalityTracker Tracker { get; }

            public async Task<string> BlockFinalityAsync(long blockNumber) =>
                await ReadPropertyAsync($"/blocks/{blockNumber}", "finality");

            public async Task<string> FinalityStatusAsync(long blockNumber) =>
                await ReadPropertyAsync($"/finality/{blockNumber}", "status");

            private async Task<string> ReadPropertyAsync(string path, string property)
            {
                using var document = JsonDocument.Parse(await _client.GetStringAsync(path));
                return document.RootElement.GetProperty(property).GetString();
            }

            public async ValueTask DisposeAsync()
            {
                _client.Dispose();
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
        }
    }
}
