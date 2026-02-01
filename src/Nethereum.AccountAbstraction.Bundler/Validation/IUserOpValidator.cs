using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    /// <summary>
    /// Interface for validating UserOperations before adding to mempool.
    /// </summary>
    public interface IUserOpValidator
    {
        /// <summary>
        /// Validates a UserOperation for inclusion in the mempool.
        /// This performs both structural validation and simulation.
        /// </summary>
        /// <param name="userOp">The packed user operation to validate</param>
        /// <param name="entryPoint">The EntryPoint address</param>
        /// <returns>Validation result with gas estimates if successful</returns>
        Task<UserOpValidationResult> ValidateAsync(PackedUserOperation userOp, string entryPoint);

        /// <summary>
        /// Performs only structural validation (no simulation).
        /// Faster but less thorough.
        /// </summary>
        Task<UserOpValidationResult> ValidateStructureAsync(PackedUserOperation userOp, string entryPoint);

        /// <summary>
        /// Simulates the UserOperation to validate execution and get gas estimates.
        /// </summary>
        Task<UserOpValidationResult> SimulateValidationAsync(PackedUserOperation userOp, string entryPoint);

        /// <summary>
        /// Estimates gas for a UserOperation.
        /// </summary>
        Task<UserOpValidationResult> EstimateGasAsync(UserOperation userOp, string entryPoint);
    }

    /// <summary>
    /// Result of simulation including trace information.
    /// </summary>
    public class SimulationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public byte[]? ReturnData { get; set; }
        public ulong GasUsed { get; set; }
        public StorageAccess[]? StorageAccesses { get; set; }
        public OpcodeAccess[]? OpcodeAccesses { get; set; }
        public string[]? CalledContracts { get; set; }
    }

    /// <summary>
    /// Storage slot access during simulation.
    /// </summary>
    public class StorageAccess
    {
        public string Address { get; set; } = null!;
        public string Slot { get; set; } = null!;
        public bool IsWrite { get; set; }
    }

    /// <summary>
    /// Banned opcode access during simulation.
    /// </summary>
    public class OpcodeAccess
    {
        public string Opcode { get; set; } = null!;
        public string Address { get; set; } = null!;
        public int Depth { get; set; }
    }
}
