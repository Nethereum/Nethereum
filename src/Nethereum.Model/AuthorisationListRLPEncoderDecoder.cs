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
            var decodedList = (RLPCollection)(RLP.RLP.Decode(encoded));


            var accessLists = new List<Authorisation7702Signed>();
            foreach (var rlpElement in decodedList)
            {
                var decodedItem = (RLPCollection)rlpElement;
                var authorisationListItem = new Authorisation7702Signed();
                authorisationListItem.ChainId = decodedItem[0].RLPData.ToBigIntegerFromRLPDecoded();
                authorisationListItem.Address = decodedItem[1].RLPData.ToHex();
                authorisationListItem.Nonce = decodedItem[2].RLPData.ToBigIntegerFromRLPDecoded();
                authorisationListItem.V = decodedItem[3].RLPData ?? new byte[0];
                authorisationListItem.R = decodedItem[4].RLPData ?? new byte[0];
                authorisationListItem.S = decodedItem[5].RLPData ?? new byte[0];
                accessLists.Add(authorisationListItem);
            }

            return accessLists;
        }

    }
}