Imports System
Imports System.Threading.Tasks
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Namespace StandardToken.MyContractName.DTOs

    <[FunctionOutput]>
    Public Class TotalSupplyOutputDTO
    
        <[Parameter]("uint256", "", 1)>
        Public Property B As BigInteger
    
    End Class

End Namespace
