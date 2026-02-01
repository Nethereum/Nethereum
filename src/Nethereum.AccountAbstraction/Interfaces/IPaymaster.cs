using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;

namespace Nethereum.AccountAbstraction.Interfaces
{
    /// <summary>
    /// Result from paymaster validation containing context and validation data.
    /// </summary>
    public class PaymasterValidationResult
    {
        /// <summary>
        /// Context data to pass to postOp. Can be empty if no postOp needed.
        /// </summary>
        public byte[] Context { get; set; } = System.Array.Empty<byte>();

        /// <summary>
        /// Validation data packed as: (sigFailed: 1 bit) | (validUntil: 48 bits) | (validAfter: 48 bits)
        /// Return 0 for successful validation with no time restrictions.
        /// </summary>
        public BigInteger ValidationData { get; set; } = BigInteger.Zero;
    }

    /// <summary>
    /// Defines the mode for postOp callback.
    /// </summary>
    public enum PostOpMode
    {
        /// <summary>
        /// User operation succeeded.
        /// </summary>
        OpSucceeded = 0,

        /// <summary>
        /// User operation reverted, still has to pay.
        /// </summary>
        OpReverted = 1,

        /// <summary>
        /// postOp itself reverted (only in 2nd call, which should not happen).
        /// </summary>
        PostOpReverted = 2
    }

    /// <summary>
    /// ERC-4337 IPaymaster interface - allows third-party contracts to sponsor gas for user operations.
    /// Paymasters can implement custom logic for gas sponsorship, including:
    /// - Token-based payment (user pays in ERC20, paymaster pays gas)
    /// - Subscription/quota systems
    /// - Sponsored transactions for specific contracts
    /// - Rate limiting and fraud prevention
    /// </summary>
    public interface IPaymaster
    {
        /// <summary>
        /// Validates a UserOperation for gas sponsorship.
        /// Called by EntryPoint during the verification loop.
        /// </summary>
        /// <param name="userOp">The packed user operation</param>
        /// <param name="userOpHash">Hash of the user operation</param>
        /// <param name="maxCost">Maximum cost the paymaster may need to pay</param>
        /// <returns>Context for postOp and validation data</returns>
        Task<PaymasterValidationResult> ValidatePaymasterUserOpAsync(
            PackedUserOperation userOp,
            byte[] userOpHash,
            BigInteger maxCost);

        /// <summary>
        /// Post-operation handler called after the user operation execution.
        /// Can be used to charge the user (e.g., in tokens) or perform cleanup.
        /// </summary>
        /// <param name="mode">The mode indicating success/failure of the operation</param>
        /// <param name="context">Context data returned from validatePaymasterUserOp</param>
        /// <param name="actualGasCost">Actual gas cost of the operation</param>
        /// <param name="actualUserOpFeePerGas">Actual fee per gas used</param>
        Task PostOpAsync(
            PostOpMode mode,
            byte[] context,
            BigInteger actualGasCost,
            BigInteger actualUserOpFeePerGas);

        /// <summary>
        /// Gets the deposit balance for this paymaster in the EntryPoint.
        /// </summary>
        Task<BigInteger> GetDepositAsync();

        /// <summary>
        /// Adds stake for this paymaster.
        /// </summary>
        /// <param name="unstakeDelaySec">The unstake delay in seconds</param>
        Task<string> AddStakeAsync(uint unstakeDelaySec);

        /// <summary>
        /// Deposits funds for this paymaster to the EntryPoint.
        /// </summary>
        Task<string> DepositAsync(BigInteger amount);

        /// <summary>
        /// Withdraws funds from the EntryPoint.
        /// </summary>
        Task<string> WithdrawToAsync(string withdrawAddress, BigInteger amount);
    }
}
