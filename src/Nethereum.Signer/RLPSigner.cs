using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Signer
{
    public class RLPSigner
    {
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];

        private readonly int numberOfEncodingElements;

        private byte[][] data;
        private bool decoded;

        private byte[] rlpEncoded;
        private byte[] rlpRaw;

        private EthECDSASignature signature;

        public RLPSigner(byte[] rawData, int numberOfEncodingElements)
        {
            rlpEncoded = rawData;
            decoded = false;
            this.numberOfEncodingElements = numberOfEncodingElements;
        }

        public RLPSigner(byte[][] data) : this(data, data.Length)
        {
        }

        public RLPSigner(byte[][] data, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.data = data;
            decoded = true;
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte v) : this(data, r, s, v, data.Length)
        {
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte v, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.data = data;
            signature = EthECDSASignatureFactory.FromComponents(r, s, v);
            decoded = true;
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte[] v) : this(data, r, s, v, data.Length)
        {
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte[] v, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.data = data;
            signature = EthECDSASignatureFactory.FromComponents(r, s, v);
            decoded = true;
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


        public byte[] BuildRLPRawEncoding()
        {
            var encodedData = new List<byte[]>();
            encodedData.AddRange(Data.Select(RLP.RLP.EncodeElement).ToArray());
            return RLP.RLP.EncodeList(encodedData.ToArray());
        }

        public byte[] BuildRLPEncoding()
        {
            var encodedData = new List<byte[]>();
            for (var i = 0; i < numberOfEncodingElements; i++)
                encodedData.Add(RLP.RLP.EncodeElement(Data[i]));

            byte[] v, r, s;

            if (signature != null)
            {
                //CalculateV
                v = RLP.RLP.EncodeElement(signature.V);
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
            rlpEncoded = BuildRLPEncoding();
            return rlpEncoded;
        }

        public byte[] GetRLPEncodedRaw()
        {
            EnsuredRPLDecoded();
            rlpRaw = BuildRLPRawEncoding();
            return rlpRaw;
        }

        public void RlpDecode()
        {
            var decodedList = RLP.RLP.Decode(GetRLPEncoded());
            var decodedData = new List<byte[]>();
            var decodedElements = (RLPCollection) decodedList[0];
            for (var i = 0; i < numberOfEncodingElements; i++)
                decodedData.Add(decodedElements[i].RLPData);
            // only parse signature in case is signed
            if (decodedElements[numberOfEncodingElements].RLPData != null)
            {
                //Decode Signature
                var v = decodedElements[numberOfEncodingElements].RLPData;
                var r = decodedElements[numberOfEncodingElements + 1].RLPData;
                var s = decodedElements[numberOfEncodingElements + 2].RLPData;

                signature = EthECDSASignatureFactory.FromComponents(r, s, v);
            }
            data = decodedData.ToArray();
            decoded = true;
        }

        public void AppendData(params byte[][] extraData)
        {
            var fullData = new List<byte[]>();
            fullData.AddRange(Data);
            fullData.AddRange(extraData);
            data = fullData.ToArray();
        }

        public void Sign(EthECKey key)
        {
            signature = key.SignAndCalculateV(RawHash);
            rlpEncoded = null;
        }

        public void Sign(EthECKey key, BigInteger chainId)
        {
            signature = key.SignAndCalculateV(RawHash, chainId);
            rlpEncoded = null;
        }

        public bool IsVSignatureForChain()
        {
            if(Signature == null) throw new Exception("Signature not initiated or calculatated");
            return Signature.IsVSignedForChain();
        }

        private void EnsuredRPLDecoded()
        {
            if (!decoded)
                RlpDecode();
        }
    }
}