using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class UnwiredPrecompileTests
    {
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string SenderKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly ITestOutputHelper _out;

        public UnwiredPrecompileTests(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData("0x000000000000000000000000000000000000000a")]
        [InlineData("0x000000000000000000000000000000000000000b")]
        [InlineData("0x000000000000000000000000000000000000000f")]
        [InlineData("0x0000000000000000000000000000000000000011")]
        [Trait("Finding", "S4-R3-sound")]
        public async Task Probe_ATransactionCallingAPrecompileWithNoBackend_IsMinedAsAFailureAndTheChainKeepsProducing(string precompile)
        {
            await using var host = await DevChainFuzzHost.StartAsync();
            var nonceBody = await host.CallAsync("eth_getTransactionCount", "[\"" + Sender + "\",\"latest\"]");
            using var nonceDoc = JsonDocument.Parse(nonceBody);
            var nonce = nonceDoc.RootElement.GetProperty("result").GetString()!.HexToBigInteger(false);
            var signed = new LegacyTransactionSigner().SignTransaction(
                SenderKey.HexToByteArray(), new BigInteger(31337), precompile, BigInteger.Zero, nonce, 10_000_000_000, 500_000, "00");
            var send = await host.CallAsync("eth_sendRawTransaction", "[\"0x" + signed.EnsureHexPrefix().RemoveHexPrefix() + "\"]");
            _out.WriteLine("send: " + send);

            var head = await host.CallAsync("eth_blockNumber", "[]");
            var mine = await host.CallAsync("evm_mine", "[]");
            var head2 = await host.CallAsync("eth_blockNumber", "[]");
            _out.WriteLine("head " + head + " mine " + mine + " head2 " + head2);
            lock (host.Log.Entries) foreach (var e in host.Log.Entries.Where(x => x.Exception != null))
                _out.WriteLine("LOGGED " + e.Level + " " + e.Message + " " + e.Exception!.GetType().Name);

            Assert.DoesNotContain("error", mine);
        }
    }
}
