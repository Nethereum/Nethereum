using System.Numerics;
using System.Text.Json;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class AppChainUnwiredPrecompileTests
    {
        private readonly ITestOutputHelper _out;

        public AppChainUnwiredPrecompileTests(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData("0x0000000000000000000000000000000000000001")]
        [InlineData("0x000000000000000000000000000000000000000f")]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_AnUnfundedZeroGasTransactionCallingAPrecompile_When_TheSequencerIncludesIt_Then_BlockProductionContinues(string precompile)
        {
            await using var host = await AppChainRpcHost.StartAsync(config => config.Consensus.AllowEmptyBlocks = true);
            var key = EthECKey.GenerateKey();
            var from = key.GetPublicAddress();
            var chainId = JsonDocument.Parse(await host.CallAsync("eth_chainId", "[]")).RootElement.GetProperty("result").GetString()!.HexToBigInteger(false);
            var signed = new LegacyTransactionSigner().SignTransaction(
                key.GetPrivateKey().HexToByteArray(), chainId, precompile, BigInteger.Zero, BigInteger.Zero, 0, 500_000, "00");
            var send = await host.CallAsync("eth_sendRawTransaction", "[\"0x" + signed.EnsureHexPrefix().RemoveHexPrefix() + "\"]");
            _out.WriteLine("send from " + from + ": " + send);
            await Task.Delay(15000);
            var head1 = await host.CallAsync("eth_blockNumber", "[]");
            await Task.Delay(10000);
            var head2 = await host.CallAsync("eth_blockNumber", "[]");
            _out.WriteLine("heads " + head1 + " then " + head2);
            Assert.NotEqual(head1, head2);
            _ = from;
        }
    }
}
