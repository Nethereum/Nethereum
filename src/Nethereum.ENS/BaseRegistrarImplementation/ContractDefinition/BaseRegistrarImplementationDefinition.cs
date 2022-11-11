using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.ENS.BaseRegistrarImplementation.ContractDefinition
{

    public partial class BaseRegistrarImplementationDeployment : BaseRegistrarImplementationDeploymentBase
    {
        public BaseRegistrarImplementationDeployment() : base(BYTECODE) { }
        public BaseRegistrarImplementationDeployment(string byteCode) : base(byteCode) { }
    }

    public class BaseRegistrarImplementationDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public BaseRegistrarImplementationDeploymentBase() : base(BYTECODE) { }
        public BaseRegistrarImplementationDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_ens", 1)]
        public virtual string Ens { get; set; }
        [Parameter("bytes32", "_baseNode", 2)]
        public virtual byte[] BaseNode { get; set; }
    }

    public partial class GRACE_PERIODFunction : GRACE_PERIODFunctionBase { }

    [Function("GRACE_PERIOD", "uint256")]
    public class GRACE_PERIODFunctionBase : FunctionMessage
    {

    }

    public partial class AddControllerFunction : AddControllerFunctionBase { }

    [Function("addController")]
    public class AddControllerFunctionBase : FunctionMessage
    {
        [Parameter("address", "controller", 1)]
        public virtual string Controller { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "tokenId", 2)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class AvailableFunction : AvailableFunctionBase { }

    [Function("available", "bool")]
    public class AvailableFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class BaseNodeFunction : BaseNodeFunctionBase { }

    [Function("baseNode", "bytes32")]
    public class BaseNodeFunctionBase : FunctionMessage
    {

    }

    public partial class ControllersFunction : ControllersFunctionBase { }

    [Function("controllers", "bool")]
    public class ControllersFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class EnsFunction : EnsFunctionBase { }

    [Function("ens", "address")]
    public class EnsFunctionBase : FunctionMessage
    {

    }

    public partial class GetApprovedFunction : GetApprovedFunctionBase { }

    [Function("getApproved", "address")]
    public class GetApprovedFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class IsApprovedForAllFunction : IsApprovedForAllFunctionBase { }

    [Function("isApprovedForAll", "bool")]
    public class IsApprovedForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2)]
        public virtual string Operator { get; set; }
    }

    public partial class IsOwnerFunction : IsOwnerFunctionBase { }

    [Function("isOwner", "bool")]
    public class IsOwnerFunctionBase : FunctionMessage
    {

    }

    public partial class NameExpiresFunction : NameExpiresFunctionBase { }

    [Function("nameExpires", "uint256")]
    public class NameExpiresFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class OwnerOfFunction : OwnerOfFunctionBase { }

    [Function("ownerOf", "address")]
    public class OwnerOfFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class ReclaimFunction : ReclaimFunctionBase { }

    [Function("reclaim")]
    public class ReclaimFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
    }

    public partial class RegisterFunction : RegisterFunctionBase { }

    [Function("register", "uint256")]
    public class RegisterFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "duration", 3)]
        public virtual BigInteger Duration { get; set; }
    }

    public partial class RegisterOnlyFunction : RegisterOnlyFunctionBase { }

    [Function("registerOnly", "uint256")]
    public class RegisterOnlyFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "duration", 3)]
        public virtual BigInteger Duration { get; set; }
    }

    public partial class RemoveControllerFunction : RemoveControllerFunctionBase { }

    [Function("removeController")]
    public class RemoveControllerFunctionBase : FunctionMessage
    {
        [Parameter("address", "controller", 1)]
        public virtual string Controller { get; set; }
    }

    public partial class RenewFunction : RenewFunctionBase { }

    [Function("renew", "uint256")]
    public class RenewFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "duration", 2)]
        public virtual BigInteger Duration { get; set; }
    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class SafeTransferFromFunction : SafeTransferFromFunctionBase { }

    [Function("safeTransferFrom")]
    public class SafeTransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "tokenId", 3)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class SafeTransferFromFunction2 : SafeTransferFromFunctionBase2 { }

    [Function("safeTransferFrom")]
    public class SafeTransferFromFunctionBase2 : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "tokenId", 3)]
        public virtual BigInteger TokenId { get; set; }
        [Parameter("bytes", "_data", 4)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SetApprovalForAllFunction : SetApprovalForAllFunctionBase { }

    [Function("setApprovalForAll")]
    public class SetApprovalForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("bool", "approved", 2)]
        public virtual bool Approved { get; set; }
    }

    public partial class SetResolverFunction : SetResolverFunctionBase { }

    [Function("setResolver")]
    public class SetResolverFunctionBase : FunctionMessage
    {
        [Parameter("address", "resolver", 1)]
        public virtual string Resolver { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "tokenId", 3)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true)]
        public virtual string Owner { get; set; }
        [Parameter("address", "approved", 2, true)]
        public virtual string Approved { get; set; }
        [Parameter("uint256", "tokenId", 3, true)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class ApprovalForAllEventDTO : ApprovalForAllEventDTOBase { }

    [Event("ApprovalForAll")]
    public class ApprovalForAllEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true)]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2, true)]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 3, false)]
        public virtual bool Approved { get; set; }
    }

    public partial class ControllerAddedEventDTO : ControllerAddedEventDTOBase { }

    [Event("ControllerAdded")]
    public class ControllerAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "controller", 1, true)]
        public virtual string Controller { get; set; }
    }

    public partial class ControllerRemovedEventDTO : ControllerRemovedEventDTOBase { }

    [Event("ControllerRemoved")]
    public class ControllerRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "controller", 1, true)]
        public virtual string Controller { get; set; }
    }

    public partial class NameMigratedEventDTO : NameMigratedEventDTOBase { }

    [Event("NameMigrated")]
    public class NameMigratedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "id", 1, true)]
        public virtual BigInteger Id { get; set; }
        [Parameter("address", "owner", 2, true)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "expires", 3, false)]
        public virtual BigInteger Expires { get; set; }
    }

    public partial class NameRegisteredEventDTO : NameRegisteredEventDTOBase { }

    [Event("NameRegistered")]
    public class NameRegisteredEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "id", 1, true)]
        public virtual BigInteger Id { get; set; }
        [Parameter("address", "owner", 2, true)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "expires", 3, false)]
        public virtual BigInteger Expires { get; set; }
    }

    public partial class NameRenewedEventDTO : NameRenewedEventDTOBase { }

    [Event("NameRenewed")]
    public class NameRenewedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "id", 1, true)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "expires", 2, false)]
        public virtual BigInteger Expires { get; set; }
    }

    public partial class OwnershipTransferredEventDTO : OwnershipTransferredEventDTOBase { }

    [Event("OwnershipTransferred")]
    public class OwnershipTransferredEventDTOBase : IEventDTO
    {
        [Parameter("address", "previousOwner", 1, true)]
        public virtual string PreviousOwner { get; set; }
        [Parameter("address", "newOwner", 2, true)]
        public virtual string NewOwner { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "tokenId", 3, true)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class GRACE_PERIODOutputDTO : GRACE_PERIODOutputDTOBase { }

    [FunctionOutput]
    public class GRACE_PERIODOutputDTOBase : IFunctionOutputDTO
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

    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class BaseNodeOutputDTO : BaseNodeOutputDTOBase { }

    [FunctionOutput]
    public class BaseNodeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class ControllersOutputDTO : ControllersOutputDTOBase { }

    [FunctionOutput]
    public class ControllersOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class EnsOutputDTO : EnsOutputDTOBase { }

    [FunctionOutput]
    public class EnsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetApprovedOutputDTO : GetApprovedOutputDTOBase { }

    [FunctionOutput]
    public class GetApprovedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsApprovedForAllOutputDTO : IsApprovedForAllOutputDTOBase { }

    [FunctionOutput]
    public class IsApprovedForAllOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsOwnerOutputDTO : IsOwnerOutputDTOBase { }

    [FunctionOutput]
    public class IsOwnerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class NameExpiresOutputDTO : NameExpiresOutputDTOBase { }

    [FunctionOutput]
    public class NameExpiresOutputDTOBase : IFunctionOutputDTO
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

    public partial class OwnerOfOutputDTO : OwnerOfOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOfOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





















    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }




}
