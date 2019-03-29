using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.Signer
{
    public class RLPSigner
    {
        private readonly int numberOfEncodingElements;
        private byte[] rlpSignedEncoded;
        private byte[] rlpRawWitNoSignature;

        public RLPSigner(byte[] rawData, int numberOfEncodingElements)
        {
            rlpSignedEncoded = rawData;
            this.numberOfEncodingElements = numberOfEncodingElements;
            Decode();
        }

        public RLPSigner(byte[][] data) : this(data, data.Length)
        {
        }

        public RLPSigner(byte[][] data, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.Data = data;
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte v) : this(data, r, s, v, data.Length)
        {
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte v, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.Data = data;
            Signature = EthECDSASignatureFactory.FromComponents(r, s, v);
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte[] v) : this(data, r, s, v, data.Length)
        {
        }

        public RLPSigner(byte[][] data, byte[] r, byte[] s, byte[] v, int numberOfEncodingElements)
        {
            this.numberOfEncodingElements = numberOfEncodingElements;
            this.Data = data;
            Signature = EthECDSASignatureFactory.FromComponents(r, s, v);
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

        public EthECDSASignature Signature { get; private set; }

        public byte[] GetRLPEncoded()
        {
            if (rlpSignedEncoded != null) return rlpSignedEncoded;
            rlpSignedEncoded = RLPEncoder.EncodeSigned(new SignedData(Data, Signature), numberOfEncodingElements);
            return rlpSignedEncoded;
        }

        public byte[] GetRLPEncodedRaw()
        {
            rlpRawWitNoSignature = RLP.RLP.EncodeElementsAndList(Data);
            return rlpRawWitNoSignature;
        }

        public void AppendData(params byte[][] extraData)
        {
            var fullData = new List<byte[]>();
            fullData.AddRange(Data);
            fullData.AddRange(extraData);
            Data = fullData.ToArray();
        }

        public void Sign(EthECKey key)
        {
            Signature = key.SignAndCalculateV(RawHash);
            rlpSignedEncoded = null;
        }

        public void Sign(EthECKey key, BigInteger chainId)
        {
            Signature = key.SignAndCalculateV(RawHash, chainId);
            rlpSignedEncoded = null;
        }

        public void SetSignature(EthECDSASignature signature)
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
            var signedData = RLPDecoder.DecodeSigned(rlpSignedEncoded, numberOfEncodingElements);
            Data = signedData.Data;
            Signature = signedData.GetSignature();
        }
    }
}