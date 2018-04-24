Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Namespace StandardToken.MyContractName.DTOs

    <[Event]("Approval")>
    Public Class ApprovalEventDTO
    
        <[Parameter]("address", "_owner", 1, true)>
        Public Property Owner As String
        <[Parameter]("address", "_spender", 2, true)>
        Public Property Spender As String
        <[Parameter]("uint256", "_value", 3, false)>
        Public Property Value As BigInteger
    
    End Class

End Namespace
