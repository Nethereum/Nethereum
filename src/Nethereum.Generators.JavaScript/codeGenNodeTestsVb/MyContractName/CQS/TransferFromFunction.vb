Imports System
Imports System.Threading.Tasks
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.Contracts.CQS
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports StandardToken.MyContractName.DTOs
Namespace StandardToken.MyContractName.CQS

    <[Function]("transferFrom", "bool")>
    Public Class TransferFromFunction
        Inherits ContractMessage
    
        <[Parameter]("address", "_from", 1)>
        Public Property From As String
        <[Parameter]("address", "_to", 2)>
        Public Property To As String
        <[Parameter]("uint256", "_value", 3)>
        Public Property Value As BigInteger
    
    End Class


End Namespace
