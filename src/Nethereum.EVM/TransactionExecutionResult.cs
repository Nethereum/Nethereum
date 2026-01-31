using System.Collections.Generic;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.EVM
{
    public class TransactionExecutionResult
    {
        public bool Success { get; set; }
        public BigInteger GasUsed { get; set; }
        public BigInteger GasRefund { get; set; }
        public BigInteger EffectiveGasUsed { get; set; }
        public byte[] ReturnData { get; set; }
        public string RevertReason { get; set; }
        public List<FilterLog> Logs { get; set; } = new List<FilterLog>();
        public byte[] StateRoot { get; set; }
        public string ContractAddress { get; set; }
        public string Error { get; set; }
        public bool IsValidationError { get; set; }
        public List<ProgramTrace> Traces { get; set; }
        public List<string> CreatedAccounts { get; set; } = new List<string>();
        public List<string> DeletedAccounts { get; set; } = new List<string>();
        public List<CallInput> InnerCalls { get; set; } = new List<CallInput>();
        public Dictionary<string, List<ProgramInstruction>> InnerContractCodeCalls { get; set; } = new Dictionary<string, List<ProgramInstruction>>();
        public ProgramResult ProgramResult { get; set; }
    }

    public enum TransactionError
    {
        None,
        InsufficientMaxFeePerGas,
        PriorityGreaterThanMaxFee,
        InsufficientBalance,
        GasAllowanceExceeded,
        IntrinsicGasTooLow,
        NonceIsMax,
        SenderNotEOA,
        InitcodeSizeExceeded,
        Type3TxContractCreation,
        Type3TxZeroBlobs,
        Type3TxBlobCountExceeded,
        Type3TxInvalidBlobVersionedHash,
        AddressCollision,
        InvalidEFPrefix,
        MaxCodeSizeExceeded,
        OutOfGas,
        Reverted,
    }
}
