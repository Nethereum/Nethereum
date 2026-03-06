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
using Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.ISponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.ISponsoredPaymaster.ContractDefinition
{


    public partial class ISponsoredPaymasterDeployment : ISponsoredPaymasterDeploymentBase
    {
        public ISponsoredPaymasterDeployment() : base(BYTECODE) { }
        public ISponsoredPaymasterDeployment(string byteCode) : base(byteCode) { }
    }

    public class ISponsoredPaymasterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ISponsoredPaymasterDeploymentBase() : base(BYTECODE) { }
        public ISponsoredPaymasterDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class DailySponsoredFunction : DailySponsoredFunctionBase { }

    [Function("dailySponsored", "uint256")]
    public class DailySponsoredFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class MaxDailySponsorPerUserFunction : MaxDailySponsorPerUserFunctionBase { }

    [Function("maxDailySponsorPerUser", "uint256")]
    public class MaxDailySponsorPerUserFunctionBase : FunctionMessage
    {

    }

    public partial class PostOpFunction : PostOpFunctionBase { }

    [Function("postOp")]
    public class PostOpFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "mode", 1)]
        public virtual byte Mode { get; set; }
        [Parameter("bytes", "context", 2)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "actualGasCost", 3)]
        public virtual BigInteger ActualGasCost { get; set; }
        [Parameter("uint256", "actualUserOpFeePerGas", 4)]
        public virtual BigInteger ActualUserOpFeePerGas { get; set; }
    }

    public partial class SetMaxDailySponsorPerUserFunction : SetMaxDailySponsorPerUserFunctionBase { }

    [Function("setMaxDailySponsorPerUser")]
    public class SetMaxDailySponsorPerUserFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class ValidatePaymasterUserOpFunction : ValidatePaymasterUserOpFunctionBase { }

    [Function("validatePaymasterUserOp", typeof(ValidatePaymasterUserOpOutputDTO))]
    public class ValidatePaymasterUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
        [Parameter("uint256", "maxCost", 3)]
        public virtual BigInteger MaxCost { get; set; }
    }

    public partial class DailySponsoredOutputDTO : DailySponsoredOutputDTOBase { }

    [FunctionOutput]
    public class DailySponsoredOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class MaxDailySponsorPerUserOutputDTO : MaxDailySponsorPerUserOutputDTOBase { }

    [FunctionOutput]
    public class MaxDailySponsorPerUserOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class ValidatePaymasterUserOpOutputDTO : ValidatePaymasterUserOpOutputDTOBase { }

    [FunctionOutput]
    public class ValidatePaymasterUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "context", 1)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "validationData", 2)]
        public virtual BigInteger ValidationData { get; set; }
    }

    public partial class SponsorLimitSetEventDTO : SponsorLimitSetEventDTOBase { }

    [Event("SponsorLimitSet")]
    public class SponsorLimitSetEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "oldLimit", 1, false )]
        public virtual BigInteger OldLimit { get; set; }
        [Parameter("uint256", "newLimit", 2, false )]
        public virtual BigInteger NewLimit { get; set; }
    }
}
