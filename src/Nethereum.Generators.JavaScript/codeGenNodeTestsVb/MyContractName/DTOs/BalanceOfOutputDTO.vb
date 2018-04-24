Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Namespace StandardToken.MyContractName.DTOs

    <[FunctionOutput]>
    Public Class BalanceOfOutputDTO
    
        <[Parameter]("uint256", "balance", 1)>
        Public Property Balance As BigInteger
    
    End Class

End Namespace
