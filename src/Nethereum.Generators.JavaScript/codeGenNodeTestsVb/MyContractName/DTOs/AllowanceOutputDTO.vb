Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Namespace StandardToken.MyContractName.DTOs

    <[FunctionOutput]>
    Public Class AllowanceOutputDTO
    
        <[Parameter]("uint256", "remaining", 1)>
        Public Property Remaining As BigInteger
    
    End Class

End Namespace
