using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;

namespace Nethereum.RPC.Eth.Mappers
{
    public static class SignatureRpcMapper
    {
        /// <summary>
        /// JSON-RPC encodes a signature word — v, r, s, and the EIP-7702 authorisation yParity — as
        /// QUANTITY: 0x-prefixed with leading zeros stripped, and zero as "0x0". The words are stored
        /// left-padded (r/s to 32 bytes), so a raw byte-to-hex keeps padding JSON-RPC clients strip;
        /// normalising through the unsigned big-endian magnitude yields the canonical form. The single
        /// signature-quantity seam shared by the transaction serializer (v/r/s) and the 7702
        /// authorisation mapper (r/s/yParity).
        /// </summary>
        public static string ToRpcSignatureQuantity(this byte[] bigEndianValue)
        {
            if (bigEndianValue == null) return null;
            return new HexBigInteger(new BigInteger(
                ByteUtil.BigEndianToBigIntegerLittleEndianUnsigned(bigEndianValue))).HexValue;
        }
    }
}
