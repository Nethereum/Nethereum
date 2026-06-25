using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Crypto
{
    public class RlpxSecrets
    {
        public byte[] AesSecret { get; init; }
        public byte[] MacSecret { get; init; }
        public KeccakMacState EgressMac { get; init; }
        public KeccakMacState IngressMac { get; init; }

        public static RlpxSecrets Derive(
            EthECKey localEphemeral,
            byte[] remoteEphemeralPubNoPrefix,
            byte[] initiatorNonce,
            byte[] recipientNonce,
            byte[] authPacket,
            byte[] ackPacket,
            bool isInitiator)
        {
            var remoteEph = new EthECKey(remoteEphemeralPubNoPrefix, false, EthECKey.DEFAULT_PREFIX);
            var ephemeralSharedSecret = localEphemeral.CalculateCommonSecret(remoteEph);

            var keccak = Sha3Keccack.Current;

            var nonceHash = keccak.CalculateHash(recipientNonce.ConcatArrays(initiatorNonce));
            var sharedSecret = keccak.CalculateHash(ephemeralSharedSecret.ConcatArrays(nonceHash));
            var aesSecret = keccak.CalculateHash(ephemeralSharedSecret.ConcatArrays(sharedSecret));
            var macSecret = keccak.CalculateHash(ephemeralSharedSecret.ConcatArrays(aesSecret));

            KeccakMacState egressMac, ingressMac;
            if (isInitiator)
            {
                egressMac = KeccakMacState.Init(macSecret.XOR(recipientNonce), authPacket);
                ingressMac = KeccakMacState.Init(macSecret.XOR(initiatorNonce), ackPacket);
            }
            else
            {
                egressMac = KeccakMacState.Init(macSecret.XOR(initiatorNonce), ackPacket);
                ingressMac = KeccakMacState.Init(macSecret.XOR(recipientNonce), authPacket);
            }

            return new RlpxSecrets
            {
                AesSecret = aesSecret,
                MacSecret = macSecret,
                EgressMac = egressMac,
                IngressMac = ingressMac
            };
        }
    }
}
