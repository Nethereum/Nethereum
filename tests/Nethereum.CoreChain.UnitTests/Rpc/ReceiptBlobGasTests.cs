using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Rpc;
using Nethereum.EVM;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class ReceiptBlobGasTests
    {
        private const long CancunChainId = 987001;
        private const long PragueChainId = 987002;
        private const string ToAddress = "0x000000000000000000000000000000000000dead";

        static ReceiptBlobGasTests()
        {
            ChainActivationsRegistry.Instance.Register(CancunChainId, new FixedChainActivations(HardforkName.Cancun));
            ChainActivationsRegistry.Instance.Register(PragueChainId, new FixedChainActivations(HardforkName.Prague));
        }

        private static Transaction4844 Blob(int blobCount)
        {
            var hashes = new List<byte[]>();
            for (int i = 0; i < blobCount; i++) hashes.Add(new byte[32]);
            return new Transaction4844(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(3UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(),
                maxFeePerBlobGas: new EvmUInt256(1000UL), blobVersionedHashes: hashes,
                new Signature(new byte[32], new byte[32], new byte[] { 0 }));
        }

        private static BlockHeader HeaderWithExcess(long excessBlobGas) => new BlockHeader
        {
            BlockNumber = 20_000_000,
            Timestamp = 1_700_000_000,
            ExcessBlobGas = excessBlobGas
        };

        [Fact]
        public void Given_Tx4844_When_Resolve_Then_BlobGasUsedIsGasPerBlobTimesCount()
        {
            var (used, _) = ReceiptBlobGas.Resolve(Blob(3), HeaderWithExcess(10_000_000), CancunChainId);
            Assert.Equal(new BigInteger(3L * 131072), used);
        }

        [Fact]
        public void Given_NonBlobTx_When_Resolve_Then_BothNull()
        {
            var legacy = new LegacyTransaction(
                nonce: new byte[] { 0x01 }, gasPrice: new byte[] { 0x14 }, gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20], value: new byte[] { }, data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);

            var (used, price) = ReceiptBlobGas.Resolve(legacy, HeaderWithExcess(10_000_000), CancunChainId);

            Assert.Null(used);
            Assert.Null(price);
        }

        [Fact]
        public void Given_SameExcess_When_ResolvedUnderCancunVsPrague_Then_BlobGasPriceDiffersByFork()
        {
            var header = HeaderWithExcess(50_000_000);

            var (_, cancunPrice) = ReceiptBlobGas.Resolve(Blob(1), header, CancunChainId);
            var (_, praguePrice) = ReceiptBlobGas.Resolve(Blob(1), header, PragueChainId);

            Assert.NotNull(cancunPrice);
            Assert.NotNull(praguePrice);
            Assert.NotEqual(cancunPrice, praguePrice);
            Assert.True(praguePrice < cancunPrice);
        }

        [Fact]
        public void Given_NonBlobReceipt_When_SerializedForTheWire_Then_BlobKeysOmitted()
        {
            var receipt = new TransactionReceipt
            {
                TransactionHash = "0x" + new string('0', 64),
                BlobGasUsed = null,
                BlobGasPrice = null
            };

            var json = JsonSerializer.Serialize(receipt, CoreChainJsonContext.Default.TransactionReceipt);

            Assert.DoesNotContain("blobGasUsed", json);
            Assert.DoesNotContain("blobGasPrice", json);
        }

        [Fact]
        public void Given_ReceiptWithNullRootAndRevertReason_When_SerializedForTheWire_Then_KeysOmitted()
        {
            var receipt = new TransactionReceipt
            {
                TransactionHash = "0x" + new string('0', 64),
                Root = null,
                RevertReason = null
            };

            var json = JsonSerializer.Serialize(receipt, CoreChainJsonContext.Default.TransactionReceipt);

            Assert.DoesNotContain("\"root\"", json);
            Assert.DoesNotContain("revertReason", json);
        }

        [Fact]
        public void Given_BlobReceipt_When_SerializedForTheWire_Then_BlobKeysPresent()
        {
            var receipt = new TransactionReceipt
            {
                TransactionHash = "0x" + new string('0', 64),
                BlobGasUsed = new HexBigInteger(131072),
                BlobGasPrice = new HexBigInteger(7)
            };

            var json = JsonSerializer.Serialize(receipt, CoreChainJsonContext.Default.TransactionReceipt);

            Assert.Contains("blobGasUsed", json);
            Assert.Contains("blobGasPrice", json);
        }

        private const long UnregisteredChainId = 424242424;

        private static ChainConfig CancunConfigForUnregisteredChain() => new ChainConfig
        {
            ChainId = UnregisteredChainId,
            Activations = new FixedChainActivations(HardforkName.Cancun)
        };

        [Fact]
        public void Given_UnregisteredChainId_When_ResolvedByChainId_Then_BlobGasPriceIsNull()
        {
            var (used, price) = ReceiptBlobGas.Resolve(Blob(1), HeaderWithExcess(50_000_000), (BigInteger)UnregisteredChainId);

            Assert.NotNull(used);
            Assert.Null(price);
        }

        [Fact]
        public void Given_UnregisteredChainId_When_ResolvedByChainConfig_Then_BlobGasPriceIsResolved()
        {
            var (used, price) = ReceiptBlobGas.Resolve(Blob(1), HeaderWithExcess(50_000_000), CancunConfigForUnregisteredChain());

            Assert.NotNull(used);
            Assert.NotNull(price);
            Assert.True(price.Value > BigInteger.Zero);
        }

        [Fact]
        public void Given_NoExcessBlobGas_When_ResolvedByChainConfig_Then_BlobGasPriceIsNull()
        {
            var header = new BlockHeader { BlockNumber = 20_000_000, Timestamp = 1_700_000_000, ExcessBlobGas = null };

            var (used, price) = ReceiptBlobGas.Resolve(Blob(1), header, CancunConfigForUnregisteredChain());

            Assert.NotNull(used);
            Assert.Null(price);
        }
    }
}
