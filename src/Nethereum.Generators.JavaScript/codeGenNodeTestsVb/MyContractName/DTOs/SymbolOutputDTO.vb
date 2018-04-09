Imports System
Imports System.Threading.Tasks
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Namespace StandardToken.MyContractName.DTOs

    <[FunctionOutput]>
    Public Class SymbolOutputDTO
    
        <[Parameter]("string", "", 1)>
        Public Property B As String
    
    End Class

End Namespace
