using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.Parity
{
    public class BlobTransactionSizeLimitTests
    {
        private const int GethTxMaxSize = 1024 * 1024;
        private const int PreviousLimit = 917_504;
        private static readonly BigInteger ChainId = new BigInteger(31337);

        private static Transaction4844 SixBlobNetworkFormTransaction(int calldataBytes)
        {
            var zero = EvmUInt256.Zero;
            var hashes = Enumerable.Range(0, 6).Select(i => { var h = new byte[32]; h[0] = 0x01; h[31] = (byte)i; return h; }).ToList();
            var tx = new Transaction4844(
                (EvmUInt256)ChainId, zero, zero, zero, (EvmUInt256)30_000_000,
                "0x0000000000000000000000000000000000000001", zero, "0x" + new string('a', calldataBytes * 2), new List<AccessListItem>(),
                (EvmUInt256)1_000_000_000, hashes);
            tx.Sidecar = new BlobSidecar(
                Enumerable.Range(0, 6).Select(_ => new byte[131072]).ToList(),
                Enumerable.Range(0, 6).Select(_ => new byte[48]).ToList(),
                Enumerable.Range(0, 6 * 128).Select(_ => new byte[48]).ToList())
            { WrapperVersion = 1 };
            new Transaction4844Signer().SignTransaction(EthECKey.GenerateKey(), tx);
            return tx;
        }

        [Fact]
        [Trait("Finding", "P10")]
        public void Given_TheDefaultBlobTransactionSizeLimit_When_ItIsRead_Then_ItEqualsGethsTxMaxSizeAndTheDevP2PAliasFollowsIt()
        {
            Assert.Equal(GethTxMaxSize, TransactionAdmissionRules.DefaultMaxBlobTransactionSizeBytes);
            Assert.Equal(GethTxMaxSize, MempoolAdmissionValidator.DefaultMaxBlobTransactionSizeBytes);
        }

        [Fact]
        [Trait("Finding", "P10")]
        public void Given_ASixBlobNetworkFormTransactionBetweenThePreviousLimitAndGethsLimit_When_TheSharedRulesCheckIt_Then_ItIsAccepted()
        {
            var tx = SixBlobNetworkFormTransaction(calldataBytes: 100_000);
            var size = tx.GetRLPEncodedWithSidecar().Length;
            Assert.InRange(size, PreviousLimit + 1, GethTxMaxSize);

            var rules = new TransactionAdmissionRules(ChainId, 100_000_000);

            Assert.Null(rules.OversizeReason(tx));
            Assert.Null(rules.FirstRefusal(tx));
        }

        [Fact]
        [Trait("Finding", "P10")]
        public void Given_ASixBlobNetworkFormTransactionOverGethsLimit_When_TheSharedRulesCheckIt_Then_ItIsRefusedAsOversize()
        {
            var tx = SixBlobNetworkFormTransaction(calldataBytes: 400_000);
            Assert.True(tx.GetRLPEncodedWithSidecar().Length > GethTxMaxSize);

            var rules = new TransactionAdmissionRules(ChainId, 100_000_000);

            Assert.Contains("exceeds blob cap", rules.OversizeReason(tx));
        }
    }
}
