using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.Mempool
{
    public class MempoolAdmissionValidatorTests
    {
        private const string PrivateKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const int NodeChainId = 1;

        private static readonly BigInteger CurveOrderN = BigInteger.Parse(
            "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141",
            NumberStyles.HexNumber);

        private static readonly LegacyTransactionSigner LegacySigner = new();

        private static string SenderAddress => new EthECKey(PrivateKey).GetPublicAddress().ToLowerInvariant();

        private static MempoolAdmissionValidator Validator(int chainId = NodeChainId)
            => new MempoolAdmissionValidator(chainId);

        private static async Task<InMemoryChainStoreBundle> FundedBundleAsync(BigInteger nonce, BigInteger balance)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.State.SaveAccountAsync(SenderAddress, new Account
            {
                Nonce = (EvmUInt256)nonce,
                Balance = (EvmUInt256)balance
            });
            return bundle;
        }

        private static ISignedTransaction SignLegacy(
            BigInteger chainId, BigInteger nonce, BigInteger gasPrice, BigInteger gasLimit,
            BigInteger amount, string data = "")
        {
            var hex = LegacySigner.SignTransaction(
                PrivateKey.HexToByteArray(), chainId, Recipient, amount, nonce, gasPrice, gasLimit, data);
            return TransactionFactory.CreateTransaction(hex);
        }

        [Fact]
        public async Task Given_WellFormedFundedTransaction_When_Validated_Then_Accepted()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000_000_000_000_000);
            var tx = SignLegacy(NodeChainId, nonce: 0, gasPrice: 1, gasLimit: 21_000, amount: 1000);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(MempoolRejectReason.None, result.Reason);
            Assert.Equal(SenderAddress, result.Sender);
        }

        [Fact]
        public async Task Given_HighSSignature_When_Validated_Then_RejectedInvalidSignature()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000_000_000_000_000);

            var tx = new Transaction1559((EvmUInt256)NodeChainId, 0, 1, 1, 21_000, Recipient, 1000, "", new List<AccessListItem>());
            var sig = new EthECKey(PrivateKey).SignAndCalculateYParityV(tx.RawHash);
            var sLow = new BigInteger(sig.S, isUnsigned: true, isBigEndian: true);
            var sHigh = CurveOrderN - sLow;
            tx.SetSignature(new Signature(sig.R, To32BytesBigEndian(sHigh), sig.V));

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.InvalidSignature, result.Reason);
        }

        [Fact]
        public async Task Given_TransactionSignedForWrongChain_When_Validated_Then_RejectedWrongChainId()
        {
            var validator = Validator(chainId: NodeChainId);
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000_000_000_000_000);
            var tx = SignLegacy(chainId: 999, nonce: 0, gasPrice: 1, gasLimit: 21_000, amount: 1000);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.WrongChainId, result.Reason);
        }

        [Fact]
        public async Task Given_GasLimitBelowIntrinsic_When_Validated_Then_RejectedIntrinsicGasTooLow()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000_000_000_000_000);
            var tx = SignLegacy(NodeChainId, nonce: 0, gasPrice: 1, gasLimit: 20_000, amount: 1000);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.IntrinsicGasTooLow, result.Reason);
        }

        [Fact]
        public async Task Given_PriorityFeeAboveFeeCap_When_Validated_Then_RejectedFeeCapBelowPriorityFee()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000_000_000_000_000);

            var tx1559 = new Transaction1559((EvmUInt256)NodeChainId, 0, 100, 50, 21_000, Recipient, 1000, "", new List<AccessListItem>());
            var signedHex = new Transaction1559Signer().SignTransaction(PrivateKey, tx1559);
            var tx = TransactionFactory.CreateTransaction(signedHex);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.FeeCapBelowPriorityFee, result.Reason);
        }

        [Fact]
        public async Task Given_NonceBelowConfirmedNonce_When_Validated_Then_RejectedNonceTooLow()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 5, balance: 1_000_000_000_000_000_000);
            var tx = SignLegacy(NodeChainId, nonce: 0, gasPrice: 1, gasLimit: 21_000, amount: 1000);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.NonceTooLow, result.Reason);
        }

        [Fact]
        public async Task Given_BalanceBelowValuePlusGas_When_Validated_Then_RejectedInsufficientBalance()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1);
            var tx = SignLegacy(NodeChainId, nonce: 0, gasPrice: 1, gasLimit: 21_000, amount: 1000);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.InsufficientBalance, result.Reason);
        }

        [Fact]
        public async Task Given_OversizeTransaction_When_Validated_Then_RejectedOversize()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000_000_000_000_000);
            var data = "0x" + new string('a', 200 * 1024 * 2);
            var tx = SignLegacy(NodeChainId, nonce: 0, gasPrice: 1, gasLimit: 5_000_000, amount: 0, data: data);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.Oversize, result.Reason);
        }

        [Fact]
        public async Task Given_OverflowInducingGasLimitAndFee_When_Validated_Then_RejectedGasLimitTooHigh()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 0);
            var huge = (EvmUInt256)(BigInteger.One << 128);
            var tx1559 = new Transaction1559((EvmUInt256)NodeChainId, 0, 0, huge, huge, Recipient, 0, "", new List<AccessListItem>());
            var signedHex = new Transaction1559Signer().SignTransaction(PrivateKey, tx1559);
            var tx = TransactionFactory.CreateTransaction(signedHex);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.GasLimitTooHigh, result.Reason);
        }

        [Fact]
        public async Task Given_ProductThatWrapsButGasLimitUnderRaisedCap_When_Validated_Then_RejectedInsufficientBalance()
        {
            var validator = new MempoolAdmissionValidator(NodeChainId, maxGasLimit: long.MaxValue);
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 0);
            var gasLimit = (EvmUInt256)(BigInteger.One << 62);
            var maxFee = (EvmUInt256)(BigInteger.One << 200);
            var tx1559 = new Transaction1559((EvmUInt256)NodeChainId, 0, 0, maxFee, gasLimit, Recipient, 0, "", new List<AccessListItem>());
            var signedHex = new Transaction1559Signer().SignTransaction(PrivateKey, tx1559);
            var tx = TransactionFactory.CreateTransaction(signedHex);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.False(result.Accepted);
            Assert.Equal(MempoolRejectReason.InsufficientBalance, result.Reason);
        }

        [Fact]
        public async Task Given_HighButSaneGasLimitWithFunds_When_Validated_Then_Accepted()
        {
            var validator = Validator();
            using var bundle = await FundedBundleAsync(nonce: 0, balance: 1_000_000_000_000_000_000);
            var tx = SignLegacy(NodeChainId, nonce: 0, gasPrice: 1, gasLimit: 30_000_000, amount: 0);

            var result = await validator.ValidateAsync(tx, bundle, new TxPool());

            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(MempoolRejectReason.None, result.Reason);
        }

        private static byte[] To32BytesBigEndian(BigInteger value)
        {
            var unsigned = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (unsigned.Length == 32) return unsigned;
            var padded = new byte[32];
            Array.Copy(unsigned, 0, padded, 32 - unsigned.Length, unsigned.Length);
            return padded;
        }
    }
}
