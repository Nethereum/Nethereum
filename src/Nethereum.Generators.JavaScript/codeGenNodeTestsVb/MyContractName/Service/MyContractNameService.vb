Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports Nethereum.Web3
Imports Nethereum.RPC.Eth.DTOs
Imports Nethereum.Contracts.CQS
Imports System.Threading
Imports StandardToken.MyContractName.CQS
Imports StandardToken.MyContractName.DTOs
Namespace StandardToken.MyContractName.Service


    Public Class MyContractNameService
    
    
        Public Shared Function DeployContractAndWaitForReceiptAsync(ByVal web3 As Web3, ByVal myContractNameDeployment As MyContractNameDeployment, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return web3.Eth.GetContractDeploymentHandler(Of MyContractNameDeployment)().SendRequestAndWaitForReceiptAsync(myContractNameDeployment, cancellationTokenSource)
        
        End Function
         Public Shared Function DeployContractAsync(ByVal web3 As Web3, ByVal myContractNameDeployment As MyContractNameDeployment) As Task(Of String)
        
            Return web3.Eth.GetContractDeploymentHandler(Of MyContractNameDeployment)().SendRequestAsync(myContractNameDeployment)
        
        End Function
        Public Shared Async Function DeployContractAndGetServiceAsync(ByVal web3 As Web3, ByVal myContractNameDeployment As MyContractNameDeployment, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of MyContractNameService)
        
            Dim receipt = Await DeployContractAndWaitForReceiptAsync(web3, myContractNameDeployment, cancellationTokenSource)
            Return New MyContractNameService(web3, receipt.ContractAddress)
        
        End Function
    
        Protected Property Web3 As Web3
        
        Protected Property ContractHandler As ContractHandler
        
        Public Sub New(ByVal web3 As Web3, ByVal contractAddress As String)
            Web3 = web3
            ContractHandler = web3.Eth.GetContractHandler(contractAddress)
        End Sub
    
        Public Function NameQueryAsync(ByVal nameFunction As NameFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of NameFunction, String)(nameFunction, blockParameter)
        
        End Function
        Public Function ApproveRequestAsync(ByVal approveFunction As ApproveFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(approveFunction)
        
        End Function
        Public Function ApproveRequestAndWaitForReceiptAsync(ByVal approveFunction As ApproveFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken)
        
        End Function
        Public Function TotalSupplyQueryAsync(ByVal totalSupplyFunction As TotalSupplyFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of TotalSupplyFunction, BigInteger)(totalSupplyFunction, blockParameter)
        
        End Function
        Public Function TransferFromRequestAsync(ByVal transferFromFunction As TransferFromFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(transferFromFunction)
        
        End Function
        Public Function TransferFromRequestAndWaitForReceiptAsync(ByVal transferFromFunction As TransferFromFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken)
        
        End Function
        Public Function BalancesQueryAsync(ByVal balancesFunction As BalancesFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of BalancesFunction, BigInteger)(balancesFunction, blockParameter)
        
        End Function
        Public Function DecimalsQueryAsync(ByVal decimalsFunction As DecimalsFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte)
        
            Return ContractHandler.QueryAsync(Of DecimalsFunction, Byte)(decimalsFunction, blockParameter)
        
        End Function
        Public Function AllowedQueryAsync(ByVal allowedFunction As AllowedFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of AllowedFunction, BigInteger)(allowedFunction, blockParameter)
        
        End Function
        Public Function BalanceOfQueryAsync(ByVal balanceOfFunction As BalanceOfFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of BalanceOfFunction, BigInteger)(balanceOfFunction, blockParameter)
        
        End Function
        Public Function SymbolQueryAsync(ByVal symbolFunction As SymbolFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of SymbolFunction, String)(symbolFunction, blockParameter)
        
        End Function
        Public Function TransferRequestAsync(ByVal transferFunction As TransferFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(transferFunction)
        
        End Function
        Public Function TransferRequestAndWaitForReceiptAsync(ByVal transferFunction As TransferFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken)
        
        End Function
        Public Function AllowanceQueryAsync(ByVal allowanceFunction As AllowanceFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of AllowanceFunction, BigInteger)(allowanceFunction, blockParameter)
        
        End Function
    
    End Class

End Namespace
