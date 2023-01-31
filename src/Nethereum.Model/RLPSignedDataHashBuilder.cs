using System;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class RLPSignedDataHashBuilder
    {
        private readonly int numberOfEncodingElements;
        private byte[] rlpSignedEncoded;
        private byte[] rlpRawWithNoSignature;
        public ISignature Signature { get; private set; }    

        public RLPSignedDataHashBuilder(byte[] rawData, int numberOfEncodingElements)
        {
            rlpSignedEncoded = rawData;
            this.numberOfEncodingElements = numberOfEncodingElements;
            Decode();
        }

        public RLPSignedDataHashBuilder(byte[][] data) : this(data, data.Length)
        {
        }

        public RLPSignedDataHashBuilder(byte[][] data, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.Data = data;
        }

        public RLPSignedDataHashBuilder(byte[][] data, byte[] r, byte[] s, byte v) : this(data, r, s, v, data.Length)
        {
        }

        public RLPSignedDataHashBuilder(byte[][] data, byte[] r, byte[] s, byte v, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.Data = data;
            if (r != null && s != null)
            {
                this.Signature = new Signature(r, s, new byte[] {v});
            }
        }

        public RLPSignedDataHashBuilder(byte[][] data, byte[] r, byte[] s, byte[] v) : this(data, r, s, v, data.Length)
        {
        }

        public RLPSignedDataHashBuilder(byte[][] data, byte[] r, byte[] s, byte[] v, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.Data = data;
            if (r != null && s != null && v != null)
            {
                this.Signature = new Signature(r, s, v);
            }
        }

        public byte[] RawHash
        {
            get
            {
                var plainMsg = GetRLPEncodedRaw();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        public byte[] Hash
        {
            get
            {
                var plainMsg = GetRLPEncoded();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        public byte[][] Data { get; private set; }

        public byte[] GetRLPEncoded()
        {
            if (rlpSignedEncoded != null) return rlpSignedEncoded;
            rlpSignedEncoded = RLPSignedDataEncoder.EncodeSigned(new SignedData(Data, Signature), numberOfEncodingElements);
            return rlpSignedEncoded;
        }

        public byte[] GetRLPEncodedRaw()
        {
            rlpRawWithNoSignature = RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(Data);
            return rlpRawWithNoSignature;
        }

        public void AppendData(params byte[][] extraData)
        {
            var fullData = new List<byte[]>();
            fullData.AddRange(Data);
            fullData.AddRange(extraData);
            Data = fullData.ToArray();
        }

        public void SetSignature(ISignature signature)
        {
            Signature = signature;
            rlpSignedEncoded = null;
        }

        public bool IsVSignatureForChain()
        {
            if(Signature == null) throw new Exception("Signature not initiated or calculated");
            return Signature.IsVSignedForChain();
        }

        private void Decode()
        {
            var signedData = RLPSignedDataDecoder.DecodeSigned(rlpSignedEncoded, numberOfEncodingElements);
            Data = signedData.Data;
            Signature = signedData.GetSignature();
        }
    }
}