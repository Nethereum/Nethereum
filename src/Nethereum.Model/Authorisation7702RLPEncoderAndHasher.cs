using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.Model
{
    public static class Authorisation7702RLPEncoderAndHasher
    {
        public static byte MAGIC_NUMBER = 0x05;
        public static void Validate(this Authorisation7702 authorisation)
        {
            if (authorisation.ChainId < 0 || authorisation.ChainId >= BigInteger.Pow(2, 256))
                throw new ArgumentOutOfRangeException(nameof(authorisation.ChainId), "ChainId must be < 2^256.");
            if (authorisation.Nonce < 0 || authorisation.Nonce >= BigInteger.Pow(2, 64))
                throw new ArgumentOutOfRangeException(nameof(authorisation.Nonce), "Nonce must be < 2^64.");
            if (string.IsNullOrEmpty(authorisation.Address) || !authorisation.Address.IsValidEthereumAddressLength())
                throw new ArgumentException("Address length must be exactly 20 bytes.", nameof(authorisation.Address));
        }

        public static byte[] Encode(this Authorisation7702 authorisation)
        {
            Validate(authorisation);
            var encodedItem = new List<byte[]>();
            encodedItem.Add(RLP.RLP.EncodeElement(authorisation.ChainId.ToBytesForRLPEncoding()));
            encodedItem.Add(RLP.RLP.EncodeElement(authorisation.Address.HexToByteArray()));
            encodedItem.Add(RLP.RLP.EncodeElement(authorisation.Nonce.ToBytesForRLPEncoding()));
            var encoded = RLP.RLP.EncodeList(encodedItem.ToArray());
            return AddMagicNumberToEncodedBytes(encoded);
        }

        public static Authorisation7702 DecodeRLPToAuthorisation7702(byte[] encodedBytes)
        {
            if (encodedBytes.Length < 1 || encodedBytes[0] != MAGIC_NUMBER)
                throw new ArgumentException("Encoded bytes do not contain the magic number.", nameof(encodedBytes));
            var decoded = (RLPCollection)RLP.RLP.Decode(encodedBytes.Skip(1).ToArray());
            var chainId = decoded[0].RLPData.ToBigIntegerFromRLPDecoded();
            var address = decoded[1].RLPData.ToHex();
            var nonce = decoded[2].RLPData.ToBigIntegerFromRLPDecoded();
            return new Authorisation7702
            {
                ChainId = chainId,
                Address = address,
                Nonce = nonce
            };
        }

        public static byte[] EncodeAndHash(this Authorisation7702 authorisation)
        {
            var encoded = Encode(authorisation);
            return new Sha3Keccack().CalculateHash(encoded);
        }

        public static byte[] AddMagicNumberToEncodedBytes(byte[] encodedBytes)
        {
            var returnBytes = new byte[encodedBytes.Length + 1];
            Array.Copy(encodedBytes, 0, returnBytes, 1, encodedBytes.Length);
            returnBytes[0] = MAGIC_NUMBER;
            return returnBytes;
        }
    }
}