using Nethereum.RLP;
using System;
using System.Collections.Generic;

namespace Nethereum.Model
{

    public class SignedData
    {
        public byte[][] Data { get; set; }
        public byte[] V { get; set; }
        public byte[] R { get; set; }
        public byte[] S { get; set; }

      
        public bool IsSigned()
        {
            return (V != null);
        }

        public ISignature GetSignature()
        {
            if (R != null && S != null)
            {
                return new Signature() { R = R, S = S, V = V };
            }
            return null;
        }

        public SignedData()
        {

        }

        public SignedData(byte[][] data)
        {
            Data = data;
        }

        public SignedData(byte[][] data, ISignature signature):this(data)
        {
           if(signature != null)
           {
                R = signature.R;
                S = signature.S;
                V = signature.V;
           }
        }
    }
}