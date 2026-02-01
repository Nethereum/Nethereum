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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountModule.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountModule.ContractDefinition
{


    public partial class IAccountModuleDeployment : IAccountModuleDeploymentBase
    {
        public IAccountModuleDeployment() : base(BYTECODE) { }
        public IAccountModuleDeployment(string byteCode) : base(byteCode) { }
    }

    public class IAccountModuleDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IAccountModuleDeploymentBase() : base(BYTECODE) { }
        public IAccountModuleDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ModuleIdFunction : ModuleIdFunctionBase { }

    [Function("moduleId", "bytes32")]
    public class ModuleIdFunctionBase : FunctionMessage
    {

    }

    public partial class PostExecuteFunction : PostExecuteFunctionBase { }

    [Function("postExecute")]
    public class PostExecuteFunctionBase : FunctionMessage
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class PreExecuteFunction : PreExecuteFunctionBase { }

    [Function("preExecute")]
    public class PreExecuteFunctionBase : FunctionMessage
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class ValidateUserOpFunction : ValidateUserOpFunctionBase { }

    [Function("validateUserOp", "uint256")]
    public class ValidateUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
    }

    public partial class VersionFunction : VersionFunctionBase { }

    [Function("version", "uint256")]
    public class VersionFunctionBase : FunctionMessage
    {

    }

    public partial class ModuleIdOutputDTO : ModuleIdOutputDTOBase { }

    [FunctionOutput]
    public class ModuleIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }





    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class VersionOutputDTO : VersionOutputDTOBase { }

    [FunctionOutput]
    public class VersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }
}
