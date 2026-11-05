using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class BlobRpcAdmissionTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public BlobRpcAdmissionTests(ITestOutputHelper output) { _out = output; }

        private static (string Wire, byte[] Hash) NetworkFormBlobTransaction(S6ProducerHost host, int dataBytes)
        {
            var hash = new byte[32];
            hash[0] = 0x01;
            var zero = EvmUInt256.Zero;
            var tx = new Transaction4844((EvmUInt256)host.ChainId, zero, zero, zero, (EvmUInt256)200_000, Recipient, zero, "0x" + new string((char)48, 2 * dataBytes),
                new List<AccessListItem>(), (EvmUInt256)1_000_000_000, new List<byte[]> { hash });
            tx.Sidecar = new BlobSidecar(
                new List<byte[]> { new byte[131072] },
                new List<byte[]> { new byte[48] },
                Enumerable.Range(0, 128).Select(_ => new byte[48]).ToList())
            { WrapperVersion = 1 };
            new Transaction4844Signer().SignTransaction(host.OwnerKey, tx);
            return (tx.GetRLPEncodedWithSidecar().ToHex(true), tx.Hash);
        }

        private static async Task<Nethereum.JsonRpc.Client.RpcMessages.RpcResponseMessage> SendRawAsync(S6ProducerHost host, string wire)
        {
            var client = new AppChainRpcClient(host.Node.Node, (long)host.ChainId);
            return await client.SendAsync(
                new Nethereum.JsonRpc.Client.RpcMessages.RpcRequestMessage(1, "eth_sendRawTransaction", wire));
        }

        [Fact]
        public async Task Given_AOneBlobTransactionInTheNetworkFormFromTheOwner_When_ItIsSentThroughEthSendRawTransaction_Then_ItIsAcceptedAndMined()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var (wire, hash) = NetworkFormBlobTransaction(host, 0);

            var response = await SendRawAsync(host, wire);
            await host.Node.Sequencer!.ProduceBlockAsync();

            var mined = await host.Node.Bundle.Transactions.GetByHashAsync(hash) != null;
            _out.WriteLine($"S6 wireChars={wire.Length} error={response.Error?.Message ?? "none"} mined={mined}");
            Assert.Null(response.Error);
            Assert.True(mined);
        }

        [Fact]
        public async Task Given_ABlobTransactionWhoseNetworkFormExceedsTheBlobCap_When_ItIsSentThroughEthSendRawTransaction_Then_ItIsRefusedAsOversize()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var (wire, _) = NetworkFormBlobTransaction(host, 1_000_000);

            var response = await SendRawAsync(host, wire);

            _out.WriteLine($"S6 error={response.Error?.Message ?? "none"}");
            Assert.NotNull(response.Error);
            Assert.Contains("exceeds blob cap", response.Error!.Message);
        }
    }
}
