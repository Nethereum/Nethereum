using System.Collections.Generic;
using System.Linq;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.Signer
{
    public class RLPEncoder
    {
        public static byte[] EncodeSigned(SignedData signedData, int numberOfElements)
        {
            var encodedData = new List<byte[]>();
            for (var i = 0; i < numberOfElements; i++)
                encodedData.Add(RLP.RLP.EncodeElement(signedData.Data[i]));

            AddSignatureToEncodedData(signedData.GetSignature(), encodedData);

            return RLP.RLP.EncodeList(encodedData.ToArray());
        }


        public static void AddSignatureToEncodedData(EthECDSASignature signature, List<byte[]> encodedData)
        {
            byte[] v, r, s;

            if (signature != null && signature.V != null)
            {
                if (signature.V[0] == 0)
                {
                    v = DefaultValues.EMPTY_BYTE_ARRAY;
                }
                else
                {
                    v = signature.V;
                }
                v = RLP.RLP.EncodeElement(v);
                r = RLP.RLP.EncodeElement(signature.R.TrimZeroBytes());
                s = RLP.RLP.EncodeElement(signature.S.TrimZeroBytes());
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
        }

    }
}