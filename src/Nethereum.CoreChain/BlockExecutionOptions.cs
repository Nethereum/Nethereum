using System.Collections.Generic;
using System.Numerics;
using Nethereum.Documentation;
using Nethereum.Util;
namespace Nethereum.CoreChain
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "BlockExecutionRole — building a block vs validating one")]
    public enum BlockExecutionRole
    {
        Building,

        Validating,

        Simulating
    }

    public sealed class SimulateCallOptions
    {
        public bool Validation { get; init; }

        public bool TraceTransfers { get; init; }

        public Dictionary<string, EvmUInt256> ExpectedNonces { get; init; } = new Dictionary<string, EvmUInt256>();

        public IReadOnlyDictionary<string, int>? PrecompileRelocations { get; set; }
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "BlockExecutionOptions — what a caller hands BlockExecutor.ExecuteAsync")]
    public sealed class BlockExecutionOptions
    {
        public bool ReadOnly { get; init; }

        public bool CaptureWitness { get; init; }

        public byte[]? ParentBeaconBlockRoot { get; init; }

        public int? TraceTxIndex { get; init; }

        public BlockExecutionRole Role { get; init; } = BlockExecutionRole.Building;

        public SimulateCallOptions? SimulateCallOptions { get; init; }

        public BigInteger SimulateGlobalGasUsedBefore { get; init; }

        /// <summary>
        /// The block access list as the block DECLARES it, when the caller has one - a payload
        /// from the Engine API, a body from eth/71. EIP-7928 §Engine API: engine_newPayloadV5
        /// "Returns INVALID if access list is malformed or doesn't match", and those are two
        /// separate verdicts. Recomputing and comparing hashes answers "doesn't match"; only the
        /// declared list itself can answer "malformed".
        /// </summary>
        public List<Nethereum.Model.AccountChanges>? DeclaredBlockAccessList { get; init; }
    }
}
