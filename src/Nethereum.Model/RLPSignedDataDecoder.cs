using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model
{
    internal sealed class SignedRlpFields
    {
        public SignedRlpFields(byte[][] data)
        {
            Data = data;
        }

        public byte[][] Data { get; }
        public bool Signed { get; private set; }
        public byte[] V { get; private set; }
        public byte[] R { get; private set; }
        public byte[] S { get; private set; }

        public void SetSignature(byte[] v, byte[] r, byte[] s)
        {
            Signed = true;
            V = v;
            R = r;
            S = s;
        }

        public Signature LegacySignature()
        {
            if (!Signed || R.Length == 0 || S.Length == 0) return null;
            return RLPSignedDataDecoder.SignatureOrNull(V, R, S);
        }
    }

    public class RLPSignedDataDecoder
    {
        public static SignedData DecodeSigned(byte[] rawdata, int numberOfEncodingElements)
        {
            var fields = ReadFields(rawdata, numberOfEncodingElements);
            var signature = fields.Signed ? SignatureOrNull(fields.V, fields.R, fields.S) : null;
            return new SignedData(fields.Data, signature);
        }

        internal static SignedRlpFields ReadFields(byte[] rawdata, int numberOfEncodingElements)
        {
            var reader = new RlpReader(rawdata);
            var scope = reader.ReadListHeader();
            var data = new byte[numberOfEncodingElements][];
            for (var i = 0; i < numberOfEncodingElements; i++)
            {
                var item = reader.ReadBytes();
                data[i] = item.Length == 0 ? null : item;
            }

            var fields = new SignedRlpFields(data);
            if (!reader.IsAtEnd(scope))
            {
                var v = reader.ReadBytes();
                var r = reader.ReadBytes();
                var s = reader.ReadBytes();
                fields.SetSignature(v, r, s);
            }

            reader.EnsureEnd(scope);
            reader.EnsureEnd();
            return fields;
        }

        internal static Signature SignatureOrNull(byte[] v, byte[] r, byte[] s)
        {
            if (r.Length == 0) return null;
            return new Signature(r, s.Length == 0 ? null : s, v.Length == 0 ? new byte[] { 0 } : v);
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
