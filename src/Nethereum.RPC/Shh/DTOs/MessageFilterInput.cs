using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh.DTOs
{
    public class MessageFilterInput
    {
        //ID of symmetric key for message decryption.
        public string SymKeyID { get; set; }
        //ID of private (asymmetric) key for message decryption.
        public string PrivateKeyID { get; set; }
        //(optional): Public key of the signature.
        public string Sig { get; set; }
        //(optional): Minimal PoW requirement for incoming messages.
        public int? MinPow { get; set; }
        //(optional when asym key): Array of possible topics(or partial topics).
        public object[] Topics { get; set; }
        //(optional): Indicates if this filter allows processing of direct peer-to-peer messages (which are not to be forwarded any further, because they might be expired). This might be the case in some very rare cases, e.g. if you intend to communicate to MailServers, etc.
        public bool? AllowP2P { get; set; }
    }
}
