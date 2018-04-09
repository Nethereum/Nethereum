Imports System
Imports System.Threading.Tasks
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.Contracts.CQS
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports StandardToken.MyContractName.DTOs
Namespace StandardToken.MyContractName.CQS

    <[Function]("balances", "uint256")>
    Public Class BalancesFunction
        Inherits ContractMessage
    
        <[Parameter]("address", "", 1)>
        Public Property B As String
    
    End Class


End Namespace
