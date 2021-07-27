Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports Nethereum.Web3
Imports Nethereum.RPC.Eth.DTOs
Imports Nethereum.Contracts.CQS
Imports Nethereum.Contracts
Imports System.Threading
Namespace Nethereum.ERC721.ERC721PresetMinterPauserAutoId.ContractDefinition

    
    
    Public Partial Class ERC721PresetMinterPauserAutoIdDeployment
     Inherits ERC721PresetMinterPauserAutoIdDeploymentBase
    
        Public Sub New()
            MyBase.New(DEFAULT_BYTECODE)
        End Sub
        
        Public Sub New(ByVal byteCode As String)
            MyBase.New(byteCode)
        End Sub
    
    End Class

    Public Class ERC721PresetMinterPauserAutoIdDeploymentBase 
            Inherits ContractDeploymentMessage
        
        Public Shared DEFAULT_BYTECODE As String = "608060405234801561001057600080fd5b50604051602080611400833981016040525160008054600160a060020a03909216600160a060020a03199092169190911790556113ae806100526000396000f3006080604052600436106100da5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166301ffc9a781146100df57806310f13a8c146101155780632203ab56146101b357806329cd62ea1461024d5780632dff69411461026b5780633b3b57de1461029557806359d1d43c146102c9578063623195b01461039c578063691f3431146103fc5780637737221314610414578063aa4cb54714610472578063c3d014d6146104d0578063c8690233146104eb578063d5fa2b001461051c578063e89401a114610540575b600080fd5b3480156100eb57600080fd5b50610101600160e060020a031960043516610558565b604080519115158252519081900360200190f35b34801561012157600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b195833595369560449491939091019190819084018382808284375050604080516020601f89358b018035918201839004830284018301909452808352979a9998810197919650918201945092508291508401838280828437509497506106f99650505050505050565b005b3480156101bf57600080fd5b506101ce60043560243561091f565b6040518083815260200180602001828103825283818151815260200191508051906020019080838360005b838110156102115781810151838201526020016101f9565b50505050905090810190601f16801561023e5780820380516001836020036101000a031916815260200191505b50935050505060405180910390f35b34801561025957600080fd5b506101b1600435602435604435610a2b565b34801561027757600080fd5b50610283600435610b2b565b60408051918252519081900360200190f35b3480156102a157600080fd5b506102ad600435610b41565b60408051600160a060020a039092168252519081900360200190f35b3480156102d557600080fd5b5060408051602060046024803582810135601f8101859004850286018501909652858552610327958335953695604494919390910191908190840183828082843750949750610b5c9650505050505050565b6040805160208082528351818301528351919283929083019185019080838360005b83811015610361578181015183820152602001610349565b50505050905090810190601f16801561038e5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b3480156103a857600080fd5b50604080516020600460443581810135601f81018490048402850184019095528484526101b1948235946024803595369594606494920191908190840183828082843750949750610c659650505050505050565b34801561040857600080fd5b50610327600435610d66565b34801561042057600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b1958335953695604494919390910191908190840183828082843750949750610e0a9650505050505050565b34801561047e57600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b1958335953695604494919390910191908190840183828082843750949750610f609650505050505050565b3480156104dc57600080fd5b506101b1600435602435611076565b3480156104f757600080fd5b50610503600435611157565b6040805192835260208301919091528051918290030190f35b34801561052857600080fd5b506101b1600435600160a060020a0360243516611174565b34801561054c57600080fd5b50610327600435611278565b6000600160e060020a031982167f3b3b57de0000000000000000000000000000000000000000000000000000000014806105bb5750600160e060020a031982167fd8389dc500000000000000000000000000000000000000000000000000000000145b806105ef5750600160e060020a031982167f691f343100000000000000000000000000000000000000000000000000000000145b806106235750600160e060020a031982167f2203ab5600000000000000000000000000000000000000000000000000000000145b806106575750600160e060020a031982167fc869023300000000000000000000000000000000000000000000000000000000145b8061068b5750600160e060020a031982167f59d1d43c00000000000000000000000000000000000000000000000000000000145b806106bf5750600160e060020a031982167fe89401a100000000000000000000000000000000000000000000000000000000145b806106f35750600160e060020a031982167f01ffc9a700000000000000000000000000000000000000000000000000000000145b92915050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b15801561074c57600080fd5b505af1158015610760573d6000803e3d6000fd5b505050506040513d602081101561077657600080fd5b5051600160a060020a03161461078b57600080fd5b6000848152600160209081526040918290209151855185936005019287929182918401908083835b602083106107d25780518252601f1990920191602091820191016107b3565b51815160209384036101000a6000190180199092169116179052920194855250604051938490038101909320845161081395919491909101925090506112e7565b5083600019167fd8c9334b1a9c2f9da342a0a2b32629c1a229b6445dad78947f674b44444a75508485604051808060200180602001838103835285818151815260200191508051906020019080838360005b8381101561087d578181015183820152602001610865565b50505050905090810190601f1680156108aa5780820380516001836020036101000a031916815260200191505b50838103825284518152845160209182019186019080838360005b838110156108dd5781810151838201526020016108c5565b50505050905090810190601f16801561090a5780820380516001836020036101000a031916815260200191505b5094505050505060405180910390a250505050565b60008281526001602081905260409091206060905b838311610a1e578284161580159061096d5750600083815260068201602052604081205460026000196101006001841615020190911604115b15610a1357600083815260068201602090815260409182902080548351601f600260001961010060018616150201909316929092049182018490048402810184019094528084529091830182828015610a075780601f106109dc57610100808354040283529160200191610a07565b820191906000526020600020905b8154815290600101906020018083116109ea57829003601f168201915b50505050509150610a23565b600290920291610934565b600092505b509250929050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610a7e57600080fd5b505af1158015610a92573d6000803e3d6000fd5b505050506040513d6020811015610aa857600080fd5b5051600160a060020a031614610abd57600080fd5b604080518082018252848152602080820185815260008881526001835284902092516003840155516004909201919091558151858152908101849052815186927f1d6f5e03d3f63eb58751986629a5439baee5079ff04f345becb66e23eb154e46928290030190a250505050565b6000908152600160208190526040909120015490565b600090815260016020526040902054600160a060020a031690565b600082815260016020908152604091829020915183516060936005019285929182918401908083835b60208310610ba45780518252601f199092019160209182019101610b85565b518151600019602094850361010090810a820192831692199390931691909117909252949092019687526040805197889003820188208054601f6002600183161590980290950116959095049283018290048202880182019052818752929450925050830182828015610c585780601f10610c2d57610100808354040283529160200191610c58565b820191906000526020600020905b815481529060010190602001808311610c3b57829003601f168201915b5050505050905092915050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610cb857600080fd5b505af1158015610ccc573d6000803e3d6000fd5b505050506040513d6020811015610ce257600080fd5b5051600160a060020a031614610cf757600080fd5b6000198301831615610d0857600080fd5b600084815260016020908152604080832086845260060182529091208351610d32928501906112e7565b50604051839085907faa121bbeef5f32f5961a2a28966e769023910fc9479059ee3495d4c1a696efe390600090a350505050565b6000818152600160208181526040928390206002908101805485516000199582161561010002959095011691909104601f81018390048302840183019094528383526060939091830182828015610dfe5780601f10610dd357610100808354040283529160200191610dfe565b820191906000526020600020905b815481529060010190602001808311610de157829003601f168201915b50505050509050919050565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610e5d57600080fd5b505af1158015610e71573d6000803e3d6000fd5b505050506040513d6020811015610e8757600080fd5b5051600160a060020a031614610e9c57600080fd5b60008381526001602090815260409091208351610ec1926002909201918501906112e7565b50604080516020808252845181830152845186937fb7d29e911041e8d9b843369e890bcb72c9388692ba48b65ac54e7214c4c348f79387939092839283019185019080838360005b83811015610f21578181015183820152602001610f09565b50505050905090810190601f168015610f4e5780820380516001836020036101000a031916815260200191505b509250505060405180910390a2505050565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610fb357600080fd5b505af1158015610fc7573d6000803e3d6000fd5b505050506040513d6020811015610fdd57600080fd5b5051600160a060020a031614610ff257600080fd5b60008381526001602090815260409091208351611017926007909201918501906112e7565b50604080516020808252845181830152845186937fc0b0fc07269fc2749adada3221c095a1d2187b2d075b51c915857b520f3a502193879390928392830191850190808383600083811015610f21578181015183820152602001610f09565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b1580156110c957600080fd5b505af11580156110dd573d6000803e3d6000fd5b505050506040513d60208110156110f357600080fd5b5051600160a060020a03161461110857600080fd5b6000838152600160208181526040928390209091018490558151848152915185927f0424b6fe0d9c3bdbece0e7879dc241bb0c22e900be8b6c168b4ee08bd9bf83bc92908290030190a2505050565b600090815260016020526040902060038101546004909101549091565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b1580156111c757600080fd5b505af11580156111db573d6000803e3d6000fd5b505050506040513d60208110156111f157600080fd5b5051600160a060020a03161461120657600080fd5b600083815260016020908152604091829020805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a0386169081179091558251908152915185927f52d7d861f09ab3d26239d492e8968629f95e9e318cf0b73bfddc441522a15fd292908290030190a2505050565b60008181526001602081815260409283902060070180548451600260001995831615610100029590950190911693909304601f81018390048302840183019094528383526060939091830182828015610dfe5780601f10610dd357610100808354040283529160200191610dfe565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061132857805160ff1916838001178555611355565b82800160010185558215611355579182015b8281111561135557825182559160200191906001019061133a565b50611361929150611365565b5090565b61137f91905b80821115611361576000815560010161136b565b905600a165627a7a723058207c07f172749d04c744f3b016e51a67e768bddea1f825f4b71024a33d8bd693380029"
        
        Public Sub New()
            MyBase.New(DEFAULT_BYTECODE)
        End Sub
        
        Public Sub New(ByVal byteCode As String)
            MyBase.New(byteCode)
        End Sub
        
        <[Parameter]("string", "name", 1)>
        Public Overridable Property [Name] As String
        <[Parameter]("string", "symbol", 2)>
        Public Overridable Property [Symbol] As String
        <[Parameter]("string", "baseURI", 3)>
        Public Overridable Property [BaseURI] As String
    
    End Class    
    
    Public Partial Class DEFAULT_ADMIN_ROLEFunction
        Inherits DEFAULT_ADMIN_ROLEFunctionBase
    End Class

        <[Function]("DEFAULT_ADMIN_ROLE", "bytes32")>
    Public Class DEFAULT_ADMIN_ROLEFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class MINTER_ROLEFunction
        Inherits MINTER_ROLEFunctionBase
    End Class

        <[Function]("MINTER_ROLE", "bytes32")>
    Public Class MINTER_ROLEFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class PAUSER_ROLEFunction
        Inherits PAUSER_ROLEFunctionBase
    End Class

        <[Function]("PAUSER_ROLE", "bytes32")>
    Public Class PAUSER_ROLEFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class ApproveFunction
        Inherits ApproveFunctionBase
    End Class

        <[Function]("approve")>
    Public Class ApproveFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "to", 1)>
        Public Overridable Property [To] As String
        <[Parameter]("uint256", "tokenId", 2)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class
    
    
    Public Partial Class BalanceOfFunction
        Inherits BalanceOfFunctionBase
    End Class

        <[Function]("balanceOf", "uint256")>
    Public Class BalanceOfFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "owner", 1)>
        Public Overridable Property [Owner] As String
    
    End Class
    
    
    Public Partial Class BaseURIFunction
        Inherits BaseURIFunctionBase
    End Class

        <[Function]("baseURI", "string")>
    Public Class BaseURIFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class BurnFunction
        Inherits BurnFunctionBase
    End Class

        <[Function]("burn")>
    Public Class BurnFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("uint256", "tokenId", 1)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class
    
    
    Public Partial Class GetApprovedFunction
        Inherits GetApprovedFunctionBase
    End Class

        <[Function]("getApproved", "address")>
    Public Class GetApprovedFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("uint256", "tokenId", 1)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class
    
    
    Public Partial Class GetRoleAdminFunction
        Inherits GetRoleAdminFunctionBase
    End Class

        <[Function]("getRoleAdmin", "bytes32")>
    Public Class GetRoleAdminFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "role", 1)>
        Public Overridable Property [Role] As Byte()
    
    End Class
    
    
    Public Partial Class GetRoleMemberFunction
        Inherits GetRoleMemberFunctionBase
    End Class

        <[Function]("getRoleMember", "address")>
    Public Class GetRoleMemberFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "role", 1)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("uint256", "index", 2)>
        Public Overridable Property [Index] As BigInteger
    
    End Class
    
    
    Public Partial Class GetRoleMemberCountFunction
        Inherits GetRoleMemberCountFunctionBase
    End Class

        <[Function]("getRoleMemberCount", "uint256")>
    Public Class GetRoleMemberCountFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "role", 1)>
        Public Overridable Property [Role] As Byte()
    
    End Class
    
    
    Public Partial Class GrantRoleFunction
        Inherits GrantRoleFunctionBase
    End Class

        <[Function]("grantRole")>
    Public Class GrantRoleFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "role", 1)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("address", "account", 2)>
        Public Overridable Property [Account] As String
    
    End Class
    
    
    Public Partial Class HasRoleFunction
        Inherits HasRoleFunctionBase
    End Class

        <[Function]("hasRole", "bool")>
    Public Class HasRoleFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "role", 1)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("address", "account", 2)>
        Public Overridable Property [Account] As String
    
    End Class
    
    
    Public Partial Class IsApprovedForAllFunction
        Inherits IsApprovedForAllFunctionBase
    End Class

        <[Function]("isApprovedForAll", "bool")>
    Public Class IsApprovedForAllFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "owner", 1)>
        Public Overridable Property [Owner] As String
        <[Parameter]("address", "operator", 2)>
        Public Overridable Property [Operator] As String
    
    End Class
    
    
    Public Partial Class MintFunction
        Inherits MintFunctionBase
    End Class

        <[Function]("mint")>
    Public Class MintFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "to", 1)>
        Public Overridable Property [To] As String
    
    End Class
    
    
    Public Partial Class NameFunction
        Inherits NameFunctionBase
    End Class

        <[Function]("name", "string")>
    Public Class NameFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class OwnerOfFunction
        Inherits OwnerOfFunctionBase
    End Class

        <[Function]("ownerOf", "address")>
    Public Class OwnerOfFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("uint256", "tokenId", 1)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class
    
    
    Public Partial Class PauseFunction
        Inherits PauseFunctionBase
    End Class

        <[Function]("pause")>
    Public Class PauseFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class PausedFunction
        Inherits PausedFunctionBase
    End Class

        <[Function]("paused", "bool")>
    Public Class PausedFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class RenounceRoleFunction
        Inherits RenounceRoleFunctionBase
    End Class

        <[Function]("renounceRole")>
    Public Class RenounceRoleFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "role", 1)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("address", "account", 2)>
        Public Overridable Property [Account] As String
    
    End Class
    
    
    Public Partial Class RevokeRoleFunction
        Inherits RevokeRoleFunctionBase
    End Class

        <[Function]("revokeRole")>
    Public Class RevokeRoleFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "role", 1)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("address", "account", 2)>
        Public Overridable Property [Account] As String
    
    End Class
    
    
    Public Partial Class SafeTransferFromFunction
        Inherits SafeTransferFromFunctionBase
    End Class

        <[Function]("safeTransferFrom")>
    Public Class SafeTransferFromFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "from", 1)>
        Public Overridable Property [From] As String
        <[Parameter]("address", "to", 2)>
        Public Overridable Property [To] As String
        <[Parameter]("uint256", "tokenId", 3)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class
    
    
    Public Partial Class SafeTransferFrom1Function
        Inherits SafeTransferFrom1FunctionBase
    End Class

        <[Function]("safeTransferFrom")>
    Public Class SafeTransferFrom1FunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "from", 1)>
        Public Overridable Property [From] As String
        <[Parameter]("address", "to", 2)>
        Public Overridable Property [To] As String
        <[Parameter]("uint256", "tokenId", 3)>
        Public Overridable Property [TokenId] As BigInteger
        <[Parameter]("bytes", "_data", 4)>
        Public Overridable Property [Data] As Byte()
    
    End Class
    
    
    Public Partial Class SetApprovalForAllFunction
        Inherits SetApprovalForAllFunctionBase
    End Class

        <[Function]("setApprovalForAll")>
    Public Class SetApprovalForAllFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "operator", 1)>
        Public Overridable Property [Operator] As String
        <[Parameter]("bool", "approved", 2)>
        Public Overridable Property [Approved] As Boolean
    
    End Class
    
    
    Public Partial Class SupportsInterfaceFunction
        Inherits SupportsInterfaceFunctionBase
    End Class

        <[Function]("supportsInterface", "bool")>
    Public Class SupportsInterfaceFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes4", "interfaceId", 1)>
        Public Overridable Property [InterfaceId] As Byte()
    
    End Class
    
    
    Public Partial Class SymbolFunction
        Inherits SymbolFunctionBase
    End Class

        <[Function]("symbol", "string")>
    Public Class SymbolFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class TokenByIndexFunction
        Inherits TokenByIndexFunctionBase
    End Class

        <[Function]("tokenByIndex", "uint256")>
    Public Class TokenByIndexFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("uint256", "index", 1)>
        Public Overridable Property [Index] As BigInteger
    
    End Class
    
    
    Public Partial Class TokenOfOwnerByIndexFunction
        Inherits TokenOfOwnerByIndexFunctionBase
    End Class

        <[Function]("tokenOfOwnerByIndex", "uint256")>
    Public Class TokenOfOwnerByIndexFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "owner", 1)>
        Public Overridable Property [Owner] As String
        <[Parameter]("uint256", "index", 2)>
        Public Overridable Property [Index] As BigInteger
    
    End Class
    
    
    Public Partial Class TokenURIFunction
        Inherits TokenURIFunctionBase
    End Class

        <[Function]("tokenURI", "string")>
    Public Class TokenURIFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("uint256", "tokenId", 1)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class
    
    
    Public Partial Class TotalSupplyFunction
        Inherits TotalSupplyFunctionBase
    End Class

        <[Function]("totalSupply", "uint256")>
    Public Class TotalSupplyFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class TransferFromFunction
        Inherits TransferFromFunctionBase
    End Class

        <[Function]("transferFrom")>
    Public Class TransferFromFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("address", "from", 1)>
        Public Overridable Property [From] As String
        <[Parameter]("address", "to", 2)>
        Public Overridable Property [To] As String
        <[Parameter]("uint256", "tokenId", 3)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class
    
    
    Public Partial Class UnpauseFunction
        Inherits UnpauseFunctionBase
    End Class

        <[Function]("unpause")>
    Public Class UnpauseFunctionBase
        Inherits FunctionMessage
    

    
    End Class
    
    
    Public Partial Class ApprovalEventDTO
        Inherits ApprovalEventDTOBase
    End Class

    <[Event]("Approval")>
    Public Class ApprovalEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("address", "owner", 1, true)>
        Public Overridable Property [Owner] As String
        <[Parameter]("address", "approved", 2, true)>
        Public Overridable Property [Approved] As String
        <[Parameter]("uint256", "tokenId", 3, true)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class    
    
    Public Partial Class ApprovalForAllEventDTO
        Inherits ApprovalForAllEventDTOBase
    End Class

    <[Event]("ApprovalForAll")>
    Public Class ApprovalForAllEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("address", "owner", 1, true)>
        Public Overridable Property [Owner] As String
        <[Parameter]("address", "operator", 2, true)>
        Public Overridable Property [Operator] As String
        <[Parameter]("bool", "approved", 3, false)>
        Public Overridable Property [Approved] As Boolean
    
    End Class    
    
    Public Partial Class PausedEventDTO
        Inherits PausedEventDTOBase
    End Class

    <[Event]("Paused")>
    Public Class PausedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("address", "account", 1, false)>
        Public Overridable Property [Account] As String
    
    End Class    
    
    Public Partial Class RoleAdminChangedEventDTO
        Inherits RoleAdminChangedEventDTOBase
    End Class

    <[Event]("RoleAdminChanged")>
    Public Class RoleAdminChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "role", 1, true)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("bytes32", "previousAdminRole", 2, true)>
        Public Overridable Property [PreviousAdminRole] As Byte()
        <[Parameter]("bytes32", "newAdminRole", 3, true)>
        Public Overridable Property [NewAdminRole] As Byte()
    
    End Class    
    
    Public Partial Class RoleGrantedEventDTO
        Inherits RoleGrantedEventDTOBase
    End Class

    <[Event]("RoleGranted")>
    Public Class RoleGrantedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "role", 1, true)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("address", "account", 2, true)>
        Public Overridable Property [Account] As String
        <[Parameter]("address", "sender", 3, true)>
        Public Overridable Property [Sender] As String
    
    End Class    
    
    Public Partial Class RoleRevokedEventDTO
        Inherits RoleRevokedEventDTOBase
    End Class

    <[Event]("RoleRevoked")>
    Public Class RoleRevokedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "role", 1, true)>
        Public Overridable Property [Role] As Byte()
        <[Parameter]("address", "account", 2, true)>
        Public Overridable Property [Account] As String
        <[Parameter]("address", "sender", 3, true)>
        Public Overridable Property [Sender] As String
    
    End Class    
    
    Public Partial Class TransferEventDTO
        Inherits TransferEventDTOBase
    End Class

    <[Event]("Transfer")>
    Public Class TransferEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("address", "from", 1, true)>
        Public Overridable Property [From] As String
        <[Parameter]("address", "to", 2, true)>
        Public Overridable Property [To] As String
        <[Parameter]("uint256", "tokenId", 3, true)>
        Public Overridable Property [TokenId] As BigInteger
    
    End Class    
    
    Public Partial Class UnpausedEventDTO
        Inherits UnpausedEventDTOBase
    End Class

    <[Event]("Unpaused")>
    Public Class UnpausedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("address", "account", 1, false)>
        Public Overridable Property [Account] As String
    
    End Class    
    
    Public Partial Class DEFAULT_ADMIN_ROLEOutputDTO
        Inherits DEFAULT_ADMIN_ROLEOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class DEFAULT_ADMIN_ROLEOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bytes32", "", 1)>
        Public Overridable Property [ReturnValue1] As Byte()
    
    End Class    
    
    Public Partial Class MINTER_ROLEOutputDTO
        Inherits MINTER_ROLEOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class MINTER_ROLEOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bytes32", "", 1)>
        Public Overridable Property [ReturnValue1] As Byte()
    
    End Class    
    
    Public Partial Class PAUSER_ROLEOutputDTO
        Inherits PAUSER_ROLEOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class PAUSER_ROLEOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bytes32", "", 1)>
        Public Overridable Property [ReturnValue1] As Byte()
    
    End Class    
    
    
    
    Public Partial Class BalanceOfOutputDTO
        Inherits BalanceOfOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class BalanceOfOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("uint256", "", 1)>
        Public Overridable Property [ReturnValue1] As BigInteger
    
    End Class    
    
    Public Partial Class BaseURIOutputDTO
        Inherits BaseURIOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class BaseURIOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("string", "", 1)>
        Public Overridable Property [ReturnValue1] As String
    
    End Class    
    
    
    
    Public Partial Class GetApprovedOutputDTO
        Inherits GetApprovedOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class GetApprovedOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("address", "", 1)>
        Public Overridable Property [ReturnValue1] As String
    
    End Class    
    
    Public Partial Class GetRoleAdminOutputDTO
        Inherits GetRoleAdminOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class GetRoleAdminOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bytes32", "", 1)>
        Public Overridable Property [ReturnValue1] As Byte()
    
    End Class    
    
    Public Partial Class GetRoleMemberOutputDTO
        Inherits GetRoleMemberOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class GetRoleMemberOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("address", "", 1)>
        Public Overridable Property [ReturnValue1] As String
    
    End Class    
    
    Public Partial Class GetRoleMemberCountOutputDTO
        Inherits GetRoleMemberCountOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class GetRoleMemberCountOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("uint256", "", 1)>
        Public Overridable Property [ReturnValue1] As BigInteger
    
    End Class    
    
    
    
    Public Partial Class HasRoleOutputDTO
        Inherits HasRoleOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class HasRoleOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bool", "", 1)>
        Public Overridable Property [ReturnValue1] As Boolean
    
    End Class    
    
    Public Partial Class IsApprovedForAllOutputDTO
        Inherits IsApprovedForAllOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class IsApprovedForAllOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bool", "", 1)>
        Public Overridable Property [ReturnValue1] As Boolean
    
    End Class    
    
    
    
    Public Partial Class NameOutputDTO
        Inherits NameOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class NameOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("string", "", 1)>
        Public Overridable Property [ReturnValue1] As String
    
    End Class    
    
    Public Partial Class OwnerOfOutputDTO
        Inherits OwnerOfOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class OwnerOfOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("address", "", 1)>
        Public Overridable Property [ReturnValue1] As String
    
    End Class    
    
    
    
    Public Partial Class PausedOutputDTO
        Inherits PausedOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class PausedOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bool", "", 1)>
        Public Overridable Property [ReturnValue1] As Boolean
    
    End Class    
    
    
    
    
    
    
    
    
    
    
    
    Public Partial Class SupportsInterfaceOutputDTO
        Inherits SupportsInterfaceOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class SupportsInterfaceOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bool", "", 1)>
        Public Overridable Property [ReturnValue1] As Boolean
    
    End Class    
    
    Public Partial Class SymbolOutputDTO
        Inherits SymbolOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class SymbolOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("string", "", 1)>
        Public Overridable Property [ReturnValue1] As String
    
    End Class    
    
    Public Partial Class TokenByIndexOutputDTO
        Inherits TokenByIndexOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class TokenByIndexOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("uint256", "", 1)>
        Public Overridable Property [ReturnValue1] As BigInteger
    
    End Class    
    
    Public Partial Class TokenOfOwnerByIndexOutputDTO
        Inherits TokenOfOwnerByIndexOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class TokenOfOwnerByIndexOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("uint256", "", 1)>
        Public Overridable Property [ReturnValue1] As BigInteger
    
    End Class    
    
    Public Partial Class TokenURIOutputDTO
        Inherits TokenURIOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class TokenURIOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("string", "", 1)>
        Public Overridable Property [ReturnValue1] As String
    
    End Class    
    
    Public Partial Class TotalSupplyOutputDTO
        Inherits TotalSupplyOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class TotalSupplyOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("uint256", "", 1)>
        Public Overridable Property [ReturnValue1] As BigInteger
    
    End Class    
    
    
    

End Namespace
