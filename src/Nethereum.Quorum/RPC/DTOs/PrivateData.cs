using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class PrivateData
    {
        /// <summary>
        /// an array of the recipients’ base64-encoded public keys
        /// </summary>
        [DataMember(Name = "privateFor")]
        public List<string> PrivateFor { get; set; }

        /// <summary>
        /// (optional) the sending party’s base64-encoded public key to use (Privacy Manager default if not provided)
        /// </summary>
        [DataMember(Name = "privateFrom")]
        public string PrivateFrom { get; set; }

        /// <summary>
        /// 0 for SP (default if not provided), 1 for PP, 2 for MPP, and 3 for PSV transactions
        /// </summary>
        [DataMember(Name = "privacyFlag")]
        public int PrivacyFlag { get; set; } = 0;

        /// <summary>
        /// An array of the recipients’ base64-encoded public keys
        /// </summary>
        [DataMember(Name = "mandatoryFor")]
        public List<string> MandatoryFor { get; set; }


    }

}
