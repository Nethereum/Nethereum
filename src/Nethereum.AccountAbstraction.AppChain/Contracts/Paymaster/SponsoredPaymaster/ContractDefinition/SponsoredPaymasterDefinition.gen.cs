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
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition
{


    public partial class SponsoredPaymasterDeployment : SponsoredPaymasterDeploymentBase
    {
        public SponsoredPaymasterDeployment() : base(BYTECODE) { }
        public SponsoredPaymasterDeployment(string byteCode) : base(byteCode) { }
    }

    public class SponsoredPaymasterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a03461014b57601f6113b038819003918201601f19168301916001600160401b0383118484101761014f5780849260a09460405283398101031261014b5761004781610163565b9061005460208201610163565b9161006160408301610163565b90608060608401519301519360018060a01b038116918215610138575f80546001600160a01b03198116851782556100d694916001600160a01b03909116907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a36080526100d081610177565b506101ed565b50600280546001600160a01b0319166001600160a01b03929092169190911790556003556007556040516110cf908161028182396080518181816103be01528181610497015281816105f20152818161075c015281816108e70152610ec80152f35b631e4fbdf760e01b5f525f60045260245ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b51906001600160a01b038216820361014b57565b6001600160a01b0381165f9081525f5160206113705f395f51905f52602052604090205460ff166101e8576001600160a01b03165f8181525f5160206113705f395f51905f5260205260408120805460ff191660011790553391905f5160206113505f395f51905f528180a4600190565b505f90565b6001600160a01b0381165f9081525f5160206113905f395f51905f52602052604090205460ff166101e8576001600160a01b03165f8181525f5160206113905f395f51905f5260205260408120805460ff191660011790553391907f1597bc5e34ff090612f53164e4e642d2ab4fc78bffe19ed1b602a0d12559561a905f5160206113505f395f51905f529080a460019056fe6080806040526004361015610024575b50361561001a575f80fd5b610022610ec6565b005b5f905f3560e01c90816301ffc9a7146109da5750806304b5593f14610986578063157cf59e14610963578063205c2878146108b1578063248a9ca3146108855780632f2ff15d1461084657806336568abe1461080157806336698f19146107e357806352b7512c146107285780636661a51e146106bd5780636e3f62371461069f578063715018a6146106455780637c627b21146105965780638da5cb5b1461056f5780638e22558c1461052c57806391d14854146104e2578063a217fddf146104c6578063b0d691fe14610481578063c2d7944414610446578063c399ec8814610392578063c45d1dd214610366578063d089e11a1461033d578063d0e30db014610326578063d547741f146102de578063e44faf7e1461025c578063eb0ebfbc1461023e578063f2fde38b146101b85763f885f2b70361000f57346101b55760203660031901126101b5577f0400213fc8824cc2d2190fb0d17fd5dcb643267f429aaa1439f450ac143a0ebd604060043561019f610f2f565b600354908060035582519182526020820152a180f35b80fd5b50346101b55760203660031901126101b5576101d2610a2d565b6101da610f2f565b6001600160a01b0316801561022a5781546001600160a01b03198116821783556001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08380a380f35b631e4fbdf760e01b82526004829052602482fd5b50346101b557806003193601126101b5576020600654604051908152f35b50346101b557806003193601126101b557600654600954606092906001600160401b036201518042048116911610156102d557805b8160075480155f146102b6575050505f19905b60405192835260208301526040820152f35b8082106102c5575050906102a4565b6102cf9250610cd5565b906102a4565b60085490610291565b50346101b55760403660031901126101b5576103226004356102fe610a43565b9061031d610318825f526001602052600160405f20015490565b610f55565b611015565b5080f35b50806003193601126101b55761033a610ec6565b80f35b50346101b557806003193601126101b5576002546040516001600160a01b039091168152602090f35b50346101b55760203660031901126101b557602061038a610385610a2d565b610e58565b604051908152f35b50346101b557806003193601126101b5576040516370a0823160e01b81523060048201526020816024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa90811561043b578291610401575b602082604051908152f35b90506020813d602011610433575b8161041c60209383610a9b565b8101031261042f5760209150515f6103f6565b5080fd5b3d915061040f565b6040513d84823e3d90fd5b50346101b557806003193601126101b55760206040517f1597bc5e34ff090612f53164e4e642d2ab4fc78bffe19ed1b602a0d12559561a8152f35b50346101b557806003193601126101b5576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b50346101b557806003193601126101b557602090604051908152f35b50346101b55760403660031901126101b55760406104fe610a43565b9160043581526001602052209060018060a01b03165f52602052602060ff60405f2054166040519015158152f35b50346101b55760203660031901126101b5576020906001600160401b03906040906001600160a01b0361055d610a2d565b16815260058452205416604051908152f35b50346101b557806003193601126101b557546040516001600160a01b039091168152602090f35b50346101b55760803660031901126101b5576004359060038210156101b5576024356001600160401b03811161042f573660238201121561042f5780600401356001600160401b038111610641573660248284010111610641577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031633036106325761033a92936024604435930190610ce2565b63bd07c55160e01b8352600483fd5b8280fd5b50346101b557806003193601126101b55761065e610f2f565b80546001600160a01b03198116825581906001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08280a380f35b50346101b557806003193601126101b5576020600754604051908152f35b50346101b55760203660031901126101b5576106d7610a2d565b6106df610f2f565b600280546001600160a01b039283166001600160a01b0319821681179092559091167f68913200cecf41727f9b83c3ef808abc9b8a07e16a8ccb2fbb9fd9bcc7183f828380a380f35b50346101b55760603660031901126101b5576004356001600160401b03811161042f57610120600319823603011261042f577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031633036107d457602061079e60609260443590600401610af1565b604092919251948593604085528051938491826040880152018686015e8383018501526020830152601f01601f19168101030190f35b63bd07c55160e01b8252600482fd5b50346101b557806003193601126101b5576020600354604051908152f35b50346101b55760403660031901126101b55761081b610a43565b336001600160a01b038216036108375761032290600435611015565b63334bd91960e11b8252600482fd5b50346101b55760403660031901126101b557610322600435610866610a43565b90610880610318825f526001602052600160405f20015490565b610f8f565b50346101b55760203660031901126101b557602061038a6004355f526001602052600160405f20015490565b503461095f57604036600319011261095f576004356001600160a01b0381169081810361095f5750602435906108e5610f2f565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031691823b1561095f5760445f9283604051958694859363040b850f60e31b8552600485015260248401525af1801561095457610948575080f35b61002291505f90610a9b565b6040513d5f823e3d90fd5b5f80fd5b3461095f57602036600319011261095f57602061038a610981610a2d565b610a59565b3461095f57602036600319011261095f577fd6cc73d18d9824a8dc0c6f1f474c0a62187e08bbbd5f0ecf26889ded7cde5aae60406004356109c5610f2f565b600754908060075582519182526020820152a1005b3461095f57602036600319011261095f576004359063ffffffff60e01b821680920361095f57602091637965db0b60e01b8114908115610a1c575b5015158152f35b6301ffc9a760e01b14905083610a15565b600435906001600160a01b038216820361095f57565b602435906001600160a01b038216820361095f57565b6001600160a01b03165f818152600560205260409020546001600160401b036201518042048116911610610a96575f52600460205260405f205490565b505f90565b90601f801991011681019081106001600160401b03821117610abc57604052565b634e487b7160e01b5f52604160045260245ffd5b91908201809211610add57565b634e487b7160e01b5f52601160045260245ffd5b356001600160a01b038116919082900361095f576002546001600160a01b031680610c62575b506001600160401b0362015180420416825f526005602052806001600160401b0360405f20541610610c33575b60035480610c01575b50600954816001600160401b03821610610be8575b505060075480610bb8575b50815f52600460205260405f20610b85828254610ad0565b9055610b9381600854610ad0565b600855604051916020830152604082015260408152610bb3606082610a9b565b905f90565b610bc482600854610ad0565b11610bcf575f610b6d565b5050604051610bdf602082610a9b565b5f815290600190565b5f6008556001600160401b031916176009555f80610b62565b835f526004602052610c178360405f2054610ad0565b11610c22575f610b4d565b505050604051610bdf602082610a9b565b825f5260046020525f6040812055825f52600560205260405f20816001600160401b0319825416179055610b44565b602060249160405192838092639f8a13d760e01b82528760048301525afa908115610954575f91610c9a575b5015610bcf575f610b17565b90506020813d602011610ccd575b81610cb560209383610a9b565b8101031261095f5751801515810361095f575f610c8e565b3d9150610ca8565b91908203918211610add57565b909291836040918101031261095f5782356001600160a01b0381169384820361095f5760200135905082811115610e5157610d1d8382610cd5565b915b6003811015610e3d57600114610dd657507f2f5b7da0b8502c9a04f1c60a92a12cc859cb3cefb8951253e2f1c0df6c65d28d9181602092610d74575b50610d6881600654610ad0565b600655604051908152a2565b845f52600483528060405f2054115f14610dd057845f5260048352610d9d8160405f2054610cd5565b855f526004845260405f2055600854908082115f14610dc857610dbf91610cd5565b6008555f610d5b565b50505f610dbf565b5f610d9d565b92915050805f5260046020528160405f2054115f14610e3657805f526004602052610e058260405f2054610cd5565b905b5f52600460205260405f2055600854908082115f14610e2e57610e2991610cd5565b600855565b50505f600855565b5f90610e07565b634e487b7160e01b5f52602160045260245ffd5b5f91610d1f565b6001600160a01b03165f818152600560205260409020546001600160401b03620151804204811691161015610eb557505f5b600354908115610eae5781811015610ea857610ea591610cd5565b90565b50505f90565b50505f1990565b5f52600460205260405f2054610e8a565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b1561095f575f6024916040519283809263b760faf960e01b825230600483015234905af1801561095457610f235750565b5f610f2d91610a9b565b565b5f546001600160a01b03163303610f4257565b63118cdaa760e01b5f523360045260245ffd5b5f81815260016020908152604080832033845290915290205460ff1615610f795750565b63e2517d3f60e01b5f523360045260245260445ffd5b5f8181526001602090815260408083206001600160a01b038616845290915290205460ff16610ea8575f8181526001602081815260408084206001600160a01b0396909616808552959091528220805460ff19169091179055339291907f2f8788117e7eff1d82e926ec794901d17c78024a50270940304540a733656f0d9080a4600190565b5f8181526001602090815260408083206001600160a01b038616845290915290205460ff1615610ea8575f8181526001602090815260408083206001600160a01b0395909516808452949091528120805460ff19169055339291907ff6391f5c32d9c69d2a47ea670b442974b53935d1edc7fd64eb21e047a839171b9080a460019056fea2646970667358221220cbea78fd34d3e6a9860ee9793e990a635e3eaa3e29e22b636494a8421e87326164736f6c634300081c00332f8788117e7eff1d82e926ec794901d17c78024a50270940304540a733656f0da6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb4927365024d84ecf053ef4d13025b66be161b03e90a7fc7ff54f74d5533a9f857e";
        public SponsoredPaymasterDeploymentBase() : base(BYTECODE) { }
        public SponsoredPaymasterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "_owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("address", "_registry", 3)]
        public virtual string Registry { get; set; }
        [Parameter("uint256", "maxPerUser", 4)]
        public virtual BigInteger MaxPerUser { get; set; }
        [Parameter("uint256", "maxTotal", 5)]
        public virtual BigInteger MaxTotal { get; set; }
    }

    public partial class DefaultAdminRoleFunction : DefaultAdminRoleFunctionBase { }

    [Function("DEFAULT_ADMIN_ROLE", "bytes32")]
    public class DefaultAdminRoleFunctionBase : FunctionMessage
    {

    }

    public partial class SponsorRoleFunction : SponsorRoleFunctionBase { }

    [Function("SPONSOR_ROLE", "bytes32")]
    public class SponsorRoleFunctionBase : FunctionMessage
    {

    }

    public partial class AccountRegistryFunction : AccountRegistryFunctionBase { }

    [Function("accountRegistry", "address")]
    public class AccountRegistryFunctionBase : FunctionMessage
    {

    }

    public partial class DailySponsoredFunction : DailySponsoredFunctionBase { }

    [Function("dailySponsored", "uint256")]
    public class DailySponsoredFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class DepositFunction : DepositFunctionBase { }

    [Function("deposit")]
    public class DepositFunctionBase : FunctionMessage
    {

    }

    public partial class EntryPointFunction : EntryPointFunctionBase { }

    [Function("entryPoint", "address")]
    public class EntryPointFunctionBase : FunctionMessage
    {

    }

    public partial class GetDepositFunction : GetDepositFunctionBase { }

    [Function("getDeposit", "uint256")]
    public class GetDepositFunctionBase : FunctionMessage
    {

    }

    public partial class GetRemainingDailySponsorshipFunction : GetRemainingDailySponsorshipFunctionBase { }

    [Function("getRemainingDailySponsorship", "uint256")]
    public class GetRemainingDailySponsorshipFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetRoleAdminFunction : GetRoleAdminFunctionBase { }

    [Function("getRoleAdmin", "bytes32")]
    public class GetRoleAdminFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
    }

    public partial class GetSponsorshipStatsFunction : GetSponsorshipStatsFunctionBase { }

    [Function("getSponsorshipStats", typeof(GetSponsorshipStatsOutputDTO))]
    public class GetSponsorshipStatsFunctionBase : FunctionMessage
    {

    }

    public partial class GrantRoleFunction : GrantRoleFunctionBase { }

    [Function("grantRole")]
    public class GrantRoleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
    }

    public partial class HasRoleFunction : HasRoleFunctionBase { }

    [Function("hasRole", "bool")]
    public class HasRoleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
    }

    public partial class LastSponsorDayFunction : LastSponsorDayFunctionBase { }

    [Function("lastSponsorDay", "uint64")]
    public class LastSponsorDayFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class MaxDailySponsorPerUserFunction : MaxDailySponsorPerUserFunctionBase { }

    [Function("maxDailySponsorPerUser", "uint256")]
    public class MaxDailySponsorPerUserFunctionBase : FunctionMessage
    {

    }

    public partial class MaxTotalDailySponsorshipFunction : MaxTotalDailySponsorshipFunctionBase { }

    [Function("maxTotalDailySponsorship", "uint256")]
    public class MaxTotalDailySponsorshipFunctionBase : FunctionMessage
    {

    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
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
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class RenounceRoleFunction : RenounceRoleFunctionBase { }

    [Function("renounceRole")]
    public class RenounceRoleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
        [Parameter("address", "callerConfirmation", 2)]
        public virtual string CallerConfirmation { get; set; }
    }

    public partial class RevokeRoleFunction : RevokeRoleFunctionBase { }

    [Function("revokeRole")]
    public class RevokeRoleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
    }

    public partial class SetAccountRegistryFunction : SetAccountRegistryFunctionBase { }

    [Function("setAccountRegistry")]
    public class SetAccountRegistryFunctionBase : FunctionMessage
    {
        [Parameter("address", "registry", 1)]
        public virtual string Registry { get; set; }
    }

    public partial class SetMaxDailySponsorPerUserFunction : SetMaxDailySponsorPerUserFunctionBase { }

    [Function("setMaxDailySponsorPerUser")]
    public class SetMaxDailySponsorPerUserFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class SetMaxTotalDailySponsorshipFunction : SetMaxTotalDailySponsorshipFunctionBase { }

    [Function("setMaxTotalDailySponsorship")]
    public class SetMaxTotalDailySponsorshipFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class TotalSponsoredFunction : TotalSponsoredFunctionBase { }

    [Function("totalSponsored", "uint256")]
    public class TotalSponsoredFunctionBase : FunctionMessage
    {

    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class ValidatePaymasterUserOpFunction : ValidatePaymasterUserOpFunctionBase { }

    [Function("validatePaymasterUserOp", typeof(ValidatePaymasterUserOpOutputDTO))]
    public class ValidatePaymasterUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
        [Parameter("uint256", "maxCost", 3)]
        public virtual BigInteger MaxCost { get; set; }
    }

    public partial class WithdrawToFunction : WithdrawToFunctionBase { }

    [Function("withdrawTo")]
    public class WithdrawToFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class DefaultAdminRoleOutputDTO : DefaultAdminRoleOutputDTOBase { }

    [FunctionOutput]
    public class DefaultAdminRoleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SponsorRoleOutputDTO : SponsorRoleOutputDTOBase { }

    [FunctionOutput]
    public class SponsorRoleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AccountRegistryOutputDTO : AccountRegistryOutputDTOBase { }

    [FunctionOutput]
    public class AccountRegistryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class DailySponsoredOutputDTO : DailySponsoredOutputDTOBase { }

    [FunctionOutput]
    public class DailySponsoredOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
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

    public partial class GetRemainingDailySponsorshipOutputDTO : GetRemainingDailySponsorshipOutputDTOBase { }

    [FunctionOutput]
    public class GetRemainingDailySponsorshipOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetRoleAdminOutputDTO : GetRoleAdminOutputDTOBase { }

    [FunctionOutput]
    public class GetRoleAdminOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class GetSponsorshipStatsOutputDTO : GetSponsorshipStatsOutputDTOBase { }

    [FunctionOutput]
    public class GetSponsorshipStatsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "totalAllTime", 1)]
        public virtual BigInteger TotalAllTime { get; set; }
        [Parameter("uint256", "todayTotal", 2)]
        public virtual BigInteger TodayTotal { get; set; }
        [Parameter("uint256", "remainingToday", 3)]
        public virtual BigInteger RemainingToday { get; set; }
    }



    public partial class HasRoleOutputDTO : HasRoleOutputDTOBase { }

    [FunctionOutput]
    public class HasRoleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class LastSponsorDayOutputDTO : LastSponsorDayOutputDTOBase { }

    [FunctionOutput]
    public class LastSponsorDayOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class MaxDailySponsorPerUserOutputDTO : MaxDailySponsorPerUserOutputDTOBase { }

    [FunctionOutput]
    public class MaxDailySponsorPerUserOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class MaxTotalDailySponsorshipOutputDTO : MaxTotalDailySponsorshipOutputDTOBase { }

    [FunctionOutput]
    public class MaxTotalDailySponsorshipOutputDTOBase : IFunctionOutputDTO 
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















    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class TotalSponsoredOutputDTO : TotalSponsoredOutputDTOBase { }

    [FunctionOutput]
    public class TotalSponsoredOutputDTOBase : IFunctionOutputDTO 
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



    public partial class AccountRegistryChangedEventDTO : AccountRegistryChangedEventDTOBase { }

    [Event("AccountRegistryChanged")]
    public class AccountRegistryChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldRegistry", 1, true )]
        public virtual string OldRegistry { get; set; }
        [Parameter("address", "newRegistry", 2, true )]
        public virtual string NewRegistry { get; set; }
    }

    public partial class GasSponsoredEventDTO : GasSponsoredEventDTOBase { }

    [Event("GasSponsored")]
    public class GasSponsoredEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "amount", 2, false )]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class MaxTotalSponsorshipChangedEventDTO : MaxTotalSponsorshipChangedEventDTOBase { }

    [Event("MaxTotalSponsorshipChanged")]
    public class MaxTotalSponsorshipChangedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "oldMax", 1, false )]
        public virtual BigInteger OldMax { get; set; }
        [Parameter("uint256", "newMax", 2, false )]
        public virtual BigInteger NewMax { get; set; }
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

    public partial class RoleAdminChangedEventDTO : RoleAdminChangedEventDTOBase { }

    [Event("RoleAdminChanged")]
    public class RoleAdminChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "role", 1, true )]
        public virtual byte[] Role { get; set; }
        [Parameter("bytes32", "previousAdminRole", 2, true )]
        public virtual byte[] PreviousAdminRole { get; set; }
        [Parameter("bytes32", "newAdminRole", 3, true )]
        public virtual byte[] NewAdminRole { get; set; }
    }

    public partial class RoleGrantedEventDTO : RoleGrantedEventDTOBase { }

    [Event("RoleGranted")]
    public class RoleGrantedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "role", 1, true )]
        public virtual byte[] Role { get; set; }
        [Parameter("address", "account", 2, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "sender", 3, true )]
        public virtual string Sender { get; set; }
    }

    public partial class RoleRevokedEventDTO : RoleRevokedEventDTOBase { }

    [Event("RoleRevoked")]
    public class RoleRevokedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "role", 1, true )]
        public virtual byte[] Role { get; set; }
        [Parameter("address", "account", 2, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "sender", 3, true )]
        public virtual string Sender { get; set; }
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

    public partial class AccessControlBadConfirmationError : AccessControlBadConfirmationErrorBase { }
    [Error("AccessControlBadConfirmation")]
    public class AccessControlBadConfirmationErrorBase : IErrorDTO
    {
    }

    public partial class AccessControlUnauthorizedAccountError : AccessControlUnauthorizedAccountErrorBase { }

    [Error("AccessControlUnauthorizedAccount")]
    public class AccessControlUnauthorizedAccountErrorBase : IErrorDTO
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("bytes32", "neededRole", 2)]
        public virtual byte[] NeededRole { get; set; }
    }

    public partial class AccountNotActiveError : AccountNotActiveErrorBase { }
    [Error("AccountNotActive")]
    public class AccountNotActiveErrorBase : IErrorDTO
    {
    }

    public partial class DailySponsorLimitExceededError : DailySponsorLimitExceededErrorBase { }
    [Error("DailySponsorLimitExceeded")]
    public class DailySponsorLimitExceededErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientDepositError : InsufficientDepositErrorBase { }
    [Error("InsufficientDeposit")]
    public class InsufficientDepositErrorBase : IErrorDTO
    {
    }

    public partial class OnlyEntryPointError : OnlyEntryPointErrorBase { }
    [Error("OnlyEntryPoint")]
    public class OnlyEntryPointErrorBase : IErrorDTO
    {
    }

    public partial class OwnableInvalidOwnerError : OwnableInvalidOwnerErrorBase { }

    [Error("OwnableInvalidOwner")]
    public class OwnableInvalidOwnerErrorBase : IErrorDTO
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class OwnableUnauthorizedAccountError : OwnableUnauthorizedAccountErrorBase { }

    [Error("OwnableUnauthorizedAccount")]
    public class OwnableUnauthorizedAccountErrorBase : IErrorDTO
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class TotalDailySponsorLimitExceededError : TotalDailySponsorLimitExceededErrorBase { }
    [Error("TotalDailySponsorLimitExceeded")]
    public class TotalDailySponsorLimitExceededErrorBase : IErrorDTO
    {
    }
}
