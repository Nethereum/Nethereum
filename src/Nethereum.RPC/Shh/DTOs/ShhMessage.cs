using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh.DTOs
{
    public class ShhMessage
    { 
        public string Hash { get; set; } 
        public string Padding { get; set; } 
        public string Payload { get; set; } 
        public decimal Pow { get; set; } 
        public string RecipientPublicKey { get; set; } 
        public string Sig { get; set; } 
        public long Timestamp { get; set; } 
        public string Topic { get; set; } 
        public long Ttl { get; set; }
    }
}
