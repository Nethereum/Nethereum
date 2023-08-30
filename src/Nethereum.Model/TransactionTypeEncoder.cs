using System;
using System.Numerics;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public abstract class TransactionTypeEncoder<T>: ITransactionTypeDecoder where T : SignedTypeTransaction
    {
        public static byte[] AddTypeToEncodedBytes(byte[] encodedBytes, byte type)
        {
            var returnBytes = new byte[encodedBytes.Length + 1];
            Array.Copy(encodedBytes, 0, returnBytes, 1, encodedBytes.Length);
            returnBytes[0] = type;
            return returnBytes;
        }

        public byte[] GetBigIntegerForEncoding(BigInteger? value)
        {
            if (value == null) return DefaultValues.ZERO_BYTE_ARRAY;
            return value.Value.ToBytesForRLPEncoding();
        }
        public SignedTypeTransaction DecodeAsGeneric(byte[] rlpData)
        {
            return Decode(rlpData);
        }

        public abstract T Decode(byte[] rplData);
        public abstract byte[] Encode(T transaction);
        public abstract byte[] EncodeRaw(T transaction);
    }
}