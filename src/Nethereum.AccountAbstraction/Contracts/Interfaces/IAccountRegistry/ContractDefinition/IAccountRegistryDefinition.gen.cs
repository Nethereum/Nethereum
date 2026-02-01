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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountRegistry.ContractDefinition
{


    public partial class IAccountRegistryDeployment : IAccountRegistryDeploymentBase
    {
        public IAccountRegistryDeployment() : base(BYTECODE) { }
        public IAccountRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class IAccountRegistryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IAccountRegistryDeploymentBase() : base(BYTECODE) { }
        public IAccountRegistryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class IsActiveFunction : IsActiveFunctionBase { }

    [Function("isActive", "bool")]
    public class IsActiveFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class IsActiveOutputDTO : IsActiveOutputDTOBase { }

    [FunctionOutput]
    public class IsActiveOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}
