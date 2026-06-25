using System;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Discv4
{
    /// <summary>
    /// EIP-2 canonical-s enforcement on inbound discv4 packet signatures. Rejects
    /// the malleated twin <c>(r, N - s, v ^ 1)</c> which would otherwise recover
    /// a different public key from the same signed digest and admit a phantom
    /// node-id sharing the original sender's endpoint.
    /// See <see href="https://eips.ethereum.org/EIPS/eip-2"/>.
    /// </summary>
    public class Discv4PacketCanonicalSTests
    {
        [Fact]
        [Trait("Category", "Discv4-Security")]
        [Trait("Rule", "EIP-2 low-s")]
        public void Given_HonestLowSSignature_When_Decoded_Then_RecoversSenderPubKey()
        {
            var localKey = EthECKey.GenerateKey();
            var packet = Discv4Packet.Encode(localKey, Discv4MessageType.Ping, new byte[] { 0x80 });

            var decoded = Discv4Packet.Decode(packet);

            Assert.NotNull(decoded);
            Assert.Equal(localKey.GetPubKeyNoPrefix(), decoded.SenderPubKey);
        }

        [Fact]
        [Trait("Category", "Discv4-Security")]
        [Trait("Rule", "EIP-2 low-s")]
        public void Given_MalleatedHighSWithValidHash_When_Decoded_Then_Throws()
        {
            var localKey = EthECKey.GenerateKey();
            var packet = Discv4Packet.Encode(localKey, Discv4MessageType.Ping, new byte[] { 0x80 });

            // Mutate s in place: s' = N - s, v' = v ^ 1. This is the classic
            // malleability twin — same digest, different recovered pubkey.
            var sOffset = Discv4Packet.HashLength + 32;
            var sBytes = new byte[32];
            Buffer.BlockCopy(packet, sOffset, sBytes, 0, 32);
            var s = new BigInteger(1, sBytes);
            var sFlipped = ECKey.CURVE_ORDER.Subtract(s).ToByteArrayUnsigned();
            var sFlippedPadded = LeftPad(sFlipped, 32);
            Buffer.BlockCopy(sFlippedPadded, 0, packet, sOffset, 32);
            packet[Discv4Packet.HashLength + 64] ^= 0x01;

            // Recompute the outer keccak so the hash check passes and we reach
            // the canonical-s guard.
            var signedPortion = new byte[packet.Length - Discv4Packet.HashLength];
            Buffer.BlockCopy(packet, Discv4Packet.HashLength, signedPortion, 0, signedPortion.Length);
            var hash = new Sha3Keccack().CalculateHash(signedPortion);
            Buffer.BlockCopy(hash, 0, packet, 0, Discv4Packet.HashLength);

            var ex = Assert.Throws<InvalidOperationException>(() => Discv4Packet.Decode(packet));
            Assert.Contains("canonical", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "Discv4-Security")]
        [Trait("Rule", "EIP-2 low-s")]
        public void Given_SignatureWithZeroS_When_Decoded_Then_Throws()
        {
            var localKey = EthECKey.GenerateKey();
            var packet = Discv4Packet.Encode(localKey, Discv4MessageType.Ping, new byte[] { 0x80 });

            // Zero out s and recompute outer hash.
            var sOffset = Discv4Packet.HashLength + 32;
            for (int i = 0; i < 32; i++) packet[sOffset + i] = 0x00;
            var signedPortion = new byte[packet.Length - Discv4Packet.HashLength];
            Buffer.BlockCopy(packet, Discv4Packet.HashLength, signedPortion, 0, signedPortion.Length);
            var hash = new Sha3Keccack().CalculateHash(signedPortion);
            Buffer.BlockCopy(hash, 0, packet, 0, Discv4Packet.HashLength);

            Assert.Throws<InvalidOperationException>(() => Discv4Packet.Decode(packet));
        }

        [Fact]
        [Trait("Category", "Discv4-Security")]
        [Trait("Rule", "EIP-2 low-s")]
        public void Given_SignatureWithZeroR_When_Decoded_Then_Throws()
        {
            var localKey = EthECKey.GenerateKey();
            var packet = Discv4Packet.Encode(localKey, Discv4MessageType.Ping, new byte[] { 0x80 });

            // Zero out r and recompute outer hash.
            var rOffset = Discv4Packet.HashLength;
            for (int i = 0; i < 32; i++) packet[rOffset + i] = 0x00;
            var signedPortion = new byte[packet.Length - Discv4Packet.HashLength];
            Buffer.BlockCopy(packet, Discv4Packet.HashLength, signedPortion, 0, signedPortion.Length);
            var hash = new Sha3Keccack().CalculateHash(signedPortion);
            Buffer.BlockCopy(hash, 0, packet, 0, Discv4Packet.HashLength);

            Assert.Throws<InvalidOperationException>(() => Discv4Packet.Decode(packet));
        }

        private static byte[] LeftPad(byte[] bytes, int length)
        {
            if (bytes.Length == length) return bytes;
            if (bytes.Length > length)
            {
                var trimmed = new byte[length];
                Buffer.BlockCopy(bytes, bytes.Length - length, trimmed, 0, length);
                return trimmed;
            }
            var padded = new byte[length];
            Buffer.BlockCopy(bytes, 0, padded, length - bytes.Length, bytes.Length);
            return padded;
        }
    }
}
