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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IAccount.ContractDefinition
{


    public partial class IAccountDeployment : IAccountDeploymentBase
    {
        public IAccountDeployment() : base(BYTECODE) { }
        public IAccountDeployment(string byteCode) : base(byteCode) { }
    }

    public class IAccountDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IAccountDeploymentBase() : base(BYTECODE) { }
        public IAccountDeploymentBase(string byteCode) : base(byteCode) { }

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


}
