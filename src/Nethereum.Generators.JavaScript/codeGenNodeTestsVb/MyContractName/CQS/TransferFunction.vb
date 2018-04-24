Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.Contracts.CQS
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports StandardToken.MyContractName.DTOs
Namespace StandardToken.MyContractName.CQS

    <[Function]("transfer", "bool")>
    Public Class TransferFunction
        Inherits ContractMessage
    
        <[Parameter]("address", "_to", 1)>
        Public Property To As String
        <[Parameter]("uint256", "_value", 2)>
        Public Property Value As BigInteger
    
    End Class


End Namespace
