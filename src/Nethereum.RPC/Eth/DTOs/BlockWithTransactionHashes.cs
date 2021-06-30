using System.Runtime.Serialization;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    ///     Block including just the transaction hashes
    /// </summary>
    public class BlockWithTransactionHashes : Block
    {
        /// <summary>
        ///     Array - Array of transaction hashes
        /// </summary>
        [DataMember(Name = "transactions")]
        public string[] TransactionHashes { get; set; }
    }
}