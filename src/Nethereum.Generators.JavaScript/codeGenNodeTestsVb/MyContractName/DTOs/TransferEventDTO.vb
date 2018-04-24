Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Namespace StandardToken.MyContractName.DTOs

    <[Event]("Transfer")>
    Public Class TransferEventDTO
    
        <[Parameter]("address", "_from", 1, true)>
        Public Property From As String
        <[Parameter]("address", "_to", 2, true)>
        Public Property To As String
        <[Parameter]("uint256", "_value", 3, false)>
        Public Property Value As BigInteger
    
    End Class

End Namespace
