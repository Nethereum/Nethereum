using System.Numerics;

namespace Nethereum.Contracts.CQS
{
    /// <summary>
    /// Base class used for the messages and descriptions of Deployments and Functions (Queries, Transactions)
    /// </summary>
    public abstract class ContractMessageBase
    {
        /// <summary>
        /// The Ether Amount to Send to the contract in Wei
        /// </summary>
        /// <remarks>
        /// The smart contract function will need to mark as payable in latest versions of solidity
        /// </remarks>
        public BigInteger AmountToSend { get; set; }
        /// <summary>
        /// The Maximum Gas to use for the transaction
        /// </summary>
        public BigInteger? Gas { get; set; }
        /// <summary>
        /// The Gas price per unit of Gas
        /// </summary>
        public BigInteger? GasPrice { get; set; }
        /// <summary>
        /// The address of the sender
        /// </summary>
        public string FromAddress { get; set; }
        /// <summary>
        /// The unique number for the contract message transaction
        /// </summary>
        /// <remarks>
        /// Nonces are ordered based on the number of transactions from an specific account,
        /// so the next nonce for the next transaction will be the total number of transactions for that account
        /// </remarks>
        public BigInteger? Nonce { get; set; }
    }
}