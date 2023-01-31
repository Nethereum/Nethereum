using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public class RLPSignedDataDecoder
    {
        public static SignedData DecodeSigned(byte[] rawdata, int numberOfEncodingElements)
        {
            var decodedList = RLP.RLP.Decode(rawdata);
            var decodedData = new List<byte[]>();
            var decodedElements = (RLPCollection)decodedList;
            
            for (var i = 0; i < numberOfEncodingElements; i++)
                decodedData.Add(decodedElements[i].RLPData);
            // only parse signature in case is signed
            var signature = DecodeSignature(decodedElements, numberOfEncodingElements);
            return new SignedData(decodedData.ToArray(), signature);
        }

        public static Signature DecodeSignature(RLPCollection decodedElements, int numberOfEncodingElements)
        {
            Signature signature = null;
            if (decodedElements.Count > numberOfEncodingElements && decodedElements[numberOfEncodingElements + 1].RLPData != null)
            {
                var v = new byte[] {0};
                //Decode Signature
                if (decodedElements[numberOfEncodingElements].RLPData != null)
                {
                    v = decodedElements[numberOfEncodingElements].RLPData;
                }

                var r = decodedElements[numberOfEncodingElements + 1].RLPData;
                var s = decodedElements[numberOfEncodingElements + 2].RLPData;
                signature = new Signature(r, s, v);
            }

            return signature;
        }
    }
}


