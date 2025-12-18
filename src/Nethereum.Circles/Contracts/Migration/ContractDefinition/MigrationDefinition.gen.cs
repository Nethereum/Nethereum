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

namespace Nethereum.Circles.Contracts.Migration.ContractDefinition
{


    public partial class MigrationDeployment : MigrationDeploymentBase
    {
        public MigrationDeployment() : base(BYTECODE) { }
        public MigrationDeployment(string byteCode) : base(byteCode) { }
    }

    public class MigrationDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60e060405234801561000f575f80fd5b50604051610bcd380380610bcd83398101604081905261002e91610133565b6001600160a01b03831661005d5760405163d82c8fc960e01b8152600e60048201526024015b60405180910390fd5b6001600160a01b0382166100875760405163d82c8fc960e01b8152600f6004820152602401610054565b6001600160a01b0383811660808190525f80546001600160a01b0319169285169290921790915560a08290526040805163ef78d4fd60e01b8152905163ef78d4fd916004808201926020929091908290030181865afa1580156100ec573d5f803e3d5ffd5b505050506040513d601f19601f820116820180604052508101906101109190610173565b60c0525061018a915050565b6001600160a01b0381168114610130575f80fd5b50565b5f805f60608486031215610145575f80fd5b83516101508161011c565b60208501519093506101618161011c565b80925050604084015190509250925092565b5f60208284031215610183575f80fd5b5051919050565b60805160a05160c0516109e36101ea5f395f8181610127015281816101e10152818161037c01526103b901525f81816098015261020c01525f818160e00152818161014d0152818161026a01528181610300015261049001526109e35ff3fe608060405234801561000f575f80fd5b5060043610610060575f3560e01c806379742f66146100645780637c234d01146100935780639a288377146100c8578063bde87738146100db578063de01e15114610102578063ef78d4fd14610122575b5f80fd5b5f54610076906001600160a01b031681565b6040516001600160a01b0390911681526020015b60405180910390f35b6100ba7f000000000000000000000000000000000000000000000000000000000000000081565b60405190815260200161008a565b6100ba6100d6366004610704565b610149565b6100767f000000000000000000000000000000000000000000000000000000000000000081565b610115610110366004610763565b610413565b60405161008a9190610804565b6100ba7f000000000000000000000000000000000000000000000000000000000000000081565b5f807f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031663a4caeb426040518163ffffffff1660e01b8152600401602060405180830381865afa1580156101a7573d5f803e3d5ffd5b505050506040513d601f19601f820116820180604052508101906101cb919061081d565b90505f6101d9826001610848565b90505f6102067f000000000000000000000000000000000000000000000000000000000000000084610861565b610230907f0000000000000000000000000000000000000000000000000000000000000000610848565b90505f61023d8242610878565b604051631549c1a160e11b81526305f5e1006004820152602481018690529091505f906001600160a01b037f00000000000000000000000000000000000000000000000000000000000000001690632a93834290604401602060405180830381865afa1580156102af573d5f803e3d5ffd5b505050506040513d601f19601f820116820180604052508101906102d3919061081d565b604051631549c1a160e11b81526305f5e1006004820152602481018690529091505f906001600160a01b037f00000000000000000000000000000000000000000000000000000000000000001690632a93834290604401602060405180830381865afa158015610345573d5f803e3d5ffd5b505050506040513d601f19601f82011682018060405250810190610369919061081d565b90505f6103768483610861565b6103a0857f0000000000000000000000000000000000000000000000000000000000000000610878565b6103aa9085610861565b6103b49190610848565b9050807f00000000000000000000000000000000000000000000000000000000000000006305f5e1006103e88c6003610861565b6103f29190610861565b6103fc9190610861565b610406919061088b565b9998505050505050505050565b606083821461043d5760405163d82c8fc960e01b815260a760048201526024015b60405180910390fd5b5f8467ffffffffffffffff811115610457576104576108aa565b604051908082528060200260200182016040528015610480578160200160208202803683370190505b5090505f5b85811015610697575f7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03166328d249fe8989858181106104cf576104cf6108be565b90506020020160208101906104e491906108e9565b6040516001600160e01b031960e084901b1681526001600160a01b039091166004820152602401602060405180830381865afa158015610526573d5f803e3d5ffd5b505050506040513d601f19601f8201168201806040525081019061054a9190610904565b90506001600160a01b0381166105765760405163d82c8fc960e01b815260106004820152602401610434565b858583818110610588576105886108be565b905060200201355f036105ae57604051634d49b02960e11b815260040160405180910390fd5b6105cf8686848181106105c3576105c36108be565b90506020020135610149565b8383815181106105e1576105e16108be565b602002602001018181525050806001600160a01b03166323b872dd3330898987818110610610576106106108be565b6040516001600160e01b031960e088901b1681526001600160a01b039586166004820152949093166024850152506020909102013560448201526064016020604051808303815f875af1158015610669573d5f803e3d5ffd5b505050506040513d601f19601f8201168201806040525081019061068d919061091f565b5050600101610485565b505f5460405163f317707960e01b81526001600160a01b039091169063f3177079906106cd9033908a908a90879060040161093e565b5f604051808303815f87803b1580156106e4575f80fd5b505af11580156106f6573d5f803e3d5ffd5b509298975050505050505050565b5f60208284031215610714575f80fd5b5035919050565b5f8083601f84011261072b575f80fd5b50813567ffffffffffffffff811115610742575f80fd5b6020830191508360208260051b850101111561075c575f80fd5b9250929050565b5f805f8060408587031215610776575f80fd5b843567ffffffffffffffff8082111561078d575f80fd5b6107998883890161071b565b909650945060208701359150808211156107b1575f80fd5b506107be8782880161071b565b95989497509550505050565b5f815180845260208085019450602084015f5b838110156107f9578151875295820195908201906001016107dd565b509495945050505050565b602081525f61081660208301846107ca565b9392505050565b5f6020828403121561082d575f80fd5b5051919050565b634e487b7160e01b5f52601160045260245ffd5b8082018082111561085b5761085b610834565b92915050565b808202811582820484141761085b5761085b610834565b8181038181111561085b5761085b610834565b5f826108a557634e487b7160e01b5f52601260045260245ffd5b500490565b634e487b7160e01b5f52604160045260245ffd5b634e487b7160e01b5f52603260045260245ffd5b6001600160a01b03811681146108e6575f80fd5b50565b5f602082840312156108f9575f80fd5b8135610816816108d2565b5f60208284031215610914575f80fd5b8151610816816108d2565b5f6020828403121561092f575f80fd5b81518015158114610816575f80fd5b6001600160a01b038581168252606060208084018290529083018590525f91869160808501845b8881101561098c578435610978816108d2565b841682529382019390820190600101610965565b50858103604087015261099f81886107ca565b9a995050505050505050505056fea2646970667358221220c764adb1a7dd2f0068a37ea158cc1e6c730708210b56cf106c4a083b39b2561464736f6c63430008190033";
        public MigrationDeploymentBase() : base(BYTECODE) { }
        public MigrationDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_hubV1", 1)]
        public virtual string HubV1 { get; set; }
        [Parameter("address", "_hubV2", 2)]
        public virtual string HubV2 { get; set; }
        [Parameter("uint256", "_inflationDayZero", 3)]
        public virtual BigInteger InflationDayZero { get; set; }
    }

    public partial class ConvertFromV1ToDemurrageFunction : ConvertFromV1ToDemurrageFunctionBase { }

    [Function("convertFromV1ToDemurrage", "uint256")]
    public class ConvertFromV1ToDemurrageFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class HubV1Function : HubV1FunctionBase { }

    [Function("hubV1", "address")]
    public class HubV1FunctionBase : FunctionMessage
    {

    }

    public partial class HubV2Function : HubV2FunctionBase { }

    [Function("hubV2", "address")]
    public class HubV2FunctionBase : FunctionMessage
    {

    }

    public partial class InflationDayZeroFunction : InflationDayZeroFunctionBase { }

    [Function("inflationDayZero", "uint256")]
    public class InflationDayZeroFunctionBase : FunctionMessage
    {

    }

    public partial class MigrateFunction : MigrateFunctionBase { }

    [Function("migrate", "uint256[]")]
    public class MigrateFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "_avatars", 1)]
        public virtual List<string> Avatars { get; set; }
        [Parameter("uint256[]", "_amounts", 2)]
        public virtual List<BigInteger> Amounts { get; set; }
    }

    public partial class PeriodFunction : PeriodFunctionBase { }

    [Function("period", "uint256")]
    public class PeriodFunctionBase : FunctionMessage
    {

    }

    public partial class CirclesAmountOverflowError : CirclesAmountOverflowErrorBase { }

    [Error("CirclesAmountOverflow")]
    public class CirclesAmountOverflowErrorBase : IErrorDTO
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesErrorAddressUintArgsError : CirclesErrorAddressUintArgsErrorBase { }

    [Error("CirclesErrorAddressUintArgs")]
    public class CirclesErrorAddressUintArgsErrorBase : IErrorDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
        [Parameter("uint8", "", 3)]
        public virtual byte ReturnValue3 { get; set; }
    }

    public partial class CirclesErrorNoArgsError : CirclesErrorNoArgsErrorBase { }

    [Error("CirclesErrorNoArgs")]
    public class CirclesErrorNoArgsErrorBase : IErrorDTO
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class CirclesErrorOneAddressArgError : CirclesErrorOneAddressArgErrorBase { }

    [Error("CirclesErrorOneAddressArg")]
    public class CirclesErrorOneAddressArgErrorBase : IErrorDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint8", "", 2)]
        public virtual byte ReturnValue2 { get; set; }
    }

    public partial class CirclesIdMustBeDerivedFromAddressError : CirclesIdMustBeDerivedFromAddressErrorBase { }

    [Error("CirclesIdMustBeDerivedFromAddress")]
    public class CirclesIdMustBeDerivedFromAddressErrorBase : IErrorDTO
    {
        [Parameter("uint256", "providedId", 1)]
        public virtual BigInteger ProvidedId { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesInvalidCirclesIdError : CirclesInvalidCirclesIdErrorBase { }

    [Error("CirclesInvalidCirclesId")]
    public class CirclesInvalidCirclesIdErrorBase : IErrorDTO
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesInvalidParameterError : CirclesInvalidParameterErrorBase { }

    [Error("CirclesInvalidParameter")]
    public class CirclesInvalidParameterErrorBase : IErrorDTO
    {
        [Parameter("uint256", "parameter", 1)]
        public virtual BigInteger Parameter { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesMigrationAmountMustBeGreaterThanZeroError : CirclesMigrationAmountMustBeGreaterThanZeroErrorBase { }
    [Error("CirclesMigrationAmountMustBeGreaterThanZero")]
    public class CirclesMigrationAmountMustBeGreaterThanZeroErrorBase : IErrorDTO
    {
    }

    public partial class CirclesProxyAlreadyInitializedError : CirclesProxyAlreadyInitializedErrorBase { }
    [Error("CirclesProxyAlreadyInitialized")]
    public class CirclesProxyAlreadyInitializedErrorBase : IErrorDTO
    {
    }

    public partial class CirclesReentrancyGuardError : CirclesReentrancyGuardErrorBase { }

    [Error("CirclesReentrancyGuard")]
    public class CirclesReentrancyGuardErrorBase : IErrorDTO
    {
        [Parameter("uint8", "code", 1)]
        public virtual byte Code { get; set; }
    }

    public partial class ConvertFromV1ToDemurrageOutputDTO : ConvertFromV1ToDemurrageOutputDTOBase { }

    [FunctionOutput]
    public class ConvertFromV1ToDemurrageOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class HubV1OutputDTO : HubV1OutputDTOBase { }

    [FunctionOutput]
    public class HubV1OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class HubV2OutputDTO : HubV2OutputDTOBase { }

    [FunctionOutput]
    public class HubV2OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class InflationDayZeroOutputDTO : InflationDayZeroOutputDTOBase { }

    [FunctionOutput]
    public class InflationDayZeroOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class PeriodOutputDTO : PeriodOutputDTOBase { }

    [FunctionOutput]
    public class PeriodOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }
}
