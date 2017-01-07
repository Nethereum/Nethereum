using System.Collections.Generic;
using System.Linq;
using NBitcoin.Crypto;
using Nethereum.ABI.Util;
using Nethereum.ABI.Util.RLP;
using Nethereum.Core.Signing.Crypto;
using Org.BouncyCastle.Asn1.Ocsp;

namespace Nethereum.Core
{
    public class RLPSigner
    {
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        private byte[][] data;
        private byte[] rlpEncoded;
        private byte[] rlpRaw;
        
        private ECDSASignature signature;
        private bool decoded;
        private int numberOfElements;

        public RLPSigner(byte[] rawData, int numberOfElements)
        {
            rlpEncoded = rawData;
            decoded = false;
            this.numberOfElements = numberOfElements;
        }

        public RLPSigner(byte[][] data)
        {
            this.numberOfElements = data.Length;
            this.data = data;
            decoded = true;
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte v)
        {
            this.numberOfElements = data.Length;
            this.data = data;
            this.signature = EthECDSASignatureFactory.FromComponents(r, s, v);
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

        public ECDSASignature Signature
        {
            get
            {
                EnsuredRPLDecoded();
                return signature;
            }
        }

        public ECKey Key => EthECKey.RecoverFromSignature(Signature, RawHash);

        public byte[] BuildRLPEncoded(bool raw)
        {
            var encodedData = new List<byte[]>();
            encodedData.AddRange(this.Data.Select(RLP.EncodeElement).ToArray());

            if (raw)
                return RLP.EncodeList(encodedData.ToArray());

            byte[] v, r, s;

            if (signature != null)
            {
                v = RLP.EncodeByte(signature.V);
                r = RLP.EncodeElement(signature.R.ToByteArrayUnsigned());
                s = RLP.EncodeElement(signature.S.ToByteArrayUnsigned());
            }
            else
            {
                v = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                r = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                s = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
            }

            encodedData.Add(v);
            encodedData.Add(r);
            encodedData.Add(s);

            return RLP.EncodeList(encodedData.ToArray());
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
            var decodedList = RLP.Decode(GetRLPEncoded());
            var decodedData = new List<byte[]>();
            var decodedElements = (RLPCollection)decodedList[0];
            for (var i = 0; i < numberOfElements; i++)
            {
                decodedData.Add(decodedElements[i].RLPData);    
            }
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

        public void Sign(ECKey key)
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