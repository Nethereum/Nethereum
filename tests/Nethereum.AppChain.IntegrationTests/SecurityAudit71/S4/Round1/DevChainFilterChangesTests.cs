using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1
{
    [Collection("Sequential")]
    public class DevChainFilterChangesTests
    {
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string Emitter = "0x1000000000000000000000000000000000000002";
        private const string EmitterCode = "0x60006000a000";

        private static JsonElement Result(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("result").Clone();
        }

        private const string SenderKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private static async Task EmitAsync(DevChainRpcHost host)
        {
            var nonceBody = await host.CallAsync("eth_getTransactionCount", "[\"" + Sender + "\",\"latest\"]");
            var nonce = Result(nonceBody).GetString()!.HexToBigInteger(false);
            var signed = new LegacyTransactionSigner().SignTransaction(
                SenderKey.HexToByteArray(), new BigInteger(31337), Emitter, BigInteger.Zero, nonce, 10_000_000_000, 100_000, "");
            var body = await host.CallAsync("eth_sendRawTransaction", "[\"0x" + signed.EnsureHexPrefix().RemoveHexPrefix() + "\"]");
            Assert.True(JsonDocument.Parse(body).RootElement.TryGetProperty("result", out _), body);
        }

        [Fact]
        [Trait("Finding", "S4-R1-13")]
        public async Task Given_AFilterInstalledFromGenesisOnAChainWithLogs_When_ItIsPolled_Then_OnlyLogsSinceTheLastPollAreReturned()
        {
            await using var host = await DevChainRpcHost.StartAsync();
            await host.CallAsync("hardhat_setCode", "[\"" + Emitter + "\",\"" + EmitterCode + "\"]");
            await EmitAsync(host);
            await EmitAsync(host);
            var created = await host.CallAsync("eth_newFilter", "[{\"fromBlock\":\"0x0\",\"address\":\"" + Emitter + "\"}]");
            var filterId = Result(created).GetString();

            var firstPoll = Result(await host.CallAsync("eth_getFilterChanges", "[\"" + filterId + "\"]"));
            await EmitAsync(host);
            var secondPoll = Result(await host.CallAsync("eth_getFilterChanges", "[\"" + filterId + "\"]"));
            var thirdPoll = Result(await host.CallAsync("eth_getFilterChanges", "[\"" + filterId + "\"]"));

            Assert.Equal(0, firstPoll.GetArrayLength());
            Assert.Equal(1, secondPoll.GetArrayLength());
            Assert.Equal(0, thirdPoll.GetArrayLength());
        }

        [Fact]
        [Trait("Finding", "S4-R1-13")]
        public async Task Given_ALogFilterWhoseNewLogsExceedTheResultCap_When_ItIsPolled_Then_TheResultCapApplies()
        {
            await using var host = await DevChainRpcHost.StartAsync(config => config.Node.Rpc.MaxLogResults = 1);
            await host.CallAsync("hardhat_setCode", "[\"" + Emitter + "\",\"" + EmitterCode + "\"]");
            var created = await host.CallAsync("eth_newFilter", "[{\"address\":\"" + Emitter + "\"}]");
            var filterId = Result(created).GetString();
            await EmitAsync(host);
            await EmitAsync(host);

            var body = await host.CallAsync("eth_getFilterChanges", "[\"" + filterId + "\"]");

            using var document = JsonDocument.Parse(body);
            Assert.Equal(-32000, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }

        [Fact]
        [Trait("Finding", "S4-R1-14")]
        public async Task Given_TwoFilters_When_TheyAreInstalled_Then_TheirIdsAreRandom128BitHexNotACounter()
        {
            await using var host = await DevChainRpcHost.StartAsync();

            var first = Result(await host.CallAsync("eth_newBlockFilter", "[]")).GetString()!;
            var second = Result(await host.CallAsync("eth_newBlockFilter", "[]")).GetString()!;

            Assert.NotEqual(first, second);
            Assert.StartsWith("0x", first);
            Assert.InRange(first.Length, 2 + 24, 2 + 32);
            Assert.InRange(second.Length, 2 + 24, 2 + 32);
        }
    }
}
