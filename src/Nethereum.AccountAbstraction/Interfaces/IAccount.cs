using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;

namespace Nethereum.AccountAbstraction.Interfaces
{
    /// <summary>
    /// ERC-4337 IAccount interface - the standard interface that all smart accounts must implement.
    /// This interface defines the core validation and execution methods required by the EntryPoint.
    /// </summary>
    public interface IAccount
    {
        /// <summary>
        /// Validates a UserOperation and returns validation data.
        /// Called by EntryPoint during the verification loop.
        /// </summary>
        /// <param name="userOp">The packed user operation to validate</param>
        /// <param name="userOpHash">The hash of the user operation (used for signature verification)</param>
        /// <param name="missingAccountFunds">The amount of funds the account needs to pay to the EntryPoint</param>
        /// <returns>
        /// Validation data packed as: (sigFailed: 1 bit) | (validUntil: 48 bits) | (validAfter: 48 bits) | (aggregator: 160 bits)
        /// Return 0 for successful validation with no time restrictions.
        /// Return 1 for signature validation failure.
        /// </returns>
        Task<BigInteger> ValidateUserOpAsync(
            PackedUserOperation userOp,
            byte[] userOpHash,
            BigInteger missingAccountFunds);

        /// <summary>
        /// Executes a single call from this account.
        /// Can only be called by EntryPoint or account owner.
        /// </summary>
        Task<string> ExecuteAsync(string target, BigInteger value, byte[] data);

        /// <summary>
        /// Executes a batch of calls from this account.
        /// Can only be called by EntryPoint or account owner.
        /// </summary>
        Task<string> ExecuteBatchAsync(List<Call> calls);

        /// <summary>
        /// Gets the EntryPoint address this account is bound to.
        /// </summary>
        Task<string> GetEntryPointAsync();

        /// <summary>
        /// Gets the current nonce for this account from the EntryPoint.
        /// </summary>
        Task<BigInteger> GetNonceAsync();
    }
}
