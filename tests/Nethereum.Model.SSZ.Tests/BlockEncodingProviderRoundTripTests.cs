using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    /// <summary>
    /// Round-trip tests for <see cref="IBlockEncodingProvider"/>. Each test
    /// runs the same object through both <see cref="RlpBlockEncodingProvider"/>
    /// and <see cref="SszBlockEncodingProvider"/> and asserts the decode side
    /// reconstructs the original. Ensures A-08 storage-layer pluggability
    /// works with either provider selected at genesis.
    /// </summary>
    public class BlockEncodingProviderRoundTripTests
    {
        public static IEnumerable<object[]> Providers => new[]
        {
            new object[] { RlpBlockEncodingProvider.Instance },
            new object[] { SszBlockEncodingProvider.Instance }
        };

        // --- Block header ---

        [Theory]
        [MemberData(nameof(Providers))]
        public void BlockHeader_RoundTrip(IBlockEncodingProvider provider)
        {
            var original = new BlockHeader
            {
                ParentHash = Bytes32(0x11),
                Coinbase = "0x0000000000000000000000000000000000000042",
                StateRoot = Bytes32(0x22),
                TransactionsHash = Bytes32(0x33),
                ReceiptHash = Bytes32(0x44),
                BlockNumber = 100,
                GasLimit = 30_000_000,
                GasUsed = 21_000,
                Timestamp = 1_700_000_000,
                ExtraData = new byte[] { 0xAB, 0xCD },
                MixHash = Bytes32(0x55),
                BaseFee = new EvmUInt256(7UL),
                WithdrawalsRoot = Bytes32(0x66),
                ParentBeaconBlockRoot = Bytes32(0x77),
                RequestsHash = Bytes32(0x88)
            };

            var encoded = provider.EncodeBlockHeader(original);
            Assert.NotNull(encoded);
            Assert.NotEmpty(encoded);

            var decoded = provider.DecodeBlockHeader(encoded);
            Assert.NotNull(decoded);
            Assert.Equal(original.ParentHash, decoded.ParentHash);
            Assert.Equal(original.StateRoot, decoded.StateRoot);
            Assert.Equal(original.BlockNumber, decoded.BlockNumber);
            Assert.Equal(original.GasLimit, decoded.GasLimit);
            Assert.Equal(original.Timestamp, decoded.Timestamp);
        }

        // --- Log ---

        [Theory]
        [MemberData(nameof(Providers))]
        public void Log_RoundTrip(IBlockEncodingProvider provider)
        {
            var original = new Log
            {
                Address = "0x1111111111111111111111111111111111111111",
                Topics = new List<byte[]> { Bytes32(0xAA), Bytes32(0xBB) },
                Data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }
            };

            var encoded = provider.EncodeLog(original);
            var decoded = provider.DecodeLog(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original.Address.ToLowerInvariant(), decoded.Address.ToLowerInvariant());
            Assert.Equal(original.Topics.Count, decoded.Topics.Count);
            Assert.Equal(original.Data, decoded.Data);
        }

        // --- Receipt ---

        [Fact]
        public void Receipt_BasicStatus_Rlp_RoundTrip()
        {
            var original = Receipt.CreateStatusReceipt(
                success: true,
                cumulativeGasUsed: new EvmUInt256(21000UL),
                bloom: new byte[256],
                logs: new List<Log>());

            var encoded = RlpBlockEncodingProvider.Instance.EncodeReceipt(original);
            var decoded = RlpBlockEncodingProvider.Instance.DecodeReceipt(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original.HasSucceeded, decoded.HasSucceeded);
            Assert.Equal(original.CumulativeGasUsed, decoded.CumulativeGasUsed);
        }

        [Fact]
        public void Receipt_BasicStatus_Ssz_RoundTrip_Basic()
        {
            var original = new Receipt
            {
                PostStateOrStatus = new byte[] { 1 },
                CumulativeGasUsed = new EvmUInt256(21000UL),
                Logs = new List<Log>(),
                From = "0x1111111111111111111111111111111111111111"
            };

            var encoded = SszBlockEncodingProvider.Instance.EncodeReceipt(original);
            var decoded = SszBlockEncodingProvider.Instance.DecodeReceipt(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(true, decoded.HasSucceeded);
            Assert.Equal(original.CumulativeGasUsed, decoded.CumulativeGasUsed);
            Assert.Equal(original.From.ToLowerInvariant(), decoded.From.ToLowerInvariant());
            Assert.Null(decoded.ContractAddress);
            Assert.Null(decoded.Authorities);
        }

        [Fact]
        public void Receipt_Create_Ssz_RoundTrip()
        {
            var original = new Receipt
            {
                PostStateOrStatus = new byte[] { 1 },
                CumulativeGasUsed = new EvmUInt256(500000UL),
                Logs = new List<Log>(),
                From = "0x2222222222222222222222222222222222222222",
                ContractAddress = "0x3333333333333333333333333333333333333333"
            };

            var encoded = SszBlockEncodingProvider.Instance.EncodeReceipt(original);
            var decoded = SszBlockEncodingProvider.Instance.DecodeReceipt(encoded);

            Assert.Equal(original.From.ToLowerInvariant(), decoded.From.ToLowerInvariant());
            Assert.Equal(original.ContractAddress.ToLowerInvariant(), decoded.ContractAddress.ToLowerInvariant());
        }

        [Fact]
        public void Receipt_SetCode_Ssz_RoundTrip()
        {
            var original = new Receipt
            {
                PostStateOrStatus = new byte[] { 1 },
                CumulativeGasUsed = new EvmUInt256(60000UL),
                Logs = new List<Log>(),
                From = "0x4444444444444444444444444444444444444444",
                Authorities = new List<string>
                {
                    "0x5555555555555555555555555555555555555555",
                    "0x6666666666666666666666666666666666666666"
                }
            };

            var encoded = SszBlockEncodingProvider.Instance.EncodeReceipt(original);
            var decoded = SszBlockEncodingProvider.Instance.DecodeReceipt(encoded);

            Assert.Equal(original.From.ToLowerInvariant(), decoded.From.ToLowerInvariant());
            Assert.NotNull(decoded.Authorities);
            Assert.Equal(2, decoded.Authorities.Count);
            Assert.Equal(original.Authorities[0].ToLowerInvariant(), decoded.Authorities[0].ToLowerInvariant());
            Assert.Equal(original.Authorities[1].ToLowerInvariant(), decoded.Authorities[1].ToLowerInvariant());
        }

        // --- Account ---

        [Fact]
        public void Account_Rlp_RoundTrip()
        {
            var original = new Account
            {
                Nonce = 7,
                Balance = System.Numerics.BigInteger.Parse("1000000000000000000"),
                StateRoot = Bytes32(0xAA),
                CodeHash = Bytes32(0xBB)
            };

            var encoded = RlpBlockEncodingProvider.Instance.EncodeAccount(original);
            var decoded = RlpBlockEncodingProvider.Instance.DecodeAccount(encoded);

            Assert.NotNull(decoded);
            Assert.Equal(original.Nonce, decoded.Nonce);
            Assert.Equal(original.Balance, decoded.Balance);
        }

        [Fact]
        public void Account_Ssz_Throws_PerEip7864()
        {
            var account = new Account { Nonce = 1, Balance = 0 };
            Assert.Throws<NotImplementedException>(
                () => SszBlockEncodingProvider.Instance.EncodeAccount(account));
            Assert.Throws<NotImplementedException>(
                () => SszBlockEncodingProvider.Instance.DecodeAccount(new byte[] { 1, 2, 3 }));
        }

        // --- Withdrawal ---

        [Theory]
        [MemberData(nameof(Providers))]
        public void Withdrawal_Encodes_NonEmpty(IBlockEncodingProvider provider)
        {
            var address = new byte[20];
            address[19] = 0xEE;

            var encoded = provider.EncodeWithdrawal(
                index: 42,
                validatorIndex: 777,
                address: address,
                amountInGwei: 1_000_000_000UL);

            Assert.NotNull(encoded);
            Assert.NotEmpty(encoded);
        }

        // --- Transaction round-trip (A-12) ---

        [Fact]
        public void Transaction1559_Ssz_RoundTrip_WithSignature()
        {
            var signature = new Signature(
                r: Bytes32(0x01),
                s: Bytes32(0x02),
                v: new byte[] { 0x1B });

            var original = new Transaction1559(
                chainId: 1,
                nonce: 42,
                maxPriorityFeePerGas: 2_000_000_000,
                maxFeePerGas: 30_000_000_000,
                gasLimit: 21_000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 1_000_000_000_000_000_000UL,
                data: "",
                accessList: new List<AccessListItem>(),
                signature: signature);

            var encoded = SszBlockEncodingProvider.Instance.EncodeTransaction(original);
            var decoded = SszBlockEncodingProvider.Instance.DecodeTransaction(encoded);

            Assert.NotNull(decoded);
            var decoded1559 = Assert.IsType<Transaction1559>(decoded);
            Assert.Equal(original.ChainId, decoded1559.ChainId);
            Assert.Equal(original.Nonce, decoded1559.Nonce);
            Assert.Equal(original.GasLimit, decoded1559.GasLimit);
            Assert.Equal(original.ReceiverAddress.ToLowerInvariant(), decoded1559.ReceiverAddress.ToLowerInvariant());
            Assert.Equal(original.Amount, decoded1559.Amount);
            Assert.NotNull(decoded1559.Signature);
            Assert.Equal(signature.R, decoded1559.Signature.R);
            Assert.Equal(signature.S, decoded1559.Signature.S);
            Assert.Equal(signature.V, decoded1559.Signature.V);
        }

        [Fact]
        public void Transaction1559_Ssz_SelectorIsBasicForNonCreate()
        {
            var tx = new Transaction1559(
                chainId: 1, nonce: 1, maxPriorityFeePerGas: 1, maxFeePerGas: 1,
                gasLimit: 21_000, receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 0, data: "", accessList: new List<AccessListItem>());

            var encoded = SszBlockEncodingProvider.Instance.EncodeTransaction(tx);

            Assert.Equal(SszTransactionEncoder.SelectorRlpBasic, encoded[0]);
        }

        [Fact]
        public void Transaction1559_Ssz_SelectorIsCreateForContractDeploy()
        {
            var tx = new Transaction1559(
                chainId: 1, nonce: 1, maxPriorityFeePerGas: 1, maxFeePerGas: 1,
                gasLimit: 100_000, receiverAddress: null,
                amount: 0, data: "0x6001", accessList: new List<AccessListItem>());

            var encoded = SszBlockEncodingProvider.Instance.EncodeTransaction(tx);

            Assert.Equal(SszTransactionEncoder.SelectorRlpCreate, encoded[0]);
        }

        [Fact]
        public void Transaction_Ssz_ParseExtractsSelectorPayloadSignature()
        {
            var sig = new Signature(Bytes32(0xAA), Bytes32(0xBB), new byte[] { 0x1C });
            var tx = new Transaction1559(
                chainId: 1, nonce: 7, maxPriorityFeePerGas: 1, maxFeePerGas: 1,
                gasLimit: 21_000, receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 0, data: "", accessList: new List<AccessListItem>(),
                signature: sig);

            var encoded = SszBlockEncodingProvider.Instance.EncodeTransaction(tx);
            SszTransactionEncoder.Current.ParseTransaction(encoded,
                out var selector, out var payload, out var sigBytes);

            Assert.Equal(SszTransactionEncoder.SelectorRlpBasic, selector);
            Assert.NotEmpty(payload);
            Assert.NotEmpty(sigBytes);

            var unpacked = SszTransactionEncoder.UnpackSignature(sigBytes);
            Assert.NotNull(unpacked);
            Assert.Equal(sig.R, unpacked.R);
            Assert.Equal(sig.S, unpacked.S);
        }

        [Fact]
        public void Transaction_Ssz_LegacySelectorThrows()
        {
            // Craft a record with selector 0x01 (legacy replayable basic) which
            // SSZ encoder does not yet support.
            var crafted = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            Assert.Throws<System.NotImplementedException>(
                () => SszBlockEncodingProvider.Instance.DecodeTransaction(crafted));
        }

        // --- Cross-provider divergence smoke test ---

        [Fact]
        public void BlockHeader_RlpAndSsz_ProduceDifferentBytes()
        {
            var header = new BlockHeader
            {
                ParentHash = Bytes32(0x01),
                Coinbase = "0x0000000000000000000000000000000000000042",
                StateRoot = Bytes32(0x02),
                TransactionsHash = Bytes32(0x03),
                ReceiptHash = Bytes32(0x04),
                BlockNumber = 1,
                GasLimit = 30_000_000,
                GasUsed = 0,
                Timestamp = 100,
                MixHash = Bytes32(0x05),
                BaseFee = new EvmUInt256(1UL)
            };

            var rlp = RlpBlockEncodingProvider.Instance.EncodeBlockHeader(header);
            var ssz = SszBlockEncodingProvider.Instance.EncodeBlockHeader(header);

            Assert.NotEqual(rlp, ssz);
        }

        private static byte[] Bytes32(byte fill)
        {
            var b = new byte[32];
            for (var i = 0; i < 32; i++) b[i] = fill;
            return b;
        }
    }
}
