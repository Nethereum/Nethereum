using System.Collections.Generic;

namespace Nethereum.ABI.Util.RLP
{
    public class RLPCollection : List<IRLPElement>, IRLPElement
    {
        public byte[] RLPData { get; set; }
    }
}