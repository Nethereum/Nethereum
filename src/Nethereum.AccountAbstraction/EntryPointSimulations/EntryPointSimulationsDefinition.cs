using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;

namespace Nethereum.AccountAbstraction.EntryPointSimulations
{
    [Function("simulateValidation", typeof(SimulateValidationOutputDTO))]
    public class SimulateValidationFunction : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; } = null!;
    }

    [Function("simulateHandleOp", typeof(SimulateHandleOpOutputDTO))]
    public class SimulateHandleOpFunction : FunctionMessage
    {
        [Parameter("tuple", "op", 1)]
        public virtual PackedUserOperation Op { get; set; } = null!;

        [Parameter("address", "target", 2)]
        public virtual string Target { get; set; } = null!;

        [Parameter("bytes", "targetCallData", 3)]
        public virtual byte[] TargetCallData { get; set; } = null!;
    }

    [FunctionOutput]
    public class SimulateValidationOutputDTO : IFunctionOutputDTO
    {
        [Parameter("tuple", "", 1)]
        public virtual ValidationResult Result { get; set; } = null!;
    }

    [FunctionOutput]
    public class SimulateHandleOpOutputDTO : IFunctionOutputDTO
    {
        [Parameter("tuple", "", 1)]
        public virtual ExecutionResult Result { get; set; } = null!;
    }

    public class ValidationResult
    {
        [Parameter("tuple", "returnInfo", 1)]
        public virtual ReturnInfo ReturnInfo { get; set; } = null!;

        [Parameter("tuple", "senderInfo", 2)]
        public virtual StakeInfo SenderInfo { get; set; } = null!;

        [Parameter("tuple", "factoryInfo", 3)]
        public virtual StakeInfo FactoryInfo { get; set; } = null!;

        [Parameter("tuple", "paymasterInfo", 4)]
        public virtual StakeInfo PaymasterInfo { get; set; } = null!;

        [Parameter("tuple", "aggregatorInfo", 5)]
        public virtual AggregatorStakeInfo AggregatorInfo { get; set; } = null!;
    }

    public class ReturnInfo
    {
        [Parameter("uint256", "preOpGas", 1)]
        public virtual BigInteger PreOpGas { get; set; }

        [Parameter("uint256", "prefund", 2)]
        public virtual BigInteger Prefund { get; set; }

        [Parameter("uint256", "accountValidationData", 3)]
        public virtual BigInteger AccountValidationData { get; set; }

        [Parameter("uint256", "paymasterValidationData", 4)]
        public virtual BigInteger PaymasterValidationData { get; set; }

        [Parameter("bytes", "paymasterContext", 5)]
        public virtual byte[] PaymasterContext { get; set; } = null!;
    }

    public class StakeInfo
    {
        [Parameter("uint256", "stake", 1)]
        public virtual BigInteger Stake { get; set; }

        [Parameter("uint256", "unstakeDelaySec", 2)]
        public virtual BigInteger UnstakeDelaySec { get; set; }
    }

    public class AggregatorStakeInfo
    {
        [Parameter("address", "aggregator", 1)]
        public virtual string Aggregator { get; set; } = null!;

        [Parameter("tuple", "stakeInfo", 2)]
        public virtual StakeInfo StakeInfo { get; set; } = null!;
    }

    public class ExecutionResult
    {
        [Parameter("uint256", "preOpGas", 1)]
        public virtual BigInteger PreOpGas { get; set; }

        [Parameter("uint256", "paid", 2)]
        public virtual BigInteger Paid { get; set; }

        [Parameter("uint256", "accountValidationData", 3)]
        public virtual BigInteger AccountValidationData { get; set; }

        [Parameter("uint256", "paymasterValidationData", 4)]
        public virtual BigInteger PaymasterValidationData { get; set; }

        [Parameter("bool", "targetSuccess", 5)]
        public virtual bool TargetSuccess { get; set; }

        [Parameter("bytes", "targetResult", 6)]
        public virtual byte[] TargetResult { get; set; } = null!;
    }
}
