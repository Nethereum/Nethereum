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

namespace Nethereum.ENS.StablePriceOracle.ContractDefinition
{
    public partial class StablePriceOracleDeployment : StablePriceOracleDeploymentBase
    {
        public StablePriceOracleDeployment() : base(BYTECODE) { }
        public StablePriceOracleDeployment(string byteCode) : base(byteCode) { }
    }

    public class StablePriceOracleDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public StablePriceOracleDeploymentBase() : base(BYTECODE) { }
        public StablePriceOracleDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_usdOracle", 1)]
        public virtual string UsdOracle { get; set; }
        [Parameter("uint256[]", "_rentPrices", 2)]
        public virtual List<BigInteger> RentPrices { get; set; }
    }

    public partial class RentPricesFunction : RentPricesFunctionBase { }

    [Function("rentPrices", "uint256")]
    public class RentPricesFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class PriceFunction : PriceFunctionBase { }

    [Function("price", "uint256")]
    public class PriceFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
        [Parameter("uint256", "duration", 3)]
        public virtual BigInteger Duration { get; set; }
    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class SetPricesFunction : SetPricesFunctionBase { }

    [Function("setPrices")]
    public class SetPricesFunctionBase : FunctionMessage
    {
        [Parameter("uint256[]", "_rentPrices", 1)]
        public virtual List<BigInteger> RentPrices { get; set; }
    }

    public partial class SetOracleFunction : SetOracleFunctionBase { }

    [Function("setOracle")]
    public class SetOracleFunctionBase : FunctionMessage
    {
        [Parameter("address", "_usdOracle", 1)]
        public virtual string UsdOracle { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class IsOwnerFunction : IsOwnerFunctionBase { }

    [Function("isOwner", "bool")]
    public class IsOwnerFunctionBase : FunctionMessage
    {

    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class OracleChangedEventDTO : OracleChangedEventDTOBase { }

    [Event("OracleChanged")]
    public class OracleChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oracle", 1, false )]
        public virtual string Oracle { get; set; }
    }

    public partial class RentPriceChangedEventDTO : RentPriceChangedEventDTOBase { }

    [Event("RentPriceChanged")]
    public class RentPriceChangedEventDTOBase : IEventDTO
    {
        [Parameter("uint256[]", "prices", 1, false )]
        public virtual List<BigInteger> Prices { get; set; }
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

    public partial class RentPricesOutputDTO : RentPricesOutputDTOBase { }

    [FunctionOutput]
    public class RentPricesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class PriceOutputDTO : PriceOutputDTOBase { }

    [FunctionOutput]
    public class PriceOutputDTOBase : IFunctionOutputDTO 
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

    public partial class IsOwnerOutputDTO : IsOwnerOutputDTOBase { }

    [FunctionOutput]
    public class IsOwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }


}
