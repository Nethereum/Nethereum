using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.ETHRegistrarController.ContractDefinition
{

    public partial class MinRegistrationDurationFunction : MinRegistrationDurationFunctionBase { }

    [Function("MIN_REGISTRATION_DURATION", "uint256")]
    public class MinRegistrationDurationFunctionBase : FunctionMessage
    {

    }

    public partial class AvailableFunction : AvailableFunctionBase { }

    [Function("available", "bool")]
    public class AvailableFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class CommitFunction : CommitFunctionBase { }

    [Function("commit")]
    public class CommitFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "commitment", 1)]
        public virtual byte[] Commitment { get; set; }
    }

    public partial class CommitmentsFunction : CommitmentsFunctionBase { }

    [Function("commitments", "uint256")]
    public class CommitmentsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class IsOwnerFunction : IsOwnerFunctionBase { }

    [Function("isOwner", "bool")]
    public class IsOwnerFunctionBase : FunctionMessage
    {

    }

    public partial class MakeCommitmentFunction : MakeCommitmentFunctionBase { }

    [Function("makeCommitment", "bytes32")]
    public class MakeCommitmentFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "secret", 3)]
        public virtual byte[] Secret { get; set; }
    }

    public partial class MakeCommitmentWithConfigFunction : MakeCommitmentWithConfigFunctionBase { }

    [Function("makeCommitmentWithConfig", "bytes32")]
    public class MakeCommitmentWithConfigFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "secret", 3)]
        public virtual byte[] Secret { get; set; }
        [Parameter("address", "resolver", 4)]
        public virtual string Resolver { get; set; }
        [Parameter("address", "addr", 5)]
        public virtual string Addr { get; set; }
    }

    public partial class MaxCommitmentAgeFunction : MaxCommitmentAgeFunctionBase { }

    [Function("maxCommitmentAge", "uint256")]
    public class MaxCommitmentAgeFunctionBase : FunctionMessage
    {

    }

    public partial class MinCommitmentAgeFunction : MinCommitmentAgeFunctionBase { }

    [Function("minCommitmentAge", "uint256")]
    public class MinCommitmentAgeFunctionBase : FunctionMessage
    {

    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class RegisterFunction : RegisterFunctionBase { }

    [Function("register")]
    public class RegisterFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "duration", 3)]
        public virtual BigInteger Duration { get; set; }
        [Parameter("bytes32", "secret", 4)]
        public virtual byte[] Secret { get; set; }
    }

    public partial class RegisterWithConfigFunction : RegisterWithConfigFunctionBase { }

    [Function("registerWithConfig")]
    public class RegisterWithConfigFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "duration", 3)]
        public virtual BigInteger Duration { get; set; }
        [Parameter("bytes32", "secret", 4)]
        public virtual byte[] Secret { get; set; }
        [Parameter("address", "resolver", 5)]
        public virtual string Resolver { get; set; }
        [Parameter("address", "addr", 6)]
        public virtual string Addr { get; set; }
    }

    public partial class RenewFunction : RenewFunctionBase { }

    [Function("renew")]
    public class RenewFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("uint256", "duration", 2)]
        public virtual BigInteger Duration { get; set; }
    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class RentPriceFunction : RentPriceFunctionBase { }

    [Function("rentPrice", "uint256")]
    public class RentPriceFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("uint256", "duration", 2)]
        public virtual BigInteger Duration { get; set; }
    }

    public partial class SetCommitmentAgesFunction : SetCommitmentAgesFunctionBase { }

    [Function("setCommitmentAges")]
    public class SetCommitmentAgesFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_minCommitmentAge", 1)]
        public virtual BigInteger MinCommitmentAge { get; set; }
        [Parameter("uint256", "_maxCommitmentAge", 2)]
        public virtual BigInteger MaxCommitmentAge { get; set; }
    }

    public partial class SetPriceOracleFunction : SetPriceOracleFunctionBase { }

    [Function("setPriceOracle")]
    public class SetPriceOracleFunctionBase : FunctionMessage
    {
        [Parameter("address", "_prices", 1)]
        public virtual string Prices { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class ValidFunction : ValidFunctionBase { }

    [Function("valid", "bool")]
    public class ValidFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class WithdrawFunction : WithdrawFunctionBase { }

    [Function("withdraw")]
    public class WithdrawFunctionBase : FunctionMessage
    {

    }

    public partial class NameRegisteredEventDTO : NameRegisteredEventDTOBase { }

    [Event("NameRegistered")]
    public class NameRegisteredEventDTOBase : IEventDTO
    {
        [Parameter("string", "name", 1, false )]
        public virtual string Name { get; set; }
        [Parameter("bytes32", "label", 2, true )]
        public virtual byte[] Label { get; set; }
        [Parameter("address", "owner", 3, true )]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "cost", 4, false )]
        public virtual BigInteger Cost { get; set; }
        [Parameter("uint256", "expires", 5, false )]
        public virtual BigInteger Expires { get; set; }
    }

    public partial class NameRenewedEventDTO : NameRenewedEventDTOBase { }

    [Event("NameRenewed")]
    public class NameRenewedEventDTOBase : IEventDTO
    {
        [Parameter("string", "name", 1, false )]
        public virtual string Name { get; set; }
        [Parameter("bytes32", "label", 2, true )]
        public virtual byte[] Label { get; set; }
        [Parameter("uint256", "cost", 3, false )]
        public virtual BigInteger Cost { get; set; }
        [Parameter("uint256", "expires", 4, false )]
        public virtual BigInteger Expires { get; set; }
    }

    public partial class NewPriceOracleEventDTO : NewPriceOracleEventDTOBase { }

    [Event("NewPriceOracle")]
    public class NewPriceOracleEventDTOBase : IEventDTO
    {
        [Parameter("address", "oracle", 1, true )]
        public virtual string Oracle { get; set; }
    }

    public partial class OwnershipTransferredEventDTO : OwnershipTransferredEventDTOBase { }

    [Event("OwnershipTransferred")]
    public class OwnershipTransferredEventDTOBase : IEventDTO
    {
        [Parameter("address", "previousOwner", 1, true )]
        public virtual string PreviousOwner { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class MIN_REGISTRATION_DURATIONOutputDTO : MIN_REGISTRATION_DURATIONOutputDTOBase { }

    [FunctionOutput]
    public class MIN_REGISTRATION_DURATIONOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AvailableOutputDTO : AvailableOutputDTOBase { }

    [FunctionOutput]
    public class AvailableOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class CommitmentsOutputDTO : CommitmentsOutputDTOBase { }

    [FunctionOutput]
    public class CommitmentsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class IsOwnerOutputDTO : IsOwnerOutputDTOBase { }

    [FunctionOutput]
    public class IsOwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class MakeCommitmentOutputDTO : MakeCommitmentOutputDTOBase { }

    [FunctionOutput]
    public class MakeCommitmentOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class MakeCommitmentWithConfigOutputDTO : MakeCommitmentWithConfigOutputDTOBase { }

    [FunctionOutput]
    public class MakeCommitmentWithConfigOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class MaxCommitmentAgeOutputDTO : MaxCommitmentAgeOutputDTOBase { }

    [FunctionOutput]
    public class MaxCommitmentAgeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class MinCommitmentAgeOutputDTO : MinCommitmentAgeOutputDTOBase { }

    [FunctionOutput]
    public class MinCommitmentAgeOutputDTOBase : IFunctionOutputDTO 
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









    public partial class RentPriceOutputDTO : RentPriceOutputDTOBase { }

    [FunctionOutput]
    public class RentPriceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class ValidOutputDTO : ValidOutputDTOBase { }

    [FunctionOutput]
    public class ValidOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }


}
