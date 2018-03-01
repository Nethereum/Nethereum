using System.Collections.Generic;
using System.Linq;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Signer
{
    // the one used for Homestead, before requiring ChainId
    public class LegacyRlpSigner : IRlpSigner
    {
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        private byte[][] data;
        private bool decoded;
        private readonly int numberOfElements;
        private byte[] rlpEncoded;
        private byte[] rlpRaw;

        private EthECDSASignature signature;

        public LegacyRlpSigner(byte[] rawData, int numberOfElements)
        {
            rlpEncoded = rawData;
            decoded = false;
            this.numberOfElements = numberOfElements;
        }

        public LegacyRlpSigner(byte[][] data)
        {
            numberOfElements = data.Length;
            this.data = data;
            decoded = true;
        }

        public LegacyRlpSigner(byte[][] data, byte[] r, byte[] s, byte v)
        {
            numberOfElements = data.Length;
            this.data = data;
            signature = EthECDSASignatureFactory.FromComponents(r, s, v);
            decoded = true;
        }

        public byte[] Hash
        {
            get
            {
                EnsuredRPLDecoded();
                var plainMsg = GetRLPEncoded();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        public byte[] RawHash
        {
            get
            {
                EnsuredRPLDecoded();
                var plainMsg = GetRLPEncodedRaw();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        public byte[][] Data
        {
            get
            {
                EnsuredRPLDecoded();
                return data;
            }
        }

        public EthECDSASignature Signature
        {
            get
            {
                EnsuredRPLDecoded();
                return signature;
            }
        }

        public EthECKey Key => EthECKey.RecoverFromSignature(Signature, RawHash);

        public byte[] BuildRLPEncoded(bool raw)
        {
            var encodedData = new List<byte[]>();
            encodedData.AddRange(Data.Select(RLP.RLP.EncodeElement).ToArray());

            if (raw)
                return RLP.RLP.EncodeList(encodedData.ToArray());

            byte[] v, r, s;

            if (signature != null)
            {
                v = RLP.RLP.EncodeByte(signature.V);
                r = RLP.RLP.EncodeElement(signature.R);
                s = RLP.RLP.EncodeElement(signature.S);
            }
            else
            {
                v = RLP.RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                r = RLP.RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                s = RLP.RLP.EncodeElement(EMPTY_BYTE_ARRAY);
            }

            encodedData.Add(v);
            encodedData.Add(r);
            encodedData.Add(s);

            return RLP.RLP.EncodeList(encodedData.ToArray());
        }

        public byte[] GetRLPEncoded()
        {
            if (rlpEncoded != null) return rlpEncoded;
            rlpEncoded = BuildRLPEncoded(false);
            return rlpEncoded;
        }

        public byte[] GetRLPEncodedRaw()
        {
            EnsuredRPLDecoded();

            if (rlpRaw != null)
                return rlpRaw;
            rlpRaw = BuildRLPEncoded(true);
            return rlpRaw;
        }

        public void RlpDecode()
        {
            var decodedList = RLP.RLP.Decode(GetRLPEncoded());
            var decodedData = new List<byte[]>();
            var decodedElements = (RLPCollection) decodedList[0];
            for (var i = 0; i < numberOfElements; i++)
                decodedData.Add(decodedElements[i].RLPData);
            // only parse signature in case is signed
            if (decodedElements[numberOfElements].RLPData != null)
            {
                var v = decodedElements[numberOfElements].RLPData[0];
                var r = decodedElements[numberOfElements + 1].RLPData;
                var s = decodedElements[numberOfElements + 2].RLPData;

                signature = EthECDSASignatureFactory.FromComponents(r, s, v);
            }
            data = decodedData.ToArray();
            decoded = true;
        }

        public void Sign(EthECKey key)
        {
            signature = key.SignAndCalculateV(RawHash);
            rlpEncoded = null;
        }

        private void EnsuredRPLDecoded()
        {
            if (!decoded)
                RlpDecode();
        }
    }
}