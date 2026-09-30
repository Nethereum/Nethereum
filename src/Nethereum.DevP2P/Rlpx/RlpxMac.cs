using Nethereum.DevP2P.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nethereum.DevP2P.Rlpx
{
    internal sealed class RlpxMac
    {
        private const int BlockSize = 16;

        private readonly AesEngine _macEncryptor;
        private readonly KeccakMacState _mac;

        public RlpxMac(byte[] macSecret, KeccakMacState mac)
        {
            _macEncryptor = new AesEngine();
            _macEncryptor.Init(true, new KeyParameter(macSecret));
            _mac = mac;
        }

        public byte[] NextHeaderMac(byte[] headerCipher)
        {
            var digest = _mac.DigestFirst16();
            var seed = new byte[BlockSize];
            _macEncryptor.ProcessBlock(digest, 0, seed, 0);
            XorInPlace(seed, headerCipher, BlockSize);
            _mac.Update(seed);
            return _mac.DigestFirst16();
        }

        public byte[] NextFrameMac(byte[] frameCipher)
        {
            _mac.Update(frameCipher);
            var digest = _mac.DigestFirst16();
            var seed = new byte[BlockSize];
            _macEncryptor.ProcessBlock(digest, 0, seed, 0);
            XorInPlace(seed, digest, BlockSize);
            _mac.Update(seed);
            return _mac.DigestFirst16();
        }

        private static void XorInPlace(byte[] target, byte[] source, int length)
        {
            for (int i = 0; i < length; i++)
                target[i] ^= source[i];
        }
    }
}
