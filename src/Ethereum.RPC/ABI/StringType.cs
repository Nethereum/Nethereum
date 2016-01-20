using System.Collections.Generic;
using Ethereum.RPC.ABI;

namespace Ethereum.RPC.ABI
{
    public class StringType : ABIType
    {

        public StringType() : base("string")
        {
            this.Decoder = new StringTypeDecoder();
            this.Encoder = new StringTypeEncoder();
        }

         public override int FixedSize => -1;
    }
}