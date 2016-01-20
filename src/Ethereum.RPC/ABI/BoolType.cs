using System.Numerics;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{
    public class BoolType : ABIType
    {
        public BoolType() : base("bool")
        {
            Decoder = new BoolTypeDecoder();
            Encoder = new BoolTypeEncoder();
        }
   
    }
}