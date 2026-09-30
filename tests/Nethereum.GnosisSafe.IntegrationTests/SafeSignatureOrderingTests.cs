using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.GnosisSafe.IntegrationTests
{
    public class SafeSignatureOrderingTests
    {
        private static readonly byte[] SafeTxHash = new Sha3Keccack().CalculateHash(System.Text.Encoding.UTF8.GetBytes("safe-tx"));

        private static GnosisSafeService.SafeSignature Sign(EthECKey key)
        {
            var signature = key.SignAndCalculateV(SafeTxHash);
            return new GnosisSafeService.SafeSignature
            {
                Address = key.GetPublicAddress(),
                Signature = EthECDSASignature.CreateStringSignature(signature)
            };
        }

        private static (GnosisSafeService.SafeSignature First, GnosisSafeService.SafeSignature Second) PairWhoseSignatureOrderDiffersFromOwnerOrder()
        {
            for (var i = 1; i < 64; i++)
            {
                var a = Sign(new EthECKey(new byte[31].Concat(new[] { (byte)i }).ToArray(), true));
                var b = Sign(new EthECKey(new byte[31].Concat(new[] { (byte)(i + 64) }).ToArray(), true));
                var byAddress = string.CompareOrdinal(a.Address.ToLowerInvariant(), b.Address.ToLowerInvariant()) < 0;
                var bySignature = string.CompareOrdinal(
                    GnosisSafeService.ConvertSignatureStringToGnosisVFormat(a.Signature).ToLowerInvariant(),
                    GnosisSafeService.ConvertSignatureStringToGnosisVFormat(b.Signature).ToLowerInvariant()) < 0;
                if (byAddress != bySignature) return (a, b);
            }
            throw new InvalidOperationException("no key pair with differing orders found");
        }

        [Fact]
        public void Given_SignaturesWhoseHexOrderDiffersFromOwnerOrder_When_Combined_Then_TheyAreOrderedByAscendingOwnerAddress()
        {
            var (a, b) = PairWhoseSignatureOrderDiffersFromOwnerOrder();
            var service = new GnosisSafeService(new Web3.Web3(), "0x0000000000000000000000000000000000000001");

            var combined = service.GetCombinedSignaturesInOrder(new List<GnosisSafeService.SafeSignature> { a, b }).ToHex();

            var ascending = new[] { a, b }.OrderBy(s => s.Address.ToLowerInvariant(), StringComparer.Ordinal)
                .Select(s => GnosisSafeService.ConvertSignatureStringToGnosisVFormat(s.Signature).RemoveHexPrefix())
                .Aggregate(string.Empty, string.Concat);
            Assert.Equal(ascending, combined, ignoreCase: true);
        }
    }
}
