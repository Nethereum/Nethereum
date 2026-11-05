using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.Verify
{
    public class BlobNetworkFormAdmissionTests
    {
        private readonly ITestOutputHelper _out;

        public BlobNetworkFormAdmissionTests(ITestOutputHelper output) { _out = output; }

        private static (byte[] Wire, byte[] Hash) SignedBlobTransactionOnTheWire(EthECKey key, BigInteger chainId)
        {
            var hash = new byte[32];
            hash[0] = 0x01;
            var zero = EvmUInt256.Zero;
            var tx = new Transaction4844(
                (EvmUInt256)chainId, zero, zero, zero, (EvmUInt256)200_000,
                "0x0000000000000000000000000000000000000001", zero, "", new List<AccessListItem>(),
                (EvmUInt256)1_000_000_000, new List<byte[]> { hash });
            tx.Sidecar = new BlobSidecar(
                new List<byte[]> { new byte[131072] },
                new List<byte[]> { new byte[48] },
                Enumerable.Range(0, 128).Select(_ => new byte[48]).ToList())
            { WrapperVersion = 1 };
            new Transaction4844Signer().SignTransaction(key, tx);
            return (tx.GetRLPEncodedWithSidecar(), tx.Hash);
        }

        [Fact]
        public void Given_AOneBlobTransactionInTheNetworkFormEthSendRawTransactionReceives_When_TheSharedAdmissionRulesCheckItsSize_Then_ItIsNotRefusedAsOversize()
        {
            var key = EthECKey.GenerateKey();
            var chainId = new BigInteger(31337);
            var (wire, _) = SignedBlobTransactionOnTheWire(key, chainId);

            var received = TransactionFactory.CreateTransaction(wire);
            if (received is Transaction4844 blob) blob.Sidecar = null;

            var rules = new TransactionAdmissionRules(chainId, 30_000_000);
            var refusal = rules.OversizeReason(received);
            _out.WriteLine($"VERIFY wireBytes={wire.Length} sizeSeenByRules={received.GetRLPEncoded().Length} refusal={refusal ?? "none"}");
            Assert.Null(refusal);
        }
    }
}
