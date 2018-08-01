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
Namespace Nethereum.ENS.PublicResolver.ContractDefinition

    
    Public Partial Class PublicResolverDeployment
     Inherits PublicResolverDeploymentBase
    
        Public Sub New()
            MyBase.New(DEFAULT_BYTECODE)
        End Sub
        
        Public Sub New(ByVal byteCode As String)
            MyBase.New(byteCode)
        End Sub
    
    End Class

    Public Class PublicResolverDeploymentBase 
            Inherits ContractDeploymentMessage
        
        Public Shared DEFAULT_BYTECODE As String = "608060405234801561001057600080fd5b50604051602080611400833981016040525160008054600160a060020a03909216600160a060020a03199092169190911790556113ae806100526000396000f3006080604052600436106100da5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166301ffc9a781146100df57806310f13a8c146101155780632203ab56146101b357806329cd62ea1461024d5780632dff69411461026b5780633b3b57de1461029557806359d1d43c146102c9578063623195b01461039c578063691f3431146103fc5780637737221314610414578063aa4cb54714610472578063c3d014d6146104d0578063c8690233146104eb578063d5fa2b001461051c578063e89401a114610540575b600080fd5b3480156100eb57600080fd5b50610101600160e060020a031960043516610558565b604080519115158252519081900360200190f35b34801561012157600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b195833595369560449491939091019190819084018382808284375050604080516020601f89358b018035918201839004830284018301909452808352979a9998810197919650918201945092508291508401838280828437509497506106f99650505050505050565b005b3480156101bf57600080fd5b506101ce60043560243561091f565b6040518083815260200180602001828103825283818151815260200191508051906020019080838360005b838110156102115781810151838201526020016101f9565b50505050905090810190601f16801561023e5780820380516001836020036101000a031916815260200191505b50935050505060405180910390f35b34801561025957600080fd5b506101b1600435602435604435610a2b565b34801561027757600080fd5b50610283600435610b2b565b60408051918252519081900360200190f35b3480156102a157600080fd5b506102ad600435610b41565b60408051600160a060020a039092168252519081900360200190f35b3480156102d557600080fd5b5060408051602060046024803582810135601f8101859004850286018501909652858552610327958335953695604494919390910191908190840183828082843750949750610b5c9650505050505050565b6040805160208082528351818301528351919283929083019185019080838360005b83811015610361578181015183820152602001610349565b50505050905090810190601f16801561038e5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b3480156103a857600080fd5b50604080516020600460443581810135601f81018490048402850184019095528484526101b1948235946024803595369594606494920191908190840183828082843750949750610c659650505050505050565b34801561040857600080fd5b50610327600435610d66565b34801561042057600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b1958335953695604494919390910191908190840183828082843750949750610e0a9650505050505050565b34801561047e57600080fd5b5060408051602060046024803582810135601f81018590048502860185019096528585526101b1958335953695604494919390910191908190840183828082843750949750610f609650505050505050565b3480156104dc57600080fd5b506101b1600435602435611076565b3480156104f757600080fd5b50610503600435611157565b6040805192835260208301919091528051918290030190f35b34801561052857600080fd5b506101b1600435600160a060020a0360243516611174565b34801561054c57600080fd5b50610327600435611278565b6000600160e060020a031982167f3b3b57de0000000000000000000000000000000000000000000000000000000014806105bb5750600160e060020a031982167fd8389dc500000000000000000000000000000000000000000000000000000000145b806105ef5750600160e060020a031982167f691f343100000000000000000000000000000000000000000000000000000000145b806106235750600160e060020a031982167f2203ab5600000000000000000000000000000000000000000000000000000000145b806106575750600160e060020a031982167fc869023300000000000000000000000000000000000000000000000000000000145b8061068b5750600160e060020a031982167f59d1d43c00000000000000000000000000000000000000000000000000000000145b806106bf5750600160e060020a031982167fe89401a100000000000000000000000000000000000000000000000000000000145b806106f35750600160e060020a031982167f01ffc9a700000000000000000000000000000000000000000000000000000000145b92915050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b15801561074c57600080fd5b505af1158015610760573d6000803e3d6000fd5b505050506040513d602081101561077657600080fd5b5051600160a060020a03161461078b57600080fd5b6000848152600160209081526040918290209151855185936005019287929182918401908083835b602083106107d25780518252601f1990920191602091820191016107b3565b51815160209384036101000a6000190180199092169116179052920194855250604051938490038101909320845161081395919491909101925090506112e7565b5083600019167fd8c9334b1a9c2f9da342a0a2b32629c1a229b6445dad78947f674b44444a75508485604051808060200180602001838103835285818151815260200191508051906020019080838360005b8381101561087d578181015183820152602001610865565b50505050905090810190601f1680156108aa5780820380516001836020036101000a031916815260200191505b50838103825284518152845160209182019186019080838360005b838110156108dd5781810151838201526020016108c5565b50505050905090810190601f16801561090a5780820380516001836020036101000a031916815260200191505b5094505050505060405180910390a250505050565b60008281526001602081905260409091206060905b838311610a1e578284161580159061096d5750600083815260068201602052604081205460026000196101006001841615020190911604115b15610a1357600083815260068201602090815260409182902080548351601f600260001961010060018616150201909316929092049182018490048402810184019094528084529091830182828015610a075780601f106109dc57610100808354040283529160200191610a07565b820191906000526020600020905b8154815290600101906020018083116109ea57829003601f168201915b50505050509150610a23565b600290920291610934565b600092505b509250929050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610a7e57600080fd5b505af1158015610a92573d6000803e3d6000fd5b505050506040513d6020811015610aa857600080fd5b5051600160a060020a031614610abd57600080fd5b604080518082018252848152602080820185815260008881526001835284902092516003840155516004909201919091558151858152908101849052815186927f1d6f5e03d3f63eb58751986629a5439baee5079ff04f345becb66e23eb154e46928290030190a250505050565b6000908152600160208190526040909120015490565b600090815260016020526040902054600160a060020a031690565b600082815260016020908152604091829020915183516060936005019285929182918401908083835b60208310610ba45780518252601f199092019160209182019101610b85565b518151600019602094850361010090810a820192831692199390931691909117909252949092019687526040805197889003820188208054601f6002600183161590980290950116959095049283018290048202880182019052818752929450925050830182828015610c585780601f10610c2d57610100808354040283529160200191610c58565b820191906000526020600020905b815481529060010190602001808311610c3b57829003601f168201915b5050505050905092915050565b600080546040805160e060020a6302571be302815260048101879052905186933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610cb857600080fd5b505af1158015610ccc573d6000803e3d6000fd5b505050506040513d6020811015610ce257600080fd5b5051600160a060020a031614610cf757600080fd5b6000198301831615610d0857600080fd5b600084815260016020908152604080832086845260060182529091208351610d32928501906112e7565b50604051839085907faa121bbeef5f32f5961a2a28966e769023910fc9479059ee3495d4c1a696efe390600090a350505050565b6000818152600160208181526040928390206002908101805485516000199582161561010002959095011691909104601f81018390048302840183019094528383526060939091830182828015610dfe5780601f10610dd357610100808354040283529160200191610dfe565b820191906000526020600020905b815481529060010190602001808311610de157829003601f168201915b50505050509050919050565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610e5d57600080fd5b505af1158015610e71573d6000803e3d6000fd5b505050506040513d6020811015610e8757600080fd5b5051600160a060020a031614610e9c57600080fd5b60008381526001602090815260409091208351610ec1926002909201918501906112e7565b50604080516020808252845181830152845186937fb7d29e911041e8d9b843369e890bcb72c9388692ba48b65ac54e7214c4c348f79387939092839283019185019080838360005b83811015610f21578181015183820152602001610f09565b50505050905090810190601f168015610f4e5780820380516001836020036101000a031916815260200191505b509250505060405180910390a2505050565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b158015610fb357600080fd5b505af1158015610fc7573d6000803e3d6000fd5b505050506040513d6020811015610fdd57600080fd5b5051600160a060020a031614610ff257600080fd5b60008381526001602090815260409091208351611017926007909201918501906112e7565b50604080516020808252845181830152845186937fc0b0fc07269fc2749adada3221c095a1d2187b2d075b51c915857b520f3a502193879390928392830191850190808383600083811015610f21578181015183820152602001610f09565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b1580156110c957600080fd5b505af11580156110dd573d6000803e3d6000fd5b505050506040513d60208110156110f357600080fd5b5051600160a060020a03161461110857600080fd5b6000838152600160208181526040928390209091018490558151848152915185927f0424b6fe0d9c3bdbece0e7879dc241bb0c22e900be8b6c168b4ee08bd9bf83bc92908290030190a2505050565b600090815260016020526040902060038101546004909101549091565b600080546040805160e060020a6302571be302815260048101869052905185933393600160a060020a0316926302571be39260248083019360209383900390910190829087803b1580156111c757600080fd5b505af11580156111db573d6000803e3d6000fd5b505050506040513d60208110156111f157600080fd5b5051600160a060020a03161461120657600080fd5b600083815260016020908152604091829020805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a0386169081179091558251908152915185927f52d7d861f09ab3d26239d492e8968629f95e9e318cf0b73bfddc441522a15fd292908290030190a2505050565b60008181526001602081815260409283902060070180548451600260001995831615610100029590950190911693909304601f81018390048302840183019094528383526060939091830182828015610dfe5780601f10610dd357610100808354040283529160200191610dfe565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061132857805160ff1916838001178555611355565b82800160010185558215611355579182015b8281111561135557825182559160200191906001019061133a565b50611361929150611365565b5090565b61137f91905b80821115611361576000815560010161136b565b905600a165627a7a723058207c07f172749d04c744f3b016e51a67e768bddea1f825f4b71024a33d8bd693380029"
        
        Public Sub New()
            MyBase.New(DEFAULT_BYTECODE)
        End Sub
        
        Public Sub New(ByVal byteCode As String)
            MyBase.New(byteCode)
        End Sub
        
        <[Parameter]("address", "ensAddr", 1)>
        Public Property [EnsAddr] As String
    
    End Class    
    
    Public Partial Class SupportsInterfaceFunction
        Inherits SupportsInterfaceFunctionBase
    End Class

        <[Function]("supportsInterface", "bool")>
    Public Class SupportsInterfaceFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes4", "interfaceID", 1)>
        Public Property [InterfaceID] As Byte()
    
    End Class
    
    
    Public Partial Class SetTextFunction
        Inherits SetTextFunctionBase
    End Class

        <[Function]("setText")>
    Public Class SetTextFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("string", "key", 2)>
        Public Property [Key] As String
        <[Parameter]("string", "value", 3)>
        Public Property [Value] As String
    
    End Class
    
    
    Public Partial Class ABIFunction
        Inherits ABIFunctionBase
    End Class

        <[Function]("ABI", GetType(ABIOutputDTO))>
    Public Class ABIFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("uint256", "contentTypes", 2)>
        Public Property [ContentTypes] As BigInteger
    
    End Class
    
    
    Public Partial Class SetPubkeyFunction
        Inherits SetPubkeyFunctionBase
    End Class

        <[Function]("setPubkey")>
    Public Class SetPubkeyFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("bytes32", "x", 2)>
        Public Property [X] As Byte()
        <[Parameter]("bytes32", "y", 3)>
        Public Property [Y] As Byte()
    
    End Class
    
    
    Public Partial Class ContentFunction
        Inherits ContentFunctionBase
    End Class

        <[Function]("content", "bytes32")>
    Public Class ContentFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
    
    End Class
    
    
    Public Partial Class AddrFunction
        Inherits AddrFunctionBase
    End Class

        <[Function]("addr", "address")>
    Public Class AddrFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
    
    End Class
    
    
    Public Partial Class TextFunction
        Inherits TextFunctionBase
    End Class

        <[Function]("text", "string")>
    Public Class TextFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("string", "key", 2)>
        Public Property [Key] As String
    
    End Class
    
    
    Public Partial Class SetABIFunction
        Inherits SetABIFunctionBase
    End Class

        <[Function]("setABI")>
    Public Class SetABIFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("uint256", "contentType", 2)>
        Public Property [ContentType] As BigInteger
        <[Parameter]("bytes", "data", 3)>
        Public Property [Data] As Byte()
    
    End Class
    
    
    Public Partial Class NameFunction
        Inherits NameFunctionBase
    End Class

        <[Function]("name", "string")>
    Public Class NameFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
    
    End Class
    
    
    Public Partial Class SetNameFunction
        Inherits SetNameFunctionBase
    End Class

        <[Function]("setName")>
    Public Class SetNameFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("string", "name", 2)>
        Public Property [Name] As String
    
    End Class
    
    
    Public Partial Class SetMultihashFunction
        Inherits SetMultihashFunctionBase
    End Class

        <[Function]("setMultihash")>
    Public Class SetMultihashFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("bytes", "hash", 2)>
        Public Property [Hash] As Byte()
    
    End Class
    
    
    Public Partial Class SetContentFunction
        Inherits SetContentFunctionBase
    End Class

        <[Function]("setContent")>
    Public Class SetContentFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("bytes32", "hash", 2)>
        Public Property [Hash] As Byte()
    
    End Class
    
    
    Public Partial Class PubkeyFunction
        Inherits PubkeyFunctionBase
    End Class

        <[Function]("pubkey", GetType(PubkeyOutputDTO))>
    Public Class PubkeyFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
    
    End Class
    
    
    Public Partial Class SetAddrFunction
        Inherits SetAddrFunctionBase
    End Class

        <[Function]("setAddr")>
    Public Class SetAddrFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
        <[Parameter]("address", "addr", 2)>
        Public Property [Addr] As String
    
    End Class
    
    
    Public Partial Class MultihashFunction
        Inherits MultihashFunctionBase
    End Class

        <[Function]("multihash", "bytes")>
    Public Class MultihashFunctionBase
        Inherits FunctionMessage
    
        <[Parameter]("bytes32", "node", 1)>
        Public Property [Node] As Byte()
    
    End Class
    
    
    Public Partial Class AddrChangedEventDTO
        Inherits AddrChangedEventDTOBase
    End Class

    <[Event]("AddrChanged")>
    Public Class AddrChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "node", 1, true)>
        Public Property [Node] As Byte()
        <[Parameter]("address", "a", 2, false)>
        Public Property [A] As String
    
    End Class    
    
    Public Partial Class ContentChangedEventDTO
        Inherits ContentChangedEventDTOBase
    End Class

    <[Event]("ContentChanged")>
    Public Class ContentChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "node", 1, true)>
        Public Property [Node] As Byte()
        <[Parameter]("bytes32", "hash", 2, false)>
        Public Property [Hash] As Byte()
    
    End Class    
    
    Public Partial Class NameChangedEventDTO
        Inherits NameChangedEventDTOBase
    End Class

    <[Event]("NameChanged")>
    Public Class NameChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "node", 1, true)>
        Public Property [Node] As Byte()
        <[Parameter]("string", "name", 2, false)>
        Public Property [Name] As String
    
    End Class    
    
    Public Partial Class ABIChangedEventDTO
        Inherits ABIChangedEventDTOBase
    End Class

    <[Event]("ABIChanged")>
    Public Class ABIChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "node", 1, true)>
        Public Property [Node] As Byte()
        <[Parameter]("uint256", "contentType", 2, true)>
        Public Property [ContentType] As BigInteger
    
    End Class    
    
    Public Partial Class PubkeyChangedEventDTO
        Inherits PubkeyChangedEventDTOBase
    End Class

    <[Event]("PubkeyChanged")>
    Public Class PubkeyChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "node", 1, true)>
        Public Property [Node] As Byte()
        <[Parameter]("bytes32", "x", 2, false)>
        Public Property [X] As Byte()
        <[Parameter]("bytes32", "y", 3, false)>
        Public Property [Y] As Byte()
    
    End Class    
    
    Public Partial Class TextChangedEventDTO
        Inherits TextChangedEventDTOBase
    End Class

    <[Event]("TextChanged")>
    Public Class TextChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "node", 1, true)>
        Public Property [Node] As Byte()
        <[Parameter]("string", "indexedKey", 2, false)>
        Public Property [IndexedKey] As String
        <[Parameter]("string", "key", 3, false)>
        Public Property [Key] As String
    
    End Class    
    
    Public Partial Class MultihashChangedEventDTO
        Inherits MultihashChangedEventDTOBase
    End Class

    <[Event]("MultihashChanged")>
    Public Class MultihashChangedEventDTOBase
        Implements IEventDTO
        
        <[Parameter]("bytes32", "node", 1, true)>
        Public Property [Node] As Byte()
        <[Parameter]("bytes", "hash", 2, false)>
        Public Property [Hash] As Byte()
    
    End Class    
    
    Public Partial Class SupportsInterfaceOutputDTO
        Inherits SupportsInterfaceOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class SupportsInterfaceOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bool", "", 1)>
        Public Property [ReturnValue1] As Boolean
    
    End Class    
    
    
    
    Public Partial Class ABIOutputDTO
        Inherits ABIOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class ABIOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("uint256", "contentType", 1)>
        Public Property [ContentType] As BigInteger
        <[Parameter]("bytes", "data", 2)>
        Public Property [Data] As Byte()
    
    End Class    
    
    
    
    Public Partial Class ContentOutputDTO
        Inherits ContentOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class ContentOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bytes32", "", 1)>
        Public Property [ReturnValue1] As Byte()
    
    End Class    
    
    Public Partial Class AddrOutputDTO
        Inherits AddrOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class AddrOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("address", "", 1)>
        Public Property [ReturnValue1] As String
    
    End Class    
    
    Public Partial Class TextOutputDTO
        Inherits TextOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class TextOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("string", "", 1)>
        Public Property [ReturnValue1] As String
    
    End Class    
    
    
    
    Public Partial Class NameOutputDTO
        Inherits NameOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class NameOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("string", "", 1)>
        Public Property [ReturnValue1] As String
    
    End Class    
    
    
    
    
    
    
    
    Public Partial Class PubkeyOutputDTO
        Inherits PubkeyOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class PubkeyOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bytes32", "x", 1)>
        Public Property [X] As Byte()
        <[Parameter]("bytes32", "y", 2)>
        Public Property [Y] As Byte()
    
    End Class    
    
    
    
    Public Partial Class MultihashOutputDTO
        Inherits MultihashOutputDTOBase
    End Class

    <[FunctionOutput]>
    Public Class MultihashOutputDTOBase
        Implements IFunctionOutputDTO
        
        <[Parameter]("bytes", "", 1)>
        Public Property [ReturnValue1] As Byte()
    
    End Class
End Namespace
