using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AccountAbstraction.Contracts.Core.BaseSmartAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Core.BaseSmartAccount.ContractDefinition
{


    public partial class BaseSmartAccountDeployment : BaseSmartAccountDeploymentBase
    {
        public BaseSmartAccountDeployment() : base(BYTECODE) { }
        public BaseSmartAccountDeployment(string byteCode) : base(byteCode) { }
    }

    public class BaseSmartAccountDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public BaseSmartAccountDeploymentBase() : base(BYTECODE) { }
        public BaseSmartAccountDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class UpgradeInterfaceVersionFunction : UpgradeInterfaceVersionFunctionBase { }

    [Function("UPGRADE_INTERFACE_VERSION", "string")]
    public class UpgradeInterfaceVersionFunctionBase : FunctionMessage
    {

    }

    public partial class AddDepositFunction : AddDepositFunctionBase { }

    [Function("addDeposit")]
    public class AddDepositFunctionBase : FunctionMessage
    {

    }

    public partial class EntryPointFunction : EntryPointFunctionBase { }

    [Function("entryPoint", "address")]
    public class EntryPointFunctionBase : FunctionMessage
    {

    }

    public partial class ExecuteFunction : ExecuteFunctionBase { }

    [Function("execute", "bytes")]
    public class ExecuteFunctionBase : FunctionMessage
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class ExecuteBatchFunction : ExecuteBatchFunctionBase { }

    [Function("executeBatch", "bytes[]")]
    public class ExecuteBatchFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "targets", 1)]
        public virtual List<string> Targets { get; set; }
        [Parameter("uint256[]", "values", 2)]
        public virtual List<BigInteger> Values { get; set; }
        [Parameter("bytes[]", "datas", 3)]
        public virtual List<byte[]> Datas { get; set; }
    }

    public partial class ExecuteUserOpFunction : ExecuteUserOpFunctionBase { }

    [Function("executeUserOp")]
    public class ExecuteUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
    }

    public partial class GetDepositFunction : GetDepositFunctionBase { }

    [Function("getDeposit", "uint256")]
    public class GetDepositFunctionBase : FunctionMessage
    {

    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class ProxiableUUIDFunction : ProxiableUUIDFunctionBase { }

    [Function("proxiableUUID", "bytes32")]
    public class ProxiableUUIDFunctionBase : FunctionMessage
    {

    }

    public partial class UpgradeToAndCallFunction : UpgradeToAndCallFunctionBase { }

    [Function("upgradeToAndCall")]
    public class UpgradeToAndCallFunctionBase : FunctionMessage
    {
        [Parameter("address", "newImplementation", 1)]
        public virtual string NewImplementation { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }

    public partial class ValidateUserOpFunction : ValidateUserOpFunctionBase { }

    [Function("validateUserOp", "uint256")]
    public class ValidateUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
        [Parameter("uint256", "missingAccountFunds", 3)]
        public virtual BigInteger MissingAccountFunds { get; set; }
    }

    public partial class WithdrawDepositToFunction : WithdrawDepositToFunctionBase { }

    [Function("withdrawDepositTo")]
    public class WithdrawDepositToFunctionBase : FunctionMessage
    {
        [Parameter("address", "withdrawAddress", 1)]
        public virtual string WithdrawAddress { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class UpgradeInterfaceVersionOutputDTO : UpgradeInterfaceVersionOutputDTOBase { }

    [FunctionOutput]
    public class UpgradeInterfaceVersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class EntryPointOutputDTO : EntryPointOutputDTOBase { }

    [FunctionOutput]
    public class EntryPointOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }







    public partial class GetDepositOutputDTO : GetDepositOutputDTOBase { }

    [FunctionOutput]
    public class GetDepositOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ProxiableUUIDOutputDTO : ProxiableUUIDOutputDTOBase { }

    [FunctionOutput]
    public class ProxiableUUIDOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }







    public partial class InitializedEventDTO : InitializedEventDTOBase { }

    [Event("Initialized")]
    public class InitializedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "version", 1, false )]
        public virtual ulong Version { get; set; }
    }

    public partial class UpgradedEventDTO : UpgradedEventDTOBase { }

    [Event("Upgraded")]
    public class UpgradedEventDTOBase : IEventDTO
    {
        [Parameter("address", "implementation", 1, true )]
        public virtual string Implementation { get; set; }
    }

    public partial class AddressEmptyCodeError : AddressEmptyCodeErrorBase { }

    [Error("AddressEmptyCode")]
    public class AddressEmptyCodeErrorBase : IErrorDTO
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
    }

    public partial class ArrayLengthMismatchError : ArrayLengthMismatchErrorBase { }
    [Error("ArrayLengthMismatch")]
    public class ArrayLengthMismatchErrorBase : IErrorDTO
    {
    }

    public partial class ECDSAInvalidSignatureError : ECDSAInvalidSignatureErrorBase { }
    [Error("ECDSAInvalidSignature")]
    public class ECDSAInvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class ECDSAInvalidSignatureLengthError : ECDSAInvalidSignatureLengthErrorBase { }

    [Error("ECDSAInvalidSignatureLength")]
    public class ECDSAInvalidSignatureLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "length", 1)]
        public virtual BigInteger Length { get; set; }
    }

    public partial class ECDSAInvalidSignatureSError : ECDSAInvalidSignatureSErrorBase { }

    [Error("ECDSAInvalidSignatureS")]
    public class ECDSAInvalidSignatureSErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "s", 1)]
        public virtual byte[] S { get; set; }
    }

    public partial class ERC1967InvalidImplementationError : ERC1967InvalidImplementationErrorBase { }

    [Error("ERC1967InvalidImplementation")]
    public class ERC1967InvalidImplementationErrorBase : IErrorDTO
    {
        [Parameter("address", "implementation", 1)]
        public virtual string Implementation { get; set; }
    }

    public partial class ERC1967NonPayableError : ERC1967NonPayableErrorBase { }
    [Error("ERC1967NonPayable")]
    public class ERC1967NonPayableErrorBase : IErrorDTO
    {
    }

    public partial class ExecutionFailedError : ExecutionFailedErrorBase { }
    [Error("ExecutionFailed")]
    public class ExecutionFailedErrorBase : IErrorDTO
    {
    }

    public partial class FailedCallError : FailedCallErrorBase { }
    [Error("FailedCall")]
    public class FailedCallErrorBase : IErrorDTO
    {
    }

    public partial class InvalidInitializationError : InvalidInitializationErrorBase { }
    [Error("InvalidInitialization")]
    public class InvalidInitializationErrorBase : IErrorDTO
    {
    }

    public partial class NotInitializingError : NotInitializingErrorBase { }
    [Error("NotInitializing")]
    public class NotInitializingErrorBase : IErrorDTO
    {
    }

    public partial class OnlyEntryPointError : OnlyEntryPointErrorBase { }
    [Error("OnlyEntryPoint")]
    public class OnlyEntryPointErrorBase : IErrorDTO
    {
    }

    public partial class OnlyOwnerError : OnlyOwnerErrorBase { }
    [Error("OnlyOwner")]
    public class OnlyOwnerErrorBase : IErrorDTO
    {
    }

    public partial class OnlyOwnerOrEntryPointError : OnlyOwnerOrEntryPointErrorBase { }
    [Error("OnlyOwnerOrEntryPoint")]
    public class OnlyOwnerOrEntryPointErrorBase : IErrorDTO
    {
    }

    public partial class UUPSUnauthorizedCallContextError : UUPSUnauthorizedCallContextErrorBase { }
    [Error("UUPSUnauthorizedCallContext")]
    public class UUPSUnauthorizedCallContextErrorBase : IErrorDTO
    {
    }

    public partial class UUPSUnsupportedProxiableUUIDError : UUPSUnsupportedProxiableUUIDErrorBase { }

    [Error("UUPSUnsupportedProxiableUUID")]
    public class UUPSUnsupportedProxiableUUIDErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "slot", 1)]
        public virtual byte[] Slot { get; set; }
    }
}
