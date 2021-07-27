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
Imports Nethereum.ERC721.ERC721PresetMinterPauserAutoId.ContractDefinition
Namespace Nethereum.ERC721.ERC721PresetMinterPauserAutoId


    Public Partial Class ERC721PresetMinterPauserAutoIdService
    
    
        Public Shared Function DeployContractAndWaitForReceiptAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal eRC721PresetMinterPauserAutoIdDeployment As ERC721PresetMinterPauserAutoIdDeployment, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return web3.Eth.GetContractDeploymentHandler(Of ERC721PresetMinterPauserAutoIdDeployment)().SendRequestAndWaitForReceiptAsync(eRC721PresetMinterPauserAutoIdDeployment, cancellationTokenSource)
        
        End Function
         Public Shared Function DeployContractAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal eRC721PresetMinterPauserAutoIdDeployment As ERC721PresetMinterPauserAutoIdDeployment) As Task(Of String)
        
            Return web3.Eth.GetContractDeploymentHandler(Of ERC721PresetMinterPauserAutoIdDeployment)().SendRequestAsync(eRC721PresetMinterPauserAutoIdDeployment)
        
        End Function
        Public Shared Async Function DeployContractAndGetServiceAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal eRC721PresetMinterPauserAutoIdDeployment As ERC721PresetMinterPauserAutoIdDeployment, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of ERC721PresetMinterPauserAutoIdService)
        
            Dim receipt = Await DeployContractAndWaitForReceiptAsync(web3, eRC721PresetMinterPauserAutoIdDeployment, cancellationTokenSource)
            Return New ERC721PresetMinterPauserAutoIdService(web3, receipt.ContractAddress)
        
        End Function
    
        Protected Property Web3 As Nethereum.Web3.Web3
        
        Public Property ContractHandler As ContractHandler
        
        Public Sub New(ByVal web3 As Nethereum.Web3.Web3, ByVal contractAddress As String)
            Web3 = web3
            ContractHandler = web3.Eth.GetContractHandler(contractAddress)
        End Sub
    
        Public Function DEFAULT_ADMIN_ROLEQueryAsync(ByVal dEFAULT_ADMIN_ROLEFunction As DEFAULT_ADMIN_ROLEFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Return ContractHandler.QueryAsync(Of DEFAULT_ADMIN_ROLEFunction, Byte())(dEFAULT_ADMIN_ROLEFunction, blockParameter)
        
        End Function

        
        Public Function DEFAULT_ADMIN_ROLEQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            return ContractHandler.QueryAsync(Of DEFAULT_ADMIN_ROLEFunction, Byte())(Nothing, blockParameter)
        
        End Function



        Public Function MINTER_ROLEQueryAsync(ByVal mINTER_ROLEFunction As MINTER_ROLEFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Return ContractHandler.QueryAsync(Of MINTER_ROLEFunction, Byte())(mINTER_ROLEFunction, blockParameter)
        
        End Function

        
        Public Function MINTER_ROLEQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            return ContractHandler.QueryAsync(Of MINTER_ROLEFunction, Byte())(Nothing, blockParameter)
        
        End Function



        Public Function PAUSER_ROLEQueryAsync(ByVal pAUSER_ROLEFunction As PAUSER_ROLEFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Return ContractHandler.QueryAsync(Of PAUSER_ROLEFunction, Byte())(pAUSER_ROLEFunction, blockParameter)
        
        End Function

        
        Public Function PAUSER_ROLEQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            return ContractHandler.QueryAsync(Of PAUSER_ROLEFunction, Byte())(Nothing, blockParameter)
        
        End Function



        Public Function ApproveRequestAsync(ByVal approveFunction As ApproveFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of ApproveFunction)(approveFunction)
        
        End Function

        Public Function ApproveRequestAndWaitForReceiptAsync(ByVal approveFunction As ApproveFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of ApproveFunction)(approveFunction, cancellationToken)
        
        End Function

        
        Public Function ApproveRequestAsync(ByVal [to] As String, ByVal [tokenId] As BigInteger) As Task(Of String)
        
            Dim approveFunction = New ApproveFunction()
                approveFunction.To = [to]
                approveFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAsync(Of ApproveFunction)(approveFunction)
        
        End Function

        
        Public Function ApproveRequestAndWaitForReceiptAsync(ByVal [to] As String, ByVal [tokenId] As BigInteger, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim approveFunction = New ApproveFunction()
                approveFunction.To = [to]
                approveFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of ApproveFunction)(approveFunction, cancellationToken)
        
        End Function
        Public Function BalanceOfQueryAsync(ByVal balanceOfFunction As BalanceOfFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of BalanceOfFunction, BigInteger)(balanceOfFunction, blockParameter)
        
        End Function

        
        Public Function BalanceOfQueryAsync(ByVal [owner] As String, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Dim balanceOfFunction = New BalanceOfFunction()
                balanceOfFunction.Owner = [owner]
            
            Return ContractHandler.QueryAsync(Of BalanceOfFunction, BigInteger)(balanceOfFunction, blockParameter)
        
        End Function


        Public Function BaseURIQueryAsync(ByVal baseURIFunction As BaseURIFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of BaseURIFunction, String)(baseURIFunction, blockParameter)
        
        End Function

        
        Public Function BaseURIQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            return ContractHandler.QueryAsync(Of BaseURIFunction, String)(Nothing, blockParameter)
        
        End Function



        Public Function BurnRequestAsync(ByVal burnFunction As BurnFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of BurnFunction)(burnFunction)
        
        End Function

        Public Function BurnRequestAndWaitForReceiptAsync(ByVal burnFunction As BurnFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of BurnFunction)(burnFunction, cancellationToken)
        
        End Function

        
        Public Function BurnRequestAsync(ByVal [tokenId] As BigInteger) As Task(Of String)
        
            Dim burnFunction = New BurnFunction()
                burnFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAsync(Of BurnFunction)(burnFunction)
        
        End Function

        
        Public Function BurnRequestAndWaitForReceiptAsync(ByVal [tokenId] As BigInteger, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim burnFunction = New BurnFunction()
                burnFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of BurnFunction)(burnFunction, cancellationToken)
        
        End Function
        Public Function GetApprovedQueryAsync(ByVal getApprovedFunction As GetApprovedFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of GetApprovedFunction, String)(getApprovedFunction, blockParameter)
        
        End Function

        
        Public Function GetApprovedQueryAsync(ByVal [tokenId] As BigInteger, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Dim getApprovedFunction = New GetApprovedFunction()
                getApprovedFunction.TokenId = [tokenId]
            
            Return ContractHandler.QueryAsync(Of GetApprovedFunction, String)(getApprovedFunction, blockParameter)
        
        End Function


        Public Function GetRoleAdminQueryAsync(ByVal getRoleAdminFunction As GetRoleAdminFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Return ContractHandler.QueryAsync(Of GetRoleAdminFunction, Byte())(getRoleAdminFunction, blockParameter)
        
        End Function

        
        Public Function GetRoleAdminQueryAsync(ByVal [role] As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Byte())
        
            Dim getRoleAdminFunction = New GetRoleAdminFunction()
                getRoleAdminFunction.Role = [role]
            
            Return ContractHandler.QueryAsync(Of GetRoleAdminFunction, Byte())(getRoleAdminFunction, blockParameter)
        
        End Function


        Public Function GetRoleMemberQueryAsync(ByVal getRoleMemberFunction As GetRoleMemberFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of GetRoleMemberFunction, String)(getRoleMemberFunction, blockParameter)
        
        End Function

        
        Public Function GetRoleMemberQueryAsync(ByVal [role] As Byte(), ByVal [index] As BigInteger, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Dim getRoleMemberFunction = New GetRoleMemberFunction()
                getRoleMemberFunction.Role = [role]
                getRoleMemberFunction.Index = [index]
            
            Return ContractHandler.QueryAsync(Of GetRoleMemberFunction, String)(getRoleMemberFunction, blockParameter)
        
        End Function


        Public Function GetRoleMemberCountQueryAsync(ByVal getRoleMemberCountFunction As GetRoleMemberCountFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of GetRoleMemberCountFunction, BigInteger)(getRoleMemberCountFunction, blockParameter)
        
        End Function

        
        Public Function GetRoleMemberCountQueryAsync(ByVal [role] As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Dim getRoleMemberCountFunction = New GetRoleMemberCountFunction()
                getRoleMemberCountFunction.Role = [role]
            
            Return ContractHandler.QueryAsync(Of GetRoleMemberCountFunction, BigInteger)(getRoleMemberCountFunction, blockParameter)
        
        End Function


        Public Function GrantRoleRequestAsync(ByVal grantRoleFunction As GrantRoleFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of GrantRoleFunction)(grantRoleFunction)
        
        End Function

        Public Function GrantRoleRequestAndWaitForReceiptAsync(ByVal grantRoleFunction As GrantRoleFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of GrantRoleFunction)(grantRoleFunction, cancellationToken)
        
        End Function

        
        Public Function GrantRoleRequestAsync(ByVal [role] As Byte(), ByVal [account] As String) As Task(Of String)
        
            Dim grantRoleFunction = New GrantRoleFunction()
                grantRoleFunction.Role = [role]
                grantRoleFunction.Account = [account]
            
            Return ContractHandler.SendRequestAsync(Of GrantRoleFunction)(grantRoleFunction)
        
        End Function

        
        Public Function GrantRoleRequestAndWaitForReceiptAsync(ByVal [role] As Byte(), ByVal [account] As String, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim grantRoleFunction = New GrantRoleFunction()
                grantRoleFunction.Role = [role]
                grantRoleFunction.Account = [account]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of GrantRoleFunction)(grantRoleFunction, cancellationToken)
        
        End Function
        Public Function HasRoleQueryAsync(ByVal hasRoleFunction As HasRoleFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Return ContractHandler.QueryAsync(Of HasRoleFunction, Boolean)(hasRoleFunction, blockParameter)
        
        End Function

        
        Public Function HasRoleQueryAsync(ByVal [role] As Byte(), ByVal [account] As String, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Dim hasRoleFunction = New HasRoleFunction()
                hasRoleFunction.Role = [role]
                hasRoleFunction.Account = [account]
            
            Return ContractHandler.QueryAsync(Of HasRoleFunction, Boolean)(hasRoleFunction, blockParameter)
        
        End Function


        Public Function IsApprovedForAllQueryAsync(ByVal isApprovedForAllFunction As IsApprovedForAllFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Return ContractHandler.QueryAsync(Of IsApprovedForAllFunction, Boolean)(isApprovedForAllFunction, blockParameter)
        
        End Function

        
        Public Function IsApprovedForAllQueryAsync(ByVal [owner] As String, ByVal [operator] As String, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Dim isApprovedForAllFunction = New IsApprovedForAllFunction()
                isApprovedForAllFunction.Owner = [owner]
                isApprovedForAllFunction.Operator = [operator]
            
            Return ContractHandler.QueryAsync(Of IsApprovedForAllFunction, Boolean)(isApprovedForAllFunction, blockParameter)
        
        End Function


        Public Function MintRequestAsync(ByVal mintFunction As MintFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of MintFunction)(mintFunction)
        
        End Function

        Public Function MintRequestAndWaitForReceiptAsync(ByVal mintFunction As MintFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of MintFunction)(mintFunction, cancellationToken)
        
        End Function

        
        Public Function MintRequestAsync(ByVal [to] As String) As Task(Of String)
        
            Dim mintFunction = New MintFunction()
                mintFunction.To = [to]
            
            Return ContractHandler.SendRequestAsync(Of MintFunction)(mintFunction)
        
        End Function

        
        Public Function MintRequestAndWaitForReceiptAsync(ByVal [to] As String, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim mintFunction = New MintFunction()
                mintFunction.To = [to]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of MintFunction)(mintFunction, cancellationToken)
        
        End Function
        Public Function NameQueryAsync(ByVal nameFunction As NameFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of NameFunction, String)(nameFunction, blockParameter)
        
        End Function

        
        Public Function NameQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            return ContractHandler.QueryAsync(Of NameFunction, String)(Nothing, blockParameter)
        
        End Function



        Public Function OwnerOfQueryAsync(ByVal ownerOfFunction As OwnerOfFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of OwnerOfFunction, String)(ownerOfFunction, blockParameter)
        
        End Function

        
        Public Function OwnerOfQueryAsync(ByVal [tokenId] As BigInteger, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Dim ownerOfFunction = New OwnerOfFunction()
                ownerOfFunction.TokenId = [tokenId]
            
            Return ContractHandler.QueryAsync(Of OwnerOfFunction, String)(ownerOfFunction, blockParameter)
        
        End Function


        Public Function PauseRequestAsync(ByVal pauseFunction As PauseFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of PauseFunction)(pauseFunction)
        
        End Function

        Public Function PauseRequestAsync() As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of PauseFunction)
        
        End Function

        Public Function PauseRequestAndWaitForReceiptAsync(ByVal pauseFunction As PauseFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of PauseFunction)(pauseFunction, cancellationToken)
        
        End Function

        Public Function PauseRequestAndWaitForReceiptAsync(ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of PauseFunction)(Nothing, cancellationToken)
        
        End Function
        Public Function PausedQueryAsync(ByVal pausedFunction As PausedFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Return ContractHandler.QueryAsync(Of PausedFunction, Boolean)(pausedFunction, blockParameter)
        
        End Function

        
        Public Function PausedQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            return ContractHandler.QueryAsync(Of PausedFunction, Boolean)(Nothing, blockParameter)
        
        End Function



        Public Function RenounceRoleRequestAsync(ByVal renounceRoleFunction As RenounceRoleFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of RenounceRoleFunction)(renounceRoleFunction)
        
        End Function

        Public Function RenounceRoleRequestAndWaitForReceiptAsync(ByVal renounceRoleFunction As RenounceRoleFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of RenounceRoleFunction)(renounceRoleFunction, cancellationToken)
        
        End Function

        
        Public Function RenounceRoleRequestAsync(ByVal [role] As Byte(), ByVal [account] As String) As Task(Of String)
        
            Dim renounceRoleFunction = New RenounceRoleFunction()
                renounceRoleFunction.Role = [role]
                renounceRoleFunction.Account = [account]
            
            Return ContractHandler.SendRequestAsync(Of RenounceRoleFunction)(renounceRoleFunction)
        
        End Function

        
        Public Function RenounceRoleRequestAndWaitForReceiptAsync(ByVal [role] As Byte(), ByVal [account] As String, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim renounceRoleFunction = New RenounceRoleFunction()
                renounceRoleFunction.Role = [role]
                renounceRoleFunction.Account = [account]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of RenounceRoleFunction)(renounceRoleFunction, cancellationToken)
        
        End Function
        Public Function RevokeRoleRequestAsync(ByVal revokeRoleFunction As RevokeRoleFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of RevokeRoleFunction)(revokeRoleFunction)
        
        End Function

        Public Function RevokeRoleRequestAndWaitForReceiptAsync(ByVal revokeRoleFunction As RevokeRoleFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of RevokeRoleFunction)(revokeRoleFunction, cancellationToken)
        
        End Function

        
        Public Function RevokeRoleRequestAsync(ByVal [role] As Byte(), ByVal [account] As String) As Task(Of String)
        
            Dim revokeRoleFunction = New RevokeRoleFunction()
                revokeRoleFunction.Role = [role]
                revokeRoleFunction.Account = [account]
            
            Return ContractHandler.SendRequestAsync(Of RevokeRoleFunction)(revokeRoleFunction)
        
        End Function

        
        Public Function RevokeRoleRequestAndWaitForReceiptAsync(ByVal [role] As Byte(), ByVal [account] As String, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim revokeRoleFunction = New RevokeRoleFunction()
                revokeRoleFunction.Role = [role]
                revokeRoleFunction.Account = [account]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of RevokeRoleFunction)(revokeRoleFunction, cancellationToken)
        
        End Function
        Public Function SafeTransferFromRequestAsync(ByVal safeTransferFromFunction As SafeTransferFromFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SafeTransferFromFunction)(safeTransferFromFunction)
        
        End Function

        Public Function SafeTransferFromRequestAndWaitForReceiptAsync(ByVal safeTransferFromFunction As SafeTransferFromFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SafeTransferFromFunction)(safeTransferFromFunction, cancellationToken)
        
        End Function

        
        Public Function SafeTransferFromRequestAsync(ByVal [from] As String, ByVal [to] As String, ByVal [tokenId] As BigInteger) As Task(Of String)
        
            Dim safeTransferFromFunction = New SafeTransferFromFunction()
                safeTransferFromFunction.From = [from]
                safeTransferFromFunction.To = [to]
                safeTransferFromFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAsync(Of SafeTransferFromFunction)(safeTransferFromFunction)
        
        End Function

        
        Public Function SafeTransferFromRequestAndWaitForReceiptAsync(ByVal [from] As String, ByVal [to] As String, ByVal [tokenId] As BigInteger, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim safeTransferFromFunction = New SafeTransferFromFunction()
                safeTransferFromFunction.From = [from]
                safeTransferFromFunction.To = [to]
                safeTransferFromFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SafeTransferFromFunction)(safeTransferFromFunction, cancellationToken)
        
        End Function
        Public Function SafeTransferFromRequestAsync(ByVal safeTransferFrom1Function As SafeTransferFrom1Function) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SafeTransferFrom1Function)(safeTransferFrom1Function)
        
        End Function

        Public Function SafeTransferFromRequestAndWaitForReceiptAsync(ByVal safeTransferFrom1Function As SafeTransferFrom1Function, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SafeTransferFrom1Function)(safeTransferFrom1Function, cancellationToken)
        
        End Function

        
        Public Function SafeTransferFromRequestAsync(ByVal [from] As String, ByVal [to] As String, ByVal [tokenId] As BigInteger, ByVal [data] As Byte()) As Task(Of String)
        
            Dim safeTransferFrom1Function = New SafeTransferFrom1Function()
                safeTransferFrom1Function.From = [from]
                safeTransferFrom1Function.To = [to]
                safeTransferFrom1Function.TokenId = [tokenId]
                safeTransferFrom1Function.Data = [data]
            
            Return ContractHandler.SendRequestAsync(Of SafeTransferFrom1Function)(safeTransferFrom1Function)
        
        End Function

        
        Public Function SafeTransferFromRequestAndWaitForReceiptAsync(ByVal [from] As String, ByVal [to] As String, ByVal [tokenId] As BigInteger, ByVal [data] As Byte(), ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim safeTransferFrom1Function = New SafeTransferFrom1Function()
                safeTransferFrom1Function.From = [from]
                safeTransferFrom1Function.To = [to]
                safeTransferFrom1Function.TokenId = [tokenId]
                safeTransferFrom1Function.Data = [data]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SafeTransferFrom1Function)(safeTransferFrom1Function, cancellationToken)
        
        End Function
        Public Function SetApprovalForAllRequestAsync(ByVal setApprovalForAllFunction As SetApprovalForAllFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of SetApprovalForAllFunction)(setApprovalForAllFunction)
        
        End Function

        Public Function SetApprovalForAllRequestAndWaitForReceiptAsync(ByVal setApprovalForAllFunction As SetApprovalForAllFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetApprovalForAllFunction)(setApprovalForAllFunction, cancellationToken)
        
        End Function

        
        Public Function SetApprovalForAllRequestAsync(ByVal [operator] As String, ByVal [approved] As Boolean) As Task(Of String)
        
            Dim setApprovalForAllFunction = New SetApprovalForAllFunction()
                setApprovalForAllFunction.Operator = [operator]
                setApprovalForAllFunction.Approved = [approved]
            
            Return ContractHandler.SendRequestAsync(Of SetApprovalForAllFunction)(setApprovalForAllFunction)
        
        End Function

        
        Public Function SetApprovalForAllRequestAndWaitForReceiptAsync(ByVal [operator] As String, ByVal [approved] As Boolean, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim setApprovalForAllFunction = New SetApprovalForAllFunction()
                setApprovalForAllFunction.Operator = [operator]
                setApprovalForAllFunction.Approved = [approved]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of SetApprovalForAllFunction)(setApprovalForAllFunction, cancellationToken)
        
        End Function
        Public Function SupportsInterfaceQueryAsync(ByVal supportsInterfaceFunction As SupportsInterfaceFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Return ContractHandler.QueryAsync(Of SupportsInterfaceFunction, Boolean)(supportsInterfaceFunction, blockParameter)
        
        End Function

        
        Public Function SupportsInterfaceQueryAsync(ByVal [interfaceId] As Byte(), ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of Boolean)
        
            Dim supportsInterfaceFunction = New SupportsInterfaceFunction()
                supportsInterfaceFunction.InterfaceId = [interfaceId]
            
            Return ContractHandler.QueryAsync(Of SupportsInterfaceFunction, Boolean)(supportsInterfaceFunction, blockParameter)
        
        End Function


        Public Function SymbolQueryAsync(ByVal symbolFunction As SymbolFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of SymbolFunction, String)(symbolFunction, blockParameter)
        
        End Function

        
        Public Function SymbolQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            return ContractHandler.QueryAsync(Of SymbolFunction, String)(Nothing, blockParameter)
        
        End Function



        Public Function TokenByIndexQueryAsync(ByVal tokenByIndexFunction As TokenByIndexFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of TokenByIndexFunction, BigInteger)(tokenByIndexFunction, blockParameter)
        
        End Function

        
        Public Function TokenByIndexQueryAsync(ByVal [index] As BigInteger, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Dim tokenByIndexFunction = New TokenByIndexFunction()
                tokenByIndexFunction.Index = [index]
            
            Return ContractHandler.QueryAsync(Of TokenByIndexFunction, BigInteger)(tokenByIndexFunction, blockParameter)
        
        End Function


        Public Function TokenOfOwnerByIndexQueryAsync(ByVal tokenOfOwnerByIndexFunction As TokenOfOwnerByIndexFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of TokenOfOwnerByIndexFunction, BigInteger)(tokenOfOwnerByIndexFunction, blockParameter)
        
        End Function

        
        Public Function TokenOfOwnerByIndexQueryAsync(ByVal [owner] As String, ByVal [index] As BigInteger, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Dim tokenOfOwnerByIndexFunction = New TokenOfOwnerByIndexFunction()
                tokenOfOwnerByIndexFunction.Owner = [owner]
                tokenOfOwnerByIndexFunction.Index = [index]
            
            Return ContractHandler.QueryAsync(Of TokenOfOwnerByIndexFunction, BigInteger)(tokenOfOwnerByIndexFunction, blockParameter)
        
        End Function


        Public Function TokenURIQueryAsync(ByVal tokenURIFunction As TokenURIFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Return ContractHandler.QueryAsync(Of TokenURIFunction, String)(tokenURIFunction, blockParameter)
        
        End Function

        
        Public Function TokenURIQueryAsync(ByVal [tokenId] As BigInteger, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of String)
        
            Dim tokenURIFunction = New TokenURIFunction()
                tokenURIFunction.TokenId = [tokenId]
            
            Return ContractHandler.QueryAsync(Of TokenURIFunction, String)(tokenURIFunction, blockParameter)
        
        End Function


        Public Function TotalSupplyQueryAsync(ByVal totalSupplyFunction As TotalSupplyFunction, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            Return ContractHandler.QueryAsync(Of TotalSupplyFunction, BigInteger)(totalSupplyFunction, blockParameter)
        
        End Function

        
        Public Function TotalSupplyQueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of BigInteger)
        
            return ContractHandler.QueryAsync(Of TotalSupplyFunction, BigInteger)(Nothing, blockParameter)
        
        End Function



        Public Function TransferFromRequestAsync(ByVal transferFromFunction As TransferFromFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of TransferFromFunction)(transferFromFunction)
        
        End Function

        Public Function TransferFromRequestAndWaitForReceiptAsync(ByVal transferFromFunction As TransferFromFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of TransferFromFunction)(transferFromFunction, cancellationToken)
        
        End Function

        
        Public Function TransferFromRequestAsync(ByVal [from] As String, ByVal [to] As String, ByVal [tokenId] As BigInteger) As Task(Of String)
        
            Dim transferFromFunction = New TransferFromFunction()
                transferFromFunction.From = [from]
                transferFromFunction.To = [to]
                transferFromFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAsync(Of TransferFromFunction)(transferFromFunction)
        
        End Function

        
        Public Function TransferFromRequestAndWaitForReceiptAsync(ByVal [from] As String, ByVal [to] As String, ByVal [tokenId] As BigInteger, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Dim transferFromFunction = New TransferFromFunction()
                transferFromFunction.From = [from]
                transferFromFunction.To = [to]
                transferFromFunction.TokenId = [tokenId]
            
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of TransferFromFunction)(transferFromFunction, cancellationToken)
        
        End Function
        Public Function UnpauseRequestAsync(ByVal unpauseFunction As UnpauseFunction) As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of UnpauseFunction)(unpauseFunction)
        
        End Function

        Public Function UnpauseRequestAsync() As Task(Of String)
                    
            Return ContractHandler.SendRequestAsync(Of UnpauseFunction)
        
        End Function

        Public Function UnpauseRequestAndWaitForReceiptAsync(ByVal unpauseFunction As UnpauseFunction, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of UnpauseFunction)(unpauseFunction, cancellationToken)
        
        End Function

        Public Function UnpauseRequestAndWaitForReceiptAsync(ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
        
            Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of UnpauseFunction)(Nothing, cancellationToken)
        
        End Function
    
    End Class

End Namespace
