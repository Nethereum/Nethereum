Imports System
Imports System.Threading.Tasks
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.Contracts.CQS
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports StandardToken.MyContractName.DTOs
Namespace StandardToken.MyContractName.CQS

    <[Function]("allowed", "uint256")>
    Public Class AllowedFunction
        Inherits ContractMessage
    
        <[Parameter]("address", "", 1)>
        Public Property B As String
        <[Parameter]("address", "", 2)>
        Public Property C As String
    
    End Class


End Namespace
