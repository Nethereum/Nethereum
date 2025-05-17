using System.Collections.Generic;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class TracerLogDto
    {
        public string Address { get; set; }
        public string Data { get; set; }
        public List<object> Topics { get; set; }
        public HexBigInteger Position { get; set; }

    }
}