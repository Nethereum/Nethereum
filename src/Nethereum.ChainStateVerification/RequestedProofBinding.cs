using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.ChainStateVerification
{
    internal static class RequestedProofBinding
    {
        public static AccountProof RequireAccount(AccountProof proof, string requestedAddress)
        {
            if (proof.Address == null || !proof.Address.HexToByteArray().SequenceEqual(requestedAddress.HexToByteArray()))
            {
                throw new InvalidChainDataException($"Proof is for account {proof.Address ?? "(none)"}, not the requested {requestedAddress}.");
            }
            return proof;
        }

        public static StorageProof RequireSlot(AccountProof proof, string requestedSlotHex)
        {
            var requestedKey = requestedSlotHex.HexToByteArray().PadTo32Bytes();
            var entry = proof.StorageProof?.FirstOrDefault(p => p?.Key?.HexValue != null
                && p.Key.HexValue.HexToByteArray().PadTo32Bytes().SequenceEqual(requestedKey));
            if (entry == null || requestedKey.Length != 32)
            {
                throw new InvalidChainDataException($"Proof does not include the requested storage slot {requestedSlotHex}.");
            }
            return entry;
        }
    }
}
