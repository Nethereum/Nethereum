using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class TransactionRpcBuilderTests
    {
        private const string ToAddress = "0x000000000000000000000000000000000000dead";
        private static readonly byte[] BlockHash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        private const int TxIndex = 3;
        private static readonly BigInteger BlockNumber = 100;

        private static Signature Sig() => new Signature(new byte[32], new byte[32], new byte[] { 0 });

        private static BlockHeader HeaderWithBaseFee(ulong baseFee) => new BlockHeader { BaseFee = new EvmUInt256(baseFee) };

        [Fact]
        public void Given_LegacyTx_When_Build_Then_TypeZero_GasPriceIsOwnPrice_NoAccessList()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x14 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(1000));

            Assert.Equal(new HexBigInteger(0), rpcTx.Type);
            Assert.Equal(new HexBigInteger(20), rpcTx.GasPrice);
            Assert.Null(rpcTx.MaxFeePerGas);
            Assert.Null(rpcTx.MaxPriorityFeePerGas);
            Assert.Null(rpcTx.AccessList);
        }

        [Fact]
        public void Given_Tx2930_When_Build_Then_TypeOne_GasPriceIsOwnPrice_AccessListNeverNull()
        {
            var tx = new Transaction2930(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), gasPrice: new EvmUInt256(30UL),
                gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress, amount: new EvmUInt256(0UL),
                data: "0x", accessList: new List<AccessListItem>(), Sig());

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(1000));

            Assert.Equal(new HexBigInteger(1), rpcTx.Type);
            Assert.Equal(new HexBigInteger(30), rpcTx.GasPrice);
            Assert.NotNull(rpcTx.AccessList);
            Assert.Empty(rpcTx.AccessList);
        }

        [Fact]
        public void Given_AccessListWithBareHexAddress_When_Build_Then_AddressIsCanonicalRpcForm()
        {
            var item = new AccessListItem("DAC17F958D2ee523a2206206994597C13D831ec7", new List<byte[]> { new byte[32] });
            var tx = new Transaction2930(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), gasPrice: new EvmUInt256(30UL),
                gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress, amount: new EvmUInt256(0UL),
                data: "0x", accessList: new List<AccessListItem> { item }, Sig());

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(1000));

            Assert.Equal("0xdac17f958d2ee523a2206206994597c13d831ec7", rpcTx.AccessList[0].Address);
        }

        [Fact]
        public void Given_Tx1559_When_Build_Then_GasPriceIsEffectivePrice_MaxFeeFieldsSet()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(), Sig());
            var header = HeaderWithBaseFee(20);

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, header);

            Assert.Equal(new HexBigInteger(25), rpcTx.GasPrice);
            Assert.Equal(new HexBigInteger(100), rpcTx.MaxFeePerGas);
            Assert.Equal(new HexBigInteger(5), rpcTx.MaxPriorityFeePerGas);
            Assert.NotNull(rpcTx.AccessList);
            Assert.Equal(new HexBigInteger(1), rpcTx.ChainId);
            Assert.Equal(new HexBigInteger(0), rpcTx.YParity);
        }

        [Fact]
        public void Given_TypedTx_When_BuildWithHeaderTimestamp_Then_BlockTimestampFromHeader()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(), Sig());
            var header = new BlockHeader { BaseFee = new EvmUInt256(20UL), Timestamp = 1_700_000_123 };

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, header);

            Assert.Equal(new HexBigInteger(1_700_000_123), rpcTx.BlockTimestamp);
        }

        [Fact]
        public void Given_TypedTxWithYParityOne_When_Build_Then_YParityIsOne()
        {
            var sigV1 = new Signature(new byte[32], new byte[32], new byte[] { 1 });
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(), sigV1);

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(20));

            Assert.Equal(new HexBigInteger(1), rpcTx.YParity);
        }

        [Fact]
        public void Given_LegacyEip155Tx_When_Build_Then_ChainIdIsDecoded()
        {
            var tx = new LegacyTransactionChainId(ToAddress, new EvmUInt256(0UL), new EvmUInt256(0UL), new EvmUInt256(1UL));

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(1000));

            Assert.Equal(new HexBigInteger(1), rpcTx.ChainId);
            Assert.Null(rpcTx.YParity);
        }

        [Fact]
        public void Given_LegacyTx_When_Build_Then_NoChainIdNoYParity()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 }, gasPrice: new byte[] { 0x14 }, gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20], value: new byte[] { }, data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(1000));

            Assert.Null(rpcTx.ChainId);
            Assert.Null(rpcTx.YParity);
        }

        [Fact]
        public void Given_TypedTx_When_SerializedForTheWire_Then_ChainIdYParityBlockTimestampPresent()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(), Sig());
            var header = new BlockHeader { BaseFee = new EvmUInt256(20UL), Timestamp = 1_700_000_123 };
            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, header);

            var json = System.Text.Json.JsonSerializer.Serialize(rpcTx, CoreChainJsonContext.Default.Transaction);

            Assert.Contains("chainId", json);
            Assert.Contains("yParity", json);
            Assert.Contains("blockTimestamp", json);
        }

        [Fact]
        public void Given_LegacyTx_When_SerializedForTheWire_Then_ChainIdYParityOmitted()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 }, gasPrice: new byte[] { 0x14 }, gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20], value: new byte[] { }, data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);
            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(1000));

            var json = System.Text.Json.JsonSerializer.Serialize(rpcTx, CoreChainJsonContext.Default.Transaction);

            Assert.DoesNotContain("chainId", json);
            Assert.DoesNotContain("yParity", json);
        }

        [Fact]
        public void Given_Tx1559_When_BuildWithoutBlockHeader_Then_GasPriceFallsBackToMaxFeePerGas()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(), Sig());

            var rpcTx = TransactionRpcBuilder.Build(tx, blockHash: null, blockNumber: null, transactionIndex: null, blockHeader: null);

            Assert.Null(rpcTx.BlockHash);
            Assert.Null(rpcTx.BlockNumber);
            Assert.Null(rpcTx.TransactionIndex);
            Assert.Equal(new HexBigInteger(100), rpcTx.GasPrice);
        }

        [Fact]
        public void Given_Tx4844_When_Build_Then_BlobFieldsPopulated()
        {
            var blobHash = new byte[32];
            blobHash[0] = 0xAB;
            var tx = new Transaction4844(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(3UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(),
                maxFeePerBlobGas: new EvmUInt256(1000UL), blobVersionedHashes: new List<byte[]> { blobHash }, Sig());
            var header = HeaderWithBaseFee(50);

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, header);

            Assert.Equal(new HexBigInteger(3), rpcTx.Type);
            Assert.Equal(new HexBigInteger(53), rpcTx.GasPrice);
            Assert.Equal(new HexBigInteger(1000), rpcTx.MaxFeePerBlobGas);
            Assert.Single(rpcTx.BlobVersionedHashes);
            Assert.Equal("0xab00000000000000000000000000000000000000000000000000000000000000", rpcTx.BlobVersionedHashes[0]);
        }

        [Fact]
        public void Given_Tx7702_When_Build_Then_AuthorisationListPopulated()
        {
            var authorisation = new Authorisation7702Signed
            {
                ChainId = new EvmUInt256(1UL),
                Address = ToAddress,
                Nonce = new EvmUInt256(0UL),
                R = new byte[32],
                S = new byte[32],
                V = new byte[] { 0 }
            };
            var tx = new Transaction7702(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(10UL),
                maxFeePerGas: new EvmUInt256(200UL), gasLimit: new EvmUInt256(50000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(),
                authorisationList: new List<Authorisation7702Signed> { authorisation }, Sig());
            var header = HeaderWithBaseFee(100);

            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, header);

            Assert.Equal(new HexBigInteger(4), rpcTx.Type);
            Assert.Equal(new HexBigInteger(110), rpcTx.GasPrice);
            Assert.NotNull(rpcTx.AuthorisationList);
            Assert.Single(rpcTx.AuthorisationList);
            Assert.Equal(ToAddress, rpcTx.AuthorisationList[0].Address);
        }

        [Fact]
        public void Given_LegacyTx_When_SerializedForTheWire_Then_TypedOnlyKeysAreOmitted()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x14 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);
            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(1000));

            var json = System.Text.Json.JsonSerializer.Serialize(rpcTx, CoreChainJsonContext.Default.Transaction);

            Assert.DoesNotContain("maxFeePerGas", json);
            Assert.DoesNotContain("maxPriorityFeePerGas", json);
            Assert.DoesNotContain("accessList", json);
            Assert.DoesNotContain("authorizationList", json);
            Assert.DoesNotContain("maxFeePerBlobGas", json);
            Assert.DoesNotContain("blobVersionedHashes", json);
        }

        [Fact]
        public void Given_Tx1559_When_SerializedForTheWire_Then_FeeCapKeysPresent()
        {
            var tx = new Transaction1559(
                chainId: new EvmUInt256(1UL), nonce: new EvmUInt256(0UL), maxPriorityFeePerGas: new EvmUInt256(5UL),
                maxFeePerGas: new EvmUInt256(100UL), gasLimit: new EvmUInt256(21000UL), receiverAddress: ToAddress,
                amount: new EvmUInt256(0UL), data: "0x", accessList: new List<AccessListItem>(), Sig());
            var rpcTx = TransactionRpcBuilder.Build(tx, BlockHash, BlockNumber, TxIndex, HeaderWithBaseFee(20));

            var json = System.Text.Json.JsonSerializer.Serialize(rpcTx, CoreChainJsonContext.Default.Transaction);

            Assert.Contains("maxFeePerGas", json);
            Assert.Contains("maxPriorityFeePerGas", json);
            Assert.Contains("accessList", json);
        }
    }
}
