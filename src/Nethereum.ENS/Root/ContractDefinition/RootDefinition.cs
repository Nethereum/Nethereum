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

namespace Nethereum.ENS.Root.ContractDefinition
{

    public partial class RootDeployment : RootDeploymentBase
    {
        public RootDeployment() : base(BYTECODE) { }
        public RootDeployment(string byteCode) : base(byteCode) { }
    }

    public class RootDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public RootDeploymentBase() : base(BYTECODE) { }
        public RootDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_ens", 1)]
        public virtual string Ens { get; set; }
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

    public partial class IsOwnerFunction : IsOwnerFunctionBase { }

    [Function("isOwner", "bool")]
    public class IsOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "addr", 1)]
        public virtual string Addr { get; set; }
    }

    public partial class LockFunction : LockFunctionBase { }

    [Function("lock")]
    public class LockFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "label", 1)]
        public virtual byte[] Label { get; set; }
    }

    public partial class LockedFunction : LockedFunctionBase { }

    [Function("locked", "bool")]
    public class LockedFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class SetControllerFunction : SetControllerFunctionBase { }

    [Function("setController")]
    public class SetControllerFunctionBase : FunctionMessage
    {
        [Parameter("address", "controller", 1)]
        public virtual string Controller { get; set; }
        [Parameter("bool", "enabled", 2)]
        public virtual bool Enabled { get; set; }
    }

    public partial class SetResolverFunction : SetResolverFunctionBase { }

    [Function("setResolver")]
    public class SetResolverFunctionBase : FunctionMessage
    {
        [Parameter("address", "resolver", 1)]
        public virtual string Resolver { get; set; }
    }

    public partial class SetSubnodeOwnerFunction : SetSubnodeOwnerFunctionBase { }

    [Function("setSubnodeOwner")]
    public class SetSubnodeOwnerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "label", 1)]
        public virtual byte[] Label { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
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

    public partial class TLDLockedEventDTO : TLDLockedEventDTOBase { }

    [Event("TLDLocked")]
    public class TLDLockedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "label", 1, true )]
        public virtual byte[] Label { get; set; }
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

    public partial class IsOwnerOutputDTO : IsOwnerOutputDTOBase { }

    [FunctionOutput]
    public class IsOwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class LockedOutputDTO : LockedOutputDTOBase { }

    [FunctionOutput]
    public class LockedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
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
