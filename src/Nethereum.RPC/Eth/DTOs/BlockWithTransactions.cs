using System.Runtime.Serialization;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    ///     Block including transaction objects
    /// </summary>
    public class BlockWithTransactions : Block
    {
        /// <summary>
        ///     Array - Array of transaction objects
        /// </summary>
        [DataMember(Name = "transactions")]
        public Transaction[] Transactions { get; set; }
    }
}