using System.Numerics;
using System.Text.Json;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class AppChainIsolatedFaultTests
    {
        private readonly ITestOutputHelper _out;

        public AppChainIsolatedFaultTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_AnAppChainWithNoKzgBackend_When_OneTransactionFaultsTheHostAndAnotherSenderSendsATransfer_Then_TheTransferIsMinedInTheFirstBlockAndTheFaultingOneIsNever()
        {
            await using var host = await AppChainRpcHost.StartAsync(config => { config.Consensus.AllowEmptyBlocks = false; config.Consensus.BlockTimeMs = 4000; });
            var chainId = JsonDocument.Parse(await host.CallAsync("eth_chainId", "[]")).RootElement.GetProperty("result").GetString()!.HexToBigInteger(false);

            var faulting = await SendAsync(host, chainId, "0x000000000000000000000000000000000000000a", BigInteger.Zero);
            var innocent = await SendAsync(host, chainId, "0x0000000000000000000000000000000000000001", BigInteger.Zero);

            string innocentReceipt = "";
            for (var i = 0; i < 30; i++)
            {
                await Task.Delay(1000);
                innocentReceipt = await host.CallAsync("eth_getTransactionReceipt", "[\"" + innocent + "\"]");
                if (!innocentReceipt.Contains("\"result\":null")) break;
            }
            var faultingReceipt = await host.CallAsync("eth_getTransactionReceipt", "[\"" + faulting + "\"]");
            _out.WriteLine("innocent " + innocentReceipt);
            _out.WriteLine("faulting " + faultingReceipt);

            Assert.DoesNotContain("\"result\":null", innocentReceipt);
            Assert.Contains("\"blockNumber\":\"0x1\"", innocentReceipt);
            Assert.Contains("\"result\":null", faultingReceipt);
        }

        private static async Task<string> SendAsync(AppChainRpcHost host, BigInteger chainId, string to, BigInteger nonce)
        {
            var key = EthECKey.GenerateKey();
            var signed = new LegacyTransactionSigner().SignTransaction(
                key.GetPrivateKey().HexToByteArray(), chainId, to, BigInteger.Zero, nonce, 0, 500_000, "00");
            var send = await host.CallAsync("eth_sendRawTransaction", "[\"0x" + signed.EnsureHexPrefix().RemoveHexPrefix() + "\"]");
            using var doc = JsonDocument.Parse(send);
            return doc.RootElement.GetProperty("result").GetString()!;
        }
    }
}
