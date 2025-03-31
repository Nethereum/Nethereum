using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Model
{
    public class Authorisation7702
    {
        public BigInteger ChainId { get; set; }
        public string Address { get; set; }
        public BigInteger Nonce { get; set; }
    }
       
    public class Authorisation7702Signed: Authorisation7702, ISignature
    {

        public Authorisation7702Signed()
        {
        }

        public Authorisation7702Signed(BigInteger chainId, string address, BigInteger nonce, byte[] r, byte[] s, byte[] v)
        {
            ChainId = chainId;
            Address = address;
            Nonce = nonce;
            V = v;
            R = r;
            S = s;
        }

        public Authorisation7702Signed(Authorisation7702 authorisation, Signature signature)
        {
            ChainId = authorisation.ChainId;
            Address = authorisation.Address;
            Nonce = authorisation.Nonce;
            V = signature.V;
            R = signature.R;
            S = signature.S;
        }

        public Authorisation7702Signed(Authorisation7702 authorisation, byte[] r, byte[] s, byte[] v)
        {
            ChainId = authorisation.ChainId;
            Address = authorisation.Address;
            Nonce = authorisation.Nonce;
            V = v;
            R = r;
            S = s;
        }
        /// <summary>
        /// YParity value / V
        /// </summary>
        public byte[] V { get; set; }
        public byte[] R { get; set; }
        public byte[] S { get; set; }

    }
}