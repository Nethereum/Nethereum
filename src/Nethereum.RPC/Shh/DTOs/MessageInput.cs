using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh.DTOs
{
    public class MessageInput
    {
        //ID of symmetric key for message encryption.
        public string SymKeyID { get; set; } 
        //public key for message encryption.
        public string PubKey { get; set; }
        //(optional): ID of the signing key.
        public string Sig { get; set; }
        //4 Bytes(mandatory when key is symmetric) : Message topic.
        public string Topic { get; set; }
        //Payload to be encrypted.
        public string Payload { get; set; }
        //Optional padding (byte array of arbitrary length).
        public string Padding { get; set; }
        //Time-to-live in seconds.
        public long Ttl { get; set; }
        // Maximal time in seconds to be spent on proof of work.
        public long PowTime { get; set; }
        // Minimal PoW target required for this message.
        public long PowTarget { get; set; }
        //(optional): Optional peer ID (for peer-to-peer message only).
        public string TargetPeer { get; set; }    
    }
}
