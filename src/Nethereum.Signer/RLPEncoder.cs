using System.Collections.Generic;
using System.Linq;
using Nethereum.Model;

namespace Nethereum.Signer
{
    public class RLPEncoder
    {
        public static byte[] EncodeSigned(SignedData signedData, int numberOfElements)
        {
            var encodedData = new List<byte[]>();
            for (var i = 0; i < numberOfElements; i++)
                encodedData.Add(RLP.RLP.EncodeElement(signedData.Data[i]));

            byte[] v, r, s;

            if (signedData.IsSigned())
            {
                v = RLP.RLP.EncodeElement(signedData.V);
                r = RLP.RLP.EncodeElement(signedData.R);
                s = RLP.RLP.EncodeElement(signedData.S);
            }
            else
            {
                v = RLP.RLP.EncodeElement(DefaultValues.EMPTY_BYTE_ARRAY);
                r = RLP.RLP.EncodeElement(DefaultValues.EMPTY_BYTE_ARRAY);
                s = RLP.RLP.EncodeElement(DefaultValues.EMPTY_BYTE_ARRAY);
            }

            encodedData.Add(v);
            encodedData.Add(r);
            encodedData.Add(s);

            return RLP.RLP.EncodeList(encodedData.ToArray());
        }

    }
}