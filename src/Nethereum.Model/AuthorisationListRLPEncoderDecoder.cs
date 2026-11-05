using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Model
{
    public class AuthorisationListRLPEncoderDecoder
    {
        public static byte[] Encode(List<Authorisation7702Signed> authorisationListItem)
        {
            var encodedData = new List<byte[]>();

            foreach (var accessListItem in authorisationListItem)
            {
                Authorisation7702RLPEncoderAndHasher.Validate(accessListItem);
                var encodedItem = new List<byte[]>
                {
                    RLP.RLP.EncodeElement(accessListItem.ChainId.ToBytesForRLPEncoding()),
                    RLP.RLP.EncodeElement(accessListItem.Address.HexToByteArray()),
                    RLP.RLP.EncodeElement(accessListItem.Nonce.ToBytesForRLPEncoding()),
                    RLP.RLP.EncodeElement((accessListItem.V ?? new byte[0]).TrimZeroBytes()),
                    RLP.RLP.EncodeElement((accessListItem.R ?? new byte[0]).TrimZeroBytes()),
                    RLP.RLP.EncodeElement((accessListItem.S ?? new byte[0]).TrimZeroBytes())
                };
                encodedData.Add(RLP.RLP.EncodeList(encodedItem.ToArray()));
            }

            return RLP.RLP.EncodeList(encodedData.ToArray());
        }
        public static List<Authorisation7702Signed> Decode(byte[] encoded)
        {
            var reader = new RlpReader(encoded);
            var authorisations = Read(reader);
            reader.EnsureEnd();
            return authorisations;
        }

        internal static List<Authorisation7702Signed> Read(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var authorisations = new List<Authorisation7702Signed>();
            while (!reader.IsAtEnd(scope))
            {
                if (reader.PeekKind() != RlpKind.List)
                    throw new AuthorisationFormatException($"an authorization is a list of exactly {AuthorisationTupleFieldCount} fields");

                var item = reader.ReadListHeader();
                if (reader.CountItems(item) != AuthorisationTupleFieldCount)
                    throw new AuthorisationFormatException($"an authorization is a list of exactly {AuthorisationTupleFieldCount} fields");

                var authorisationListItem = new Authorisation7702Signed();
                authorisationListItem.ChainId = Scalar(reader, 32, "chain_id").ToEvmUInt256FromRLPDecoded();
                authorisationListItem.Address = Address(reader).ToHex();
                authorisationListItem.Nonce = Scalar(reader, 8, "nonce").ToEvmUInt256FromRLPDecoded();
                authorisationListItem.V = Scalar(reader, 1, "y_parity") ?? new byte[0];
                authorisationListItem.R = Scalar(reader, 32, "r") ?? new byte[0];
                authorisationListItem.S = Scalar(reader, 32, "s") ?? new byte[0];
                reader.EnsureEnd(item);
                authorisations.Add(authorisationListItem);
            }

            reader.EnsureEnd(scope);
            return authorisations;
        }

        private const int AuthorisationTupleFieldCount = 6;
        private const int AddressLength = 20;

        private static byte[] Scalar(RlpReader reader, int maxBytes, string field)
        {
            if (reader.PeekKind() == RlpKind.List)
                throw new AuthorisationFormatException($"{field} is a list, not a scalar");

            var bytes = reader.ReadBytes();
            if (!RlpScalar.IsCanonical(bytes))
                throw new AuthorisationFormatException($"{field} is encoded with a leading zero byte");
            if (bytes.Length > maxBytes)
                throw new AuthorisationFormatException($"{field} is {bytes.Length} bytes and its field holds {maxBytes}");
            return bytes.Length == 0 ? null : bytes;
        }

        private static byte[] Address(RlpReader reader)
        {
            if (reader.PeekKind() == RlpKind.List || reader.PeekSize() != AddressLength)
                throw new AuthorisationFormatException($"address is not {AddressLength} bytes");
            return reader.ReadFixed(AddressLength);
        }

    }
}
