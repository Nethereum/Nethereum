using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    public class DevP2PSequencerRpcClientTests
    {
        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public DevP2PSequencerRpcClientTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private DevP2PSequencerRpcClient CreateClient()
        {
            var config = new DevP2PConfig
            {
                NetworkId = _fixture.NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };
            return new DevP2PSequencerRpcClient(_fixture.Enode, config, _fixture.GenesisHash);
        }

        [Fact]
        public async Task GetBlockHeaderAsync_Genesis_HashMatchesRpc()
        {
            await using var client = CreateClient();
            var header = await client.GetBlockHeaderAsync(BigInteger.Zero);
            Assert.NotNull(header);

            var computedHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
            Assert.Equal(_fixture.GenesisHash.ToHex(true), computedHash.ToHex(true));
            _output.WriteLine($"Genesis hash via DevP2PSequencerRpcClient: {computedHash.ToHex(true)}");
        }

        [Fact]
        public async Task GetBlockHashAsync_Genesis_MatchesGeth()
        {
            await using var client = CreateClient();
            var hash = await client.GetBlockHashAsync(BigInteger.Zero);
            Assert.NotNull(hash);
            Assert.Equal(_fixture.GenesisHash.ToHex(true), hash.ToHex(true));
        }

        [Fact]
        public async Task GetBlockWithReceiptsAsync_BlockWithTx_RootsMatchHeader()
        {
            var receipt = await _fixture.SendEtherFromSealerAsync(
                "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979",
                BigInteger.Parse("50000000000000000"));
            var includedBlock = (BigInteger)(ulong)receipt.BlockNumber.Value;

            await using var client = CreateClient();
            var data = await client.GetBlockWithReceiptsAsync(includedBlock);
            Assert.NotNull(data);
            Assert.Equal(receipt.BlockHash, data.BlockHash.ToHex(true));
            Assert.True(data.Transactions.Count >= 1, $"Expected at least 1 tx, got {data.Transactions.Count}");
            Assert.True(data.Receipts.Count >= 1, $"Expected at least 1 receipt, got {data.Receipts.Count}");

            _output.WriteLine(
                $"Block #{data.Header.BlockNumber}: {data.Transactions.Count} txs, " +
                $"{data.Receipts.Count} receipts, hash {data.BlockHash.ToHex(true).Substring(0, 18)}...");
        }

        [Fact]
        public async Task GetBlockNumberAsync_ReturnsRemoteTip()
        {
            for (int i = 0; i < 3; i++)
            {
                await _fixture.SendEtherFromSealerAsync(
                    "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979",
                    BigInteger.Parse("1000000000000000") + i);
            }

            await using var client = CreateClient();
            var remoteTip = await client.GetBlockNumberAsync();
            _output.WriteLine($"Remote tip via DevP2PSequencerRpcClient: {remoteTip}");
            Assert.True(remoteTip > 0, $"Remote tip should be > 0, got {remoteTip}");
        }
    }
}
