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

namespace Nethereum.GnosisSafe.GnosisSafe.ContractDefinition
{


    public partial class GnosisSafeDeployment : GnosisSafeDeploymentBase
    {
        public GnosisSafeDeployment() : base(BYTECODE) { }
        public GnosisSafeDeployment(string byteCode) : base(byteCode) { }
    }

    public class GnosisSafeDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608060405234801561001057600080fd5b50604051602080611400833981016040525160008054600160a060020a03909216600160a060020a03199092169190911790556113ae806100526000396000f3006080604052600436106100da5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166301ffc9a781146100df57806310f13a8c146101155780632203ab56146101b357806329cd62ea1461024d5780632dff69411461026b5780633b3b57de1461029557806359d1d43c146102c9578063623195b01461039c578063691f3431146103fc5780637737221314610414578063aa4cb54714610472578063c3d014d6146104d0578063c8690233146104eb578063d5fa2b001461051c578063e89401a114610540575b600080fd5b3480156100eb57600080fd5b50610101600160e060020a031960043516610558565b604080519115158252519081900360200190f35b34801561012157600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b195833595369560449491939091019190819084018382808284375050604080516020601f89358b018035918201839004830284018301909452808352979a9998810197919650918201945092508291508401838280828437509497506106f99650505050505050565b005b3480156101bf57600080fd5b506101ce60043560243561091f565b6040518083815260200180602001828103825283818151815260200191508051906020019080838360005b838110156102115781810151838201526020016101f9565b50505050905090810190601f16801561023e5780820380516001836020036101000a031916815260200191505b50935050505060405180910390f35b34801561025957600080fd5b506101b1600435602435604435610a2b565b34801561027757600080fd5b50610283600435610b2b565b60408051918252519081900360200190f35b3480156102a157600080fd5b506102ad600435610b41565b60408051600160a060020a039092168252519081900360200190f35b3480156102d557600080fd5b5060408051602060046024803582810135601f8101859004850286018501909652858552610327958335953695604494919390910191908190840183828082843750949750610b5c9650505050505050565b6040805160208082528351818301528351919283929083019185019080838360005b83811015610361578181015183820152602001610349565b50505050905090810190601f16801561038e5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b3480156103a857600080fd5b50604080516020600460443581810135601f81018490048402850184019095528484526101b1948235946024803595369594606494920191908190840183828082843750949750610c659650505050505050565b34801561040857600080fd5b50610327600435610d66565b34801561042057600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b1958335953695604494919390910191908190840183828082843750949750610e0a9650505050505050565b34801561047e57600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b1958335953695604494919390910191908190840183828082843750949750610f609650505050505050565b3480156104dc57600080fd5b506101b1600435602435611076565b3480156104f757600080fd5b50610503600435611157565b6040805192835260208301919091528051918290030190f35b34801561052857600080fd5b506101b1600435600160a060020a0360243516611174565b34801561054c57600080fd5b50610327600435611278565b6000600160e060020a031982167f3b3b57de0000000000000000000000000000000000000000000000000000000014806105bb5750600160e060020a031982167fd8389dc500000000000000000000000000000000000000000000000000000000145b806105ef5750600160e060020a031982167f691f343100000000000000000000000000000000000000000000000000000000145b806106235750600160e060020a031982167f2203ab5600000000000000000000000000000000000000000000000000000000145b806106575750600160e060020a031982167fc869023300000000000000000000000000000000000000000000000000000000145b8061068b5750600160e060020a031982167f59d1d43c00000000000000000000000000000000000000000000000000000000145b806106bf5750600160e060020a031982167fe89401a100000000000000000000000000000000000000000000000000000000145b806106f35750600160e060020a031982167f01ffc9a700000000000000000000000000000000000000000000000000000000145b92915050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b15801561074c57600080fd5b505af1158015610760573d6000803e3d6000fd5b505050506040513d602081101561077657600080fd5b5051600160a060020a03161461078b57600080fd5b6000848152600160209081526040918290209151855185936005019287929182918401908083835b602083106107d25780518252601f1990920191602091820191016107b3565b51815160209384036101000a6000190180199092169116179052920194855250604051938490038101909320845161081395919491909101925090506112e7565b5083600019167fd8c9334b1a9c2f9da342a0a2b32629c1a229b6445dad78947f674b44444a75508485604051808060200180602001838103835285818151815260200191508051906020019080838360005b8381101561087d578181015183820152602001610865565b50505050905090810190601f1680156108aa5780820380516001836020036101000a031916815260200191505b50838103825284518152845160209182019186019080838360005b838110156108dd5781810151838201526020016108c5565b50505050905090810190601f16801561090a5780820380516001836020036101000a031916815260200191505b5094505050505060405180910390a250505050565b60008281526001602081905260409091206060905b838311610a1e578284161580159061096d5750600083815260068201602052604081205460026000196101006001841615020190911604115b15610a1357600083815260068201602090815260409182902080548351601f600260001961010060018616150201909316929092049182018490048402810184019094528084529091830182828015610a075780601f106109dc57610100808354040283529160200191610a07565b820191906000526020600020905b8154815290600101906020018083116109ea57829003601f168201915b50505050509150610a23565b600290920291610934565b600092505b509250929050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610a7e57600080fd5b505af1158015610a92573d6000803e3d6000fd5b505050506040513d6020811015610aa857600080fd5b5051600160a060020a031614610abd57600080fd5b604080518082018252848152602080820185815260008881526001835284902092516003840155516004909201919091558151858152908101849052815186927f1d6f5e03d3f63eb58751986629a5439baee5079ff04f345becb66e23eb154e46928290030190a250505050565b6000908152600160208190526040909120015490565b600090815260016020526040902054600160a060020a031690565b600082815260016020908152604091829020915183516060936005019285929182918401908083835b60208310610ba45780518252601f199092019160209182019101610b85565b518151600019602094850361010090810a820192831692199390931691909117909252949092019687526040805197889003820188208054601f6002600183161590980290950116959095049283018290048202880182019052818752929450925050830182828015610c585780601f10610c2d57610100808354040283529160200191610c58565b820191906000526020600020905b815481529060010190602001808311610c3b57829003601f168201915b5050505050905092915050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610cb857600080fd5b505af1158015610ccc573d6000803e3d6000fd5b505050506040513d6020811015610ce257600080fd5b5051600160a060020a031614610cf757600080fd5b6000198301831615610d0857600080fd5b600084815260016020908152604080832086845260060182529091208351610d32928501906112e7565b50604051839085907faa121bbeef5f32f5961a2a28966e769023910fc9479059ee3495d4c1a696efe390600090a350505050565b6000818152600160208181526040928390206002908101805485516000199582161561010002959095011691909104601f81018390048302840183019094528383526060939091830182828015610dfe5780601f10610dd357610100808354040283529160200191610dfe565b820191906000526020600020905b815481529060010190602001808311610de157829003601f168201915b50505050509050919050565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610e5d57600080fd5b505af1158015610e71573d6000803e3d6000fd5b505050506040513d6020811015610e8757600080fd5b5051600160a060020a031614610e9c57600080fd5b60008381526001602090815260409091208351610ec1926002909201918501906112e7565b50604080516020808252845181830152845186937fb7d29e911041e8d9b843369e890bcb72c9388692ba48b65ac54e7214c4c348f79387939092839283019185019080838360005b83811015610f21578181015183820152602001610f09565b50505050905090810190601f168015610f4e5780820380516001836020036101000a031916815260200191505b509250505060405180910390a2505050565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610fb357600080fd5b505af1158015610fc7573d6000803e3d6000fd5b505050506040513d6020811015610fdd57600080fd5b5051600160a060020a031614610ff257600080fd5b60008381526001602090815260409091208351611017926007909201918501906112e7565b50604080516020808252845181830152845186937fc0b0fc07269fc2749adada3221c095a1d2187b2d075b51c915857b520f3a502193879390928392830191850190808383600083811015610f21578181015183820152602001610f09565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b1580156110c957600080fd5b505af11580156110dd573d6000803e3d6000fd5b505050506040513d60208110156110f357600080fd5b5051600160a060020a03161461110857600080fd5b6000838152600160208181526040928390209091018490558151848152915185927f0424b6fe0d9c3bdbece0e7879dc241bb0c22e900be8b6c168b4ee08bd9bf83bc92908290030190a2505050565b600090815260016020526040902060038101546004909101549091565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b1580156111c757600080fd5b505af11580156111db573d6000803e3d6000fd5b505050506040513d60208110156111f157600080fd5b5051600160a060020a03161461120657600080fd5b600083815260016020908152604091829020805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a0386169081179091558251908152915185927f52d7d861f09ab3d26239d492e8968629f95e9e318cf0b73bfddc441522a15fd292908290030190a2505050565b60008181526001602081815260409283902060070180548451600260001995831615610100029590950190911693909304601f81018390048302840183019094528383526060939091830182828015610dfe5780601f10610dd357610100808354040283529160200191610dfe565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061132857805160ff1916838001178555611355565b82800160010185558215611355579182015b8281111561135557825182559160200191906001019061133a565b50611361929150611365565b5090565b61137f91905b80821115611361576000815560010161136b565b905600a165627a7a723058207c07f172749d04c744f3b016e51a67e768bddea1f825f4b71024a33d8bd693380029";
        public GnosisSafeDeploymentBase() : base(BYTECODE) { }
        public GnosisSafeDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("string", "symbol", 2)]
        public virtual string Symbol { get; set; }
        [Parameter("string", "baseTokenURI", 3)]
        public virtual string BaseTokenURI { get; set; }
        [Parameter("string", "firstTokenURI", 4)]
        public virtual string FirstTokenURI { get; set; }
    }

    public partial class DefaultAdminRoleFunction : DefaultAdminRoleFunctionBase { }

    [Function("DEFAULT_ADMIN_ROLE", "bytes32")]
    public class DefaultAdminRoleFunctionBase : FunctionMessage
    {

    }

    public partial class MinterRoleFunction : MinterRoleFunctionBase { }

    [Function("MINTER_ROLE", "bytes32")]
    public class MinterRoleFunctionBase : FunctionMessage
    {

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

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class GetApprovedFunction : GetApprovedFunctionBase { }

    [Function("getApproved", "address")]
    public class GetApprovedFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class GetRoleAdminFunction : GetRoleAdminFunctionBase { }

    [Function("getRoleAdmin", "bytes32")]
    public class GetRoleAdminFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
    }

    public partial class GetRoleMemberFunction : GetRoleMemberFunctionBase { }

    [Function("getRoleMember", "address")]
    public class GetRoleMemberFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
        [Parameter("uint256", "index", 2)]
        public virtual BigInteger Index { get; set; }
    }

    public partial class GetRoleMemberCountFunction : GetRoleMemberCountFunctionBase { }

    [Function("getRoleMemberCount", "uint256")]
    public class GetRoleMemberCountFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
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

    public partial class IsApprovedForAllFunction : IsApprovedForAllFunctionBase { }

    [Function("isApprovedForAll", "bool")]
    public class IsApprovedForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2)]
        public virtual string Operator { get; set; }
    }

    public partial class MintFunction : MintFunctionBase { }

    [Function("mint")]
    public class MintFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("string", "newTokenURI", 2)]
        public virtual string NewTokenURI { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class OwnerOfFunction : OwnerOfFunctionBase { }

    [Function("ownerOf", "address")]
    public class OwnerOfFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class RenounceRoleFunction : RenounceRoleFunctionBase { }

    [Function("renounceRole")]
    public class RenounceRoleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public virtual byte[] Role { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
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

    public partial class SafeTransferFrom1Function : SafeTransferFrom1FunctionBase { }

    [Function("safeTransferFrom")]
    public class SafeTransferFrom1FunctionBase : FunctionMessage
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
        [Parameter("address", "operator", 1)]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 2)]
        public virtual bool Approved { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {

    }

    public partial class TokenByIndexFunction : TokenByIndexFunctionBase { }

    [Function("tokenByIndex", "uint256")]
    public class TokenByIndexFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "index", 1)]
        public virtual BigInteger Index { get; set; }
    }

    public partial class TokenOfOwnerByIndexFunction : TokenOfOwnerByIndexFunctionBase { }

    [Function("tokenOfOwnerByIndex", "uint256")]
    public class TokenOfOwnerByIndexFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "index", 2)]
        public virtual BigInteger Index { get; set; }
    }

    public partial class TokenURIFunction : TokenURIFunctionBase { }

    [Function("tokenURI", "string")]
    public class TokenURIFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class TotalSupplyFunction : TotalSupplyFunctionBase { }

    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunctionBase : FunctionMessage
    {

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

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "approved", 2, true )]
        public virtual string Approved { get; set; }
        [Parameter("uint256", "tokenId", 3, true )]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class ApprovalForAllEventDTO : ApprovalForAllEventDTOBase { }

    [Event("ApprovalForAll")]
    public class ApprovalForAllEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2, true )]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 3, false )]
        public virtual bool Approved { get; set; }
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

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true )]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "tokenId", 3, true )]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class DefaultAdminRoleOutputDTO : DefaultAdminRoleOutputDTOBase { }

    [FunctionOutput]
    public class DefaultAdminRoleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class MinterRoleOutputDTO : MinterRoleOutputDTOBase { }

    [FunctionOutput]
    public class MinterRoleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }



    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetApprovedOutputDTO : GetApprovedOutputDTOBase { }

    [FunctionOutput]
    public class GetApprovedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetRoleAdminOutputDTO : GetRoleAdminOutputDTOBase { }

    [FunctionOutput]
    public class GetRoleAdminOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class GetRoleMemberOutputDTO : GetRoleMemberOutputDTOBase { }

    [FunctionOutput]
    public class GetRoleMemberOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetRoleMemberCountOutputDTO : GetRoleMemberCountOutputDTOBase { }

    [FunctionOutput]
    public class GetRoleMemberCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class HasRoleOutputDTO : HasRoleOutputDTOBase { }

    [FunctionOutput]
    public class HasRoleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsApprovedForAllOutputDTO : IsApprovedForAllOutputDTOBase { }

    [FunctionOutput]
    public class IsApprovedForAllOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
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

    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TokenByIndexOutputDTO : TokenByIndexOutputDTOBase { }

    [FunctionOutput]
    public class TokenByIndexOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class TokenOfOwnerByIndexOutputDTO : TokenOfOwnerByIndexOutputDTOBase { }

    [FunctionOutput]
    public class TokenOfOwnerByIndexOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class TokenURIOutputDTO : TokenURIOutputDTOBase { }

    [FunctionOutput]
    public class TokenURIOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase { }

    [FunctionOutput]
    public class TotalSupplyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }


}
