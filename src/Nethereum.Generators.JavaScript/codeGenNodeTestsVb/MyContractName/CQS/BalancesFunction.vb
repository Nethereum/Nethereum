Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
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
        Public Property ReturnValue1 As String
    
    End Class


End Namespace
