Imports System
Imports System.Threading.Tasks
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.Contracts.CQS
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports StandardToken.MyContractName.DTOs
Namespace StandardToken.MyContractName.CQS

    <[Function]("allowance", "uint256")>
    Public Class AllowanceFunction
        Inherits ContractMessage
    
        <[Parameter]("address", "_owner", 1)>
        Public Property Owner As String
        <[Parameter]("address", "_spender", 2)>
        Public Property Spender As String
    
    End Class


End Namespace
