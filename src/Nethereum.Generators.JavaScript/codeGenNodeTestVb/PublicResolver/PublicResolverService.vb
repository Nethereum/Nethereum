Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Numerics
Imports Nethereum.Hex.HexTypes
Imports Nethereum.ABI.FunctionEncoding.Attributes
Imports Nethereum.Web3
Imports Nethereum.RPC.Eth.DTOs
Imports Nethereum.Contracts.CQS
Imports Nethereum.Contracts.ContractHandlers
Imports Nethereum.Contracts
Imports System.Threading
Imports Nethereum.ENS.PublicResolver.ContractDefinition
Namespace Nethereum.ENS.PublicResolver


    Public Partial Class PublicResolverService
    
    
        Public Shared Function DeployContractAndWaitForReceiptAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal publicResolverDeployment As PublicResolverDeployment, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return web3.Eth.GetContractDeploymentHandler(Of PublicResolverDeployment)().SendRequestAndWaitForReceiptAsync(publicResolverDeployment, cancellationTokenSource)
        
        End Function
         Public Shared Function DeployContractAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal publicResolverDeployment As PublicResolverDeployment) As Task(Of String)
        
            Return web3.Eth.GetContractDeploymentHandler(Of PublicResolverDeployment)().SendRequestAsync(publicResolverDeployment)
        
        End Function
        Public Shared Async Function DeployContractAndGetServiceAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal publicResolverDeployment As PublicResolverDeployment, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of PublicResolverService)
        
            Dim receipt = Await DeployContractAndWaitForReceiptAsync(web3, publicResolverDeployment, cancellationTokenSource)
            Return New PublicResolverService(web3, receipt.ContractAddress)
        
        End Function
    
        Protected Property Web3 As Nethereum.Web3.Web3
        
        Public Property ContractHandler As ContractHandler
        
        Public Sub New(ByVal web3 As Nethereum.Web3.Web3, ByVal contractAddress As String)
            Web3 = web3
            ContractHandler = web3.Eth.GetContractHandler(contractAddress)
        End Sub
    
        Public Function SupportsInterfaceQueryAsync(ByVal supportsInterfaceFunction As SupportsInterfaceFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Return ContractHandler.QueryAsync(Of SupportsInterfaceFunction, Boolean)(supportsInterfaceFunction, blockParameter)
        
        End Function

        
        Public Function SupportsInterfaceQueryAsync(ByVal interfaceID As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Dim supportsInterfaceFunction = New SupportsInterfaceFunction()
                supportsInterfaceFunction.InterfaceID = interfaceID
            
            Return ContractHandler.QueryAsync(Of SupportsInterfaceFunction, Boolean)(supportsInterfaceFunction, blockParameter)
        
        End Function


        Public Function SetTextRequestAsync(ByVal setTextFunction As SetTextFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetTextFunction)(setTextFunction)
        
        End Function

        Public Function SetTextRequestAndWaitForReceiptAsync(ByVal setTextFunction As SetTextFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetTextFunction)(setTextFunction, cancellationToken)
        
        End Function

        
        Public Function SetTextRequestAsync(ByVal node As Byte(), ByVal key As String, ByVal value As String) As Task(Of String)
        
            Dim setTextFunction = New SetTextFunction()
                setTextFunction.Node = node
                setTextFunction.Key = key
                setTextFunction.Value = value
            
            Return ContractHandler.SendRequestAsync(Of SetTextFunction)(setTextFunction)
        
        End Function

        
        Public Function SetTextRequestAndWaitForReceiptAsync(ByVal node As Byte(), ByVal key As String, ByVal value As String, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setTextFunction = New SetTextFunction()
                setTextFunction.Node = node
                setTextFunction.Key = key
                setTextFunction.Value = value
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetTextFunction)(setTextFunction, cancellationToken)
        
        End Function
        Public Function ABIQueryAsync(ByVal aBIFunction As ABIFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ABIOutputDTO)
        
            Return ContractHandler.QueryDeserializingToObjectAsync(Of ABIFunction, ABIOutputDTO)(aBIFunction, blockParameter)
        
        End Function

        
        Public Function ABIQueryAsync(ByVal node As Byte(), ByVal contentTypes As BigInteger, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ABIOutputDTO)
        
            Dim aBIFunction = New ABIFunction()
                aBIFunction.Node = node
                aBIFunction.ContentTypes = contentTypes
            
            Return ContractHandler.QueryDeserializingToObjectAsync(Of ABIFunction, ABIOutputDTO)(aBIFunction, blockParameter)
        
        End Function


        Public Function SetPubkeyRequestAsync(ByVal setPubkeyFunction As SetPubkeyFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetPubkeyFunction)(setPubkeyFunction)
        
        End Function

        Public Function SetPubkeyRequestAndWaitForReceiptAsync(ByVal setPubkeyFunction As SetPubkeyFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetPubkeyFunction)(setPubkeyFunction, cancellationToken)
        
        End Function

        
        Public Function SetPubkeyRequestAsync(ByVal node As Byte(), ByVal x As Byte(), ByVal y As Byte()) As Task(Of String)
        
            Dim setPubkeyFunction = New SetPubkeyFunction()
                setPubkeyFunction.Node = node
                setPubkeyFunction.X = x
                setPubkeyFunction.Y = y
            
            Return ContractHandler.SendRequestAsync(Of SetPubkeyFunction)(setPubkeyFunction)
        
        End Function

        
        Public Function SetPubkeyRequestAndWaitForReceiptAsync(ByVal node As Byte(), ByVal x As Byte(), ByVal y As Byte(), ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setPubkeyFunction = New SetPubkeyFunction()
                setPubkeyFunction.Node = node
                setPubkeyFunction.X = x
                setPubkeyFunction.Y = y
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetPubkeyFunction)(setPubkeyFunction, cancellationToken)
        
        End Function
        Public Function ContentQueryAsync(ByVal contentFunction As ContentFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Return ContractHandler.QueryAsync(Of ContentFunction, Byte())(contentFunction, blockParameter)
        
        End Function

        
        Public Function ContentQueryAsync(ByVal node As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Dim contentFunction = New ContentFunction()
                contentFunction.Node = node
            
            Return ContractHandler.QueryAsync(Of ContentFunction, Byte())(contentFunction, blockParameter)
        
        End Function


        Public Function AddrQueryAsync(ByVal addrFunction As AddrFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of AddrFunction, String)(addrFunction, blockParameter)
        
        End Function

        
        Public Function AddrQueryAsync(ByVal node As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Dim addrFunction = New AddrFunction()
                addrFunction.Node = node
            
            Return ContractHandler.QueryAsync(Of AddrFunction, String)(addrFunction, blockParameter)
        
        End Function


        Public Function TextQueryAsync(ByVal textFunction As TextFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of TextFunction, String)(textFunction, blockParameter)
        
        End Function

        
        Public Function TextQueryAsync(ByVal node As Byte(), ByVal key As String, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Dim textFunction = New TextFunction()
                textFunction.Node = node
                textFunction.Key = key
            
            Return ContractHandler.QueryAsync(Of TextFunction, String)(textFunction, blockParameter)
        
        End Function


        Public Function SetABIRequestAsync(ByVal setABIFunction As SetABIFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetABIFunction)(setABIFunction)
        
        End Function

        Public Function SetABIRequestAndWaitForReceiptAsync(ByVal setABIFunction As SetABIFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetABIFunction)(setABIFunction, cancellationToken)
        
        End Function

        
        Public Function SetABIRequestAsync(ByVal node As Byte(), ByVal contentType As BigInteger, ByVal data As Byte()) As Task(Of String)
        
            Dim setABIFunction = New SetABIFunction()
                setABIFunction.Node = node
                setABIFunction.ContentType = contentType
                setABIFunction.Data = data
            
            Return ContractHandler.SendRequestAsync(Of SetABIFunction)(setABIFunction)
        
        End Function

        
        Public Function SetABIRequestAndWaitForReceiptAsync(ByVal node As Byte(), ByVal contentType As BigInteger, ByVal data As Byte(), ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setABIFunction = New SetABIFunction()
                setABIFunction.Node = node
                setABIFunction.ContentType = contentType
                setABIFunction.Data = data
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetABIFunction)(setABIFunction, cancellationToken)
        
        End Function
        Public Function NameQueryAsync(ByVal nameFunction As NameFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of NameFunction, String)(nameFunction, blockParameter)
        
        End Function

        
        Public Function NameQueryAsync(ByVal node As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Dim nameFunction = New NameFunction()
                nameFunction.Node = node
            
            Return ContractHandler.QueryAsync(Of NameFunction, String)(nameFunction, blockParameter)
        
        End Function


        Public Function SetNameRequestAsync(ByVal setNameFunction As SetNameFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetNameFunction)(setNameFunction)
        
        End Function

        Public Function SetNameRequestAndWaitForReceiptAsync(ByVal setNameFunction As SetNameFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetNameFunction)(setNameFunction, cancellationToken)
        
        End Function

        
        Public Function SetNameRequestAsync(ByVal node As Byte(), ByVal name As String) As Task(Of String)
        
            Dim setNameFunction = New SetNameFunction()
                setNameFunction.Node = node
                setNameFunction.Name = name
            
            Return ContractHandler.SendRequestAsync(Of SetNameFunction)(setNameFunction)
        
        End Function

        
        Public Function SetNameRequestAndWaitForReceiptAsync(ByVal node As Byte(), ByVal name As String, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setNameFunction = New SetNameFunction()
                setNameFunction.Node = node
                setNameFunction.Name = name
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetNameFunction)(setNameFunction, cancellationToken)
        
        End Function
        Public Function SetMultihashRequestAsync(ByVal setMultihashFunction As SetMultihashFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetMultihashFunction)(setMultihashFunction)
        
        End Function

        Public Function SetMultihashRequestAndWaitForReceiptAsync(ByVal setMultihashFunction As SetMultihashFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetMultihashFunction)(setMultihashFunction, cancellationToken)
        
        End Function

        
        Public Function SetMultihashRequestAsync(ByVal node As Byte(), ByVal hash As Byte()) As Task(Of String)
        
            Dim setMultihashFunction = New SetMultihashFunction()
                setMultihashFunction.Node = node
                setMultihashFunction.Hash = hash
            
            Return ContractHandler.SendRequestAsync(Of SetMultihashFunction)(setMultihashFunction)
        
        End Function

        
        Public Function SetMultihashRequestAndWaitForReceiptAsync(ByVal node As Byte(), ByVal hash As Byte(), ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setMultihashFunction = New SetMultihashFunction()
                setMultihashFunction.Node = node
                setMultihashFunction.Hash = hash
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetMultihashFunction)(setMultihashFunction, cancellationToken)
        
        End Function
        Public Function SetContentRequestAsync(ByVal setContentFunction As SetContentFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetContentFunction)(setContentFunction)
        
        End Function

        Public Function SetContentRequestAndWaitForReceiptAsync(ByVal setContentFunction As SetContentFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetContentFunction)(setContentFunction, cancellationToken)
        
        End Function

        
        Public Function SetContentRequestAsync(ByVal node As Byte(), ByVal hash As Byte()) As Task(Of String)
        
            Dim setContentFunction = New SetContentFunction()
                setContentFunction.Node = node
                setContentFunction.Hash = hash
            
            Return ContractHandler.SendRequestAsync(Of SetContentFunction)(setContentFunction)
        
        End Function

        
        Public Function SetContentRequestAndWaitForReceiptAsync(ByVal node As Byte(), ByVal hash As Byte(), ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setContentFunction = New SetContentFunction()
                setContentFunction.Node = node
                setContentFunction.Hash = hash
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetContentFunction)(setContentFunction, cancellationToken)
        
        End Function
        Public Function PubkeyQueryAsync(ByVal pubkeyFunction As PubkeyFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of PubkeyOutputDTO)
        
            Return ContractHandler.QueryDeserializingToObjectAsync(Of PubkeyFunction, PubkeyOutputDTO)(pubkeyFunction, blockParameter)
        
        End Function

        
        Public Function PubkeyQueryAsync(ByVal node As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of PubkeyOutputDTO)
        
            Dim pubkeyFunction = New PubkeyFunction()
                pubkeyFunction.Node = node
            
            Return ContractHandler.QueryDeserializingToObjectAsync(Of PubkeyFunction, PubkeyOutputDTO)(pubkeyFunction, blockParameter)
        
        End Function


        Public Function SetAddrRequestAsync(ByVal setAddrFunction As SetAddrFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetAddrFunction)(setAddrFunction)
        
        End Function

        Public Function SetAddrRequestAndWaitForReceiptAsync(ByVal setAddrFunction As SetAddrFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetAddrFunction)(setAddrFunction, cancellationToken)
        
        End Function

        
        Public Function SetAddrRequestAsync(ByVal node As Byte(), ByVal addr As String) As Task(Of String)
        
            Dim setAddrFunction = New SetAddrFunction()
                setAddrFunction.Node = node
                setAddrFunction.Addr = addr
            
            Return ContractHandler.SendRequestAsync(Of SetAddrFunction)(setAddrFunction)
        
        End Function

        
        Public Function SetAddrRequestAndWaitForReceiptAsync(ByVal node As Byte(), ByVal addr As String, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setAddrFunction = New SetAddrFunction()
                setAddrFunction.Node = node
                setAddrFunction.Addr = addr
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetAddrFunction)(setAddrFunction, cancellationToken)
        
        End Function
        Public Function MultihashQueryAsync(ByVal multihashFunction As MultihashFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Return ContractHandler.QueryAsync(Of MultihashFunction, Byte())(multihashFunction, blockParameter)
        
        End Function

        
        Public Function MultihashQueryAsync(ByVal node As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Dim multihashFunction = New MultihashFunction()
                multihashFunction.Node = node
            
            Return ContractHandler.QueryAsync(Of MultihashFunction, Byte())(multihashFunction, blockParameter)
        
        End Function


    
    End Class

End Namespace
