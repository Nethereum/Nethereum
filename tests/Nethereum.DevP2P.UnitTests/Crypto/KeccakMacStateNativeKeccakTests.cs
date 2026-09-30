using Nethereum.DevP2P.Crypto;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using BcKeccakDigest = Org.BouncyCastle.Crypto.Digests.KeccakDigest;

namespace Nethereum.DevP2P.UnitTests.Crypto
{
    public class KeccakMacStateNativeKeccakTests
    {
        [Fact]
        public void DigestFirst16_MatchesBouncyCastle_ForInitUpdateCloneSequence()
        {
            var xorKey = Repeat(0xA5, 32);
            var packet = Repeat(0x3C, 64);
            var frameHeader = Repeat(0x11, 16);
            var singleByteChunk = new byte[] { 0x7F };
            var frameBody = Repeat(0xE0, 128);

            var native = KeccakMacState.Init(xorKey, packet);
            native.Update(frameHeader);
            native.Update(singleByteChunk, 0, singleByteChunk.Length);
            var nativeClone = native.Clone();
            nativeClone.Update(frameBody);
            var nativeResult = nativeClone.DigestFirst16();

            var bc = new BcKeccakDigest(256);
            bc.BlockUpdate(xorKey, 0, xorKey.Length);
            bc.BlockUpdate(packet, 0, packet.Length);
            bc.BlockUpdate(frameHeader, 0, frameHeader.Length);
            bc.BlockUpdate(singleByteChunk, 0, singleByteChunk.Length);
            var bcClone = new BcKeccakDigest(bc);
            bcClone.BlockUpdate(frameBody, 0, frameBody.Length);
            var bcFull = new byte[32];
            bcClone.DoFinal(bcFull, 0);
            var bcResult = new byte[16];
            System.Buffer.BlockCopy(bcFull, 0, bcResult, 0, 16);

            Assert.Equal(bcResult.ToHex(), nativeResult.ToHex());
        }

        [Fact]
        public void DigestFirst16_MatchesBouncyCastle_ForEmptyPacketAndNoUpdates()
        {
            var xorKey = Repeat(0x00, 32);
            var packet = System.Array.Empty<byte>();

            var native = KeccakMacState.Init(xorKey, packet);
            var nativeResult = native.DigestFirst16();

            var bc = new BcKeccakDigest(256);
            bc.BlockUpdate(xorKey, 0, xorKey.Length);
            bc.BlockUpdate(packet, 0, packet.Length);
            var bcFull = new byte[32];
            bc.DoFinal(bcFull, 0);
            var bcResult = new byte[16];
            System.Buffer.BlockCopy(bcFull, 0, bcResult, 0, 16);

            Assert.Equal(bcResult.ToHex(), nativeResult.ToHex());
        }

        [Fact]
        public void Clone_DoesNotMutate_OriginalDigestState()
        {
            var xorKey = Repeat(0x42, 32);
            var packet = Repeat(0x99, 8);

            var mac = KeccakMacState.Init(xorKey, packet);
            var beforeClone = mac.Clone().DigestFirst16();

            var clone = mac.Clone();
            clone.Update(Repeat(0xFF, 4));

            var afterCloneUpdate = mac.Clone().DigestFirst16();

            Assert.Equal(beforeClone.ToHex(), afterCloneUpdate.ToHex());
        }

        private static byte[] Repeat(byte value, int length)
        {
            var result = new byte[length];
            for (int i = 0; i < length; i++) result[i] = value;
            return result;
        }
    }
}
