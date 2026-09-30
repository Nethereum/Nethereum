using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian
{
    public static class MultiGuardianSignatureBlobBuilder
    {
        public static byte[] BuildFromFinalHash(byte[] dataHash, IReadOnlyList<EthECKey> guardians, int threshold)
        {
            if (dataHash == null)
                throw new ArgumentNullException(nameof(dataHash));

            if (dataHash.Length != 32)
                throw new ArgumentException($"Hash must be exactly 32 bytes (was {dataHash.Length})", nameof(dataHash));

            ValidateGuardianSet(guardians, threshold);

            var rawSigner = new MessageSigner();

            var slots = guardians
                .Take(threshold)
                .Select(key => new
                {
                    Address = key.GetPublicAddress(),
                    Slot = rawSigner.Sign(dataHash, key).HexToByteArray()
                })
                .OrderBy(x => AscendingAddressKey(x.Address))
                .Select(x => x.Slot)
                .ToArray();

            return ByteUtil.Merge(slots);
        }

        public static string[] SortAddressesAscending(IEnumerable<string> addresses) =>
            addresses.OrderBy(AscendingAddressKey).ToArray();

        private static BigInteger AscendingAddressKey(string address) => address.HexToBigInteger(false);

        public static byte[] BuildFromUserOpHash(byte[] userOpHash, IReadOnlyList<EthECKey> guardians, int threshold)
        {
            if (userOpHash == null)
                throw new ArgumentNullException(nameof(userOpHash));

            if (userOpHash.Length != 32)
                throw new ArgumentException($"UserOpHash must be exactly 32 bytes (was {userOpHash.Length})", nameof(userOpHash));

            var ethSignedHash = new EthereumMessageSigner().HashPrefixedMessage(userOpHash);
            return BuildFromFinalHash(ethSignedHash, guardians, threshold);
        }

        internal static void ValidateGuardianSet(IReadOnlyList<EthECKey> guardians, int threshold)
        {
            if (guardians == null)
                throw new ArgumentNullException(nameof(guardians));

            if (guardians.Any(g => g == null))
                throw new ArgumentException("Guardian keys must not contain null entries", nameof(guardians));

            if (threshold <= 0)
                throw new ArgumentException($"Threshold must be at least 1 (was {threshold})", nameof(threshold));

            if (guardians.Count < threshold)
                throw new ArgumentException(
                    $"At least {threshold} guardian signer(s) are required to meet the threshold (was {guardians.Count})",
                    nameof(guardians));

            var distinctAddresses = guardians.Select(g => g.GetPublicAddress().ToLowerInvariant()).Distinct().Count();
            if (distinctAddresses != guardians.Count)
                throw new ArgumentException("Guardian keys must not contain duplicate addresses", nameof(guardians));
        }
    }
}
