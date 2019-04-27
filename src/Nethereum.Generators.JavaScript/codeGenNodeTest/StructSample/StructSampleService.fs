namespace Nethereum.Structs.StructSample

open System
open System.Threading.Tasks
open System.Collections.Generic
open System.Numerics
open Nethereum.Hex.HexTypes
open Nethereum.ABI.FunctionEncoding.Attributes
open Nethereum.Web3
open Nethereum.RPC.Eth.DTOs
open Nethereum.Contracts.CQS
open Nethereum.Contracts.ContractHandlers
open Nethereum.Contracts
open System.Threading
open Nethereum.Structs.StructSample.ContractDefinition


    type StructSampleService (web3: Web3, contractAddress: string) =
    
        member val Web3 = web3 with get
        member val ContractHandler = web3.Eth.GetContractHandler(contractAddress) with get
    
        static member DeployContractAndWaitForReceiptAsync(web3: Web3, structSampleDeployment: StructSampleDeployment, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> = 
            let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
            web3.Eth.GetContractDeploymentHandler<StructSampleDeployment>().SendRequestAndWaitForReceiptAsync(structSampleDeployment, cancellationTokenSourceVal)
        
        static member DeployContractAsync(web3: Web3, structSampleDeployment: StructSampleDeployment): Task<string> =
            web3.Eth.GetContractDeploymentHandler<StructSampleDeployment>().SendRequestAsync(structSampleDeployment)
        
        static member DeployContractAndGetServiceAsync(web3: Web3, structSampleDeployment: StructSampleDeployment, ?cancellationTokenSource : CancellationTokenSource) = async {
            let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
            let! receipt = StructSampleService.DeployContractAndWaitForReceiptAsync(web3, structSampleDeployment, cancellationTokenSourceVal) |> Async.AwaitTask
            return new StructSampleService(web3, receipt.ContractAddress);
            }
    
        member this.OwnersQueryAsync(ownersFunction: OwnersFunction, ?blockParameter: BlockParameter): Task<string> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<OwnersFunction, string>(ownersFunction, blockParameterVal)
            
        member this.SubmitTransactionRequestAsync(submitTransactionFunction: SubmitTransactionFunction): Task<string> =
            this.ContractHandler.SendRequestAsync(submitTransactionFunction);
        
        member this.SubmitTransactionRequestAndWaitForReceiptAsync(submitTransactionFunction: SubmitTransactionFunction, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> =
            let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
            this.ContractHandler.SendRequestAndWaitForReceiptAsync(submitTransactionFunction, cancellationTokenSourceVal);
        
        member this.RevokeConfirmationRequestAsync(revokeConfirmationFunction: RevokeConfirmationFunction): Task<string> =
            this.ContractHandler.SendRequestAsync(revokeConfirmationFunction);
        
        member this.RevokeConfirmationRequestAndWaitForReceiptAsync(revokeConfirmationFunction: RevokeConfirmationFunction, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> =
            let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
            this.ContractHandler.SendRequestAndWaitForReceiptAsync(revokeConfirmationFunction, cancellationTokenSourceVal);
        
        member this.IsOwnerQueryAsync(isOwnerFunction: IsOwnerFunction, ?blockParameter: BlockParameter): Task<bool> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<IsOwnerFunction, bool>(isOwnerFunction, blockParameterVal)
            
        member this.ConfirmationsQueryAsync(confirmationsFunction: ConfirmationsFunction, ?blockParameter: BlockParameter): Task<bool> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<ConfirmationsFunction, bool>(confirmationsFunction, blockParameterVal)
            
        member this.GetTransactionCountQueryAsync(getTransactionCountFunction: GetTransactionCountFunction, ?blockParameter: BlockParameter): Task<BigInteger> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<GetTransactionCountFunction, BigInteger>(getTransactionCountFunction, blockParameterVal)
            
        member this.IsConfirmedQueryAsync(isConfirmedFunction: IsConfirmedFunction, ?blockParameter: BlockParameter): Task<bool> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<IsConfirmedFunction, bool>(isConfirmedFunction, blockParameterVal)
            
        member this.GetConfirmationCountQueryAsync(getConfirmationCountFunction: GetConfirmationCountFunction, ?blockParameter: BlockParameter): Task<BigInteger> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<GetConfirmationCountFunction, BigInteger>(getConfirmationCountFunction, blockParameterVal)
            
        member this.TransactionsQueryAsync(transactionsFunction: TransactionsFunction, ?blockParameter: BlockParameter): Task<TransactionsOutputDTO> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryDeserializingToObjectAsync<TransactionsFunction, TransactionsOutputDTO>(transactionsFunction, blockParameterVal)
            
        member this.GetOwnersQueryAsync(getOwnersFunction: GetOwnersFunction, ?blockParameter: BlockParameter): Task<List<string>> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<GetOwnersFunction, List<string>>(getOwnersFunction, blockParameterVal)
            
        member this.GetTransactionIdsQueryAsync(getTransactionIdsFunction: GetTransactionIdsFunction, ?blockParameter: BlockParameter): Task<List<BigInteger>> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<GetTransactionIdsFunction, List<BigInteger>>(getTransactionIdsFunction, blockParameterVal)
            
        member this.GetConfirmationsQueryAsync(getConfirmationsFunction: GetConfirmationsFunction, ?blockParameter: BlockParameter): Task<List<string>> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<GetConfirmationsFunction, List<string>>(getConfirmationsFunction, blockParameterVal)
            
        member this.TransactionCountQueryAsync(transactionCountFunction: TransactionCountFunction, ?blockParameter: BlockParameter): Task<BigInteger> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<TransactionCountFunction, BigInteger>(transactionCountFunction, blockParameterVal)
            
        member this.ConfirmTransactionRequestAsync(confirmTransactionFunction: ConfirmTransactionFunction): Task<string> =
            this.ContractHandler.SendRequestAsync(confirmTransactionFunction);
        
        member this.ConfirmTransactionRequestAndWaitForReceiptAsync(confirmTransactionFunction: ConfirmTransactionFunction, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> =
            let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
            this.ContractHandler.SendRequestAndWaitForReceiptAsync(confirmTransactionFunction, cancellationTokenSourceVal);
        
        member this.MAX_OWNER_COUNTQueryAsync(mAX_OWNER_COUNTFunction: MAX_OWNER_COUNTFunction, ?blockParameter: BlockParameter): Task<BigInteger> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<MAX_OWNER_COUNTFunction, BigInteger>(mAX_OWNER_COUNTFunction, blockParameterVal)
            
        member this.RequiredQueryAsync(requiredFunction: RequiredFunction, ?blockParameter: BlockParameter): Task<BigInteger> =
            let blockParameterVal = defaultArg blockParameter null
            this.ContractHandler.QueryAsync<RequiredFunction, BigInteger>(requiredFunction, blockParameterVal)
            
        member this.ExecuteTransactionRequestAsync(executeTransactionFunction: ExecuteTransactionFunction): Task<string> =
            this.ContractHandler.SendRequestAsync(executeTransactionFunction);
        
        member this.ExecuteTransactionRequestAndWaitForReceiptAsync(executeTransactionFunction: ExecuteTransactionFunction, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> =
            let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
            this.ContractHandler.SendRequestAndWaitForReceiptAsync(executeTransactionFunction, cancellationTokenSourceVal);
        
    

