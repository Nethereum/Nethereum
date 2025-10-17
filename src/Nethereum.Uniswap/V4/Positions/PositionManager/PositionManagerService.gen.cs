using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V4.Positions.PositionManager.ContractDefinition;

namespace Nethereum.Uniswap.V4.PositionManager
{
    public partial class PositionManagerService: PositionManagerServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, PositionManagerDeployment positionManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PositionManagerDeployment>().SendRequestAndWaitForReceiptAsync(positionManagerDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, PositionManagerDeployment positionManagerDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PositionManagerDeployment>().SendRequestAsync(positionManagerDeployment);
        }

        public static async Task<PositionManagerService> DeployContractAndGetServiceAsync(IWeb3 web3, PositionManagerDeployment positionManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, positionManagerDeployment, cancellationTokenSource);
            return new PositionManagerService(web3, receipt.ContractAddress);
        }

        public PositionManagerService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class PositionManagerServiceBase: ContractWeb3ServiceBase
    {

        public PositionManagerServiceBase(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> DomainSeparatorQueryAsync(DomainSeparatorFunction domainSeparatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(domainSeparatorFunction, blockParameter);
        }

        
        public virtual Task<byte[]> DomainSeparatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(null, blockParameter);
        }

        public Task<string> Weth9QueryAsync(Weth9Function weth9Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Weth9Function, string>(weth9Function, blockParameter);
        }

        
        public virtual Task<string> Weth9QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Weth9Function, string>(null, blockParameter);
        }

        public virtual Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public virtual Task<string> ApproveRequestAsync(string spender, BigInteger id)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Id = id;
            
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger id, CancellationTokenSource cancellationToken = null)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Id = id;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Owner = owner;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<string> GetApprovedQueryAsync(GetApprovedFunction getApprovedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetApprovedFunction, string>(getApprovedFunction, blockParameter);
        }

        
        public virtual Task<string> GetApprovedQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var getApprovedFunction = new GetApprovedFunction();
                getApprovedFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<GetApprovedFunction, string>(getApprovedFunction, blockParameter);
        }

        public virtual Task<GetPoolAndPositionInfoOutputDTO> GetPoolAndPositionInfoQueryAsync(GetPoolAndPositionInfoFunction getPoolAndPositionInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetPoolAndPositionInfoFunction, GetPoolAndPositionInfoOutputDTO>(getPoolAndPositionInfoFunction, blockParameter);
        }

        public virtual Task<GetPoolAndPositionInfoOutputDTO> GetPoolAndPositionInfoQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var getPoolAndPositionInfoFunction = new GetPoolAndPositionInfoFunction();
                getPoolAndPositionInfoFunction.TokenId = tokenId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetPoolAndPositionInfoFunction, GetPoolAndPositionInfoOutputDTO>(getPoolAndPositionInfoFunction, blockParameter);
        }

        public Task<BigInteger> GetPositionLiquidityQueryAsync(GetPositionLiquidityFunction getPositionLiquidityFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPositionLiquidityFunction, BigInteger>(getPositionLiquidityFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetPositionLiquidityQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var getPositionLiquidityFunction = new GetPositionLiquidityFunction();
                getPositionLiquidityFunction.TokenId = tokenId;
            
            return ContractHandler.QueryAsync<GetPositionLiquidityFunction, BigInteger>(getPositionLiquidityFunction, blockParameter);
        }

        public virtual Task<string> InitializePoolRequestAsync(InitializePoolFunction initializePoolFunction)
        {
             return ContractHandler.SendRequestAsync(initializePoolFunction);
        }

        public virtual Task<TransactionReceipt> InitializePoolRequestAndWaitForReceiptAsync(InitializePoolFunction initializePoolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializePoolFunction, cancellationToken);
        }

        public virtual Task<string> InitializePoolRequestAsync(PoolKey key, BigInteger sqrtPriceX96)
        {
            var initializePoolFunction = new InitializePoolFunction();
                initializePoolFunction.Key = key;
                initializePoolFunction.SqrtPriceX96 = sqrtPriceX96;
            
             return ContractHandler.SendRequestAsync(initializePoolFunction);
        }

        public virtual Task<TransactionReceipt> InitializePoolRequestAndWaitForReceiptAsync(PoolKey key, BigInteger sqrtPriceX96, CancellationTokenSource cancellationToken = null)
        {
            var initializePoolFunction = new InitializePoolFunction();
                initializePoolFunction.Key = key;
                initializePoolFunction.SqrtPriceX96 = sqrtPriceX96;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializePoolFunction, cancellationToken);
        }

        public Task<bool> IsApprovedForAllQueryAsync(IsApprovedForAllFunction isApprovedForAllFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }

        
        public virtual Task<bool> IsApprovedForAllQueryAsync(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var isApprovedForAllFunction = new IsApprovedForAllFunction();
                isApprovedForAllFunction.ReturnValue1 = returnValue1;
                isApprovedForAllFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }

        public virtual Task<string> ModifyLiquiditiesRequestAsync(ModifyLiquiditiesFunction modifyLiquiditiesFunction)
        {
             return ContractHandler.SendRequestAsync(modifyLiquiditiesFunction);
        }

        public virtual Task<TransactionReceipt> ModifyLiquiditiesRequestAndWaitForReceiptAsync(ModifyLiquiditiesFunction modifyLiquiditiesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(modifyLiquiditiesFunction, cancellationToken);
        }

        public virtual Task<string> ModifyLiquiditiesRequestAsync(byte[] unlockData, BigInteger deadline)
        {
            var modifyLiquiditiesFunction = new ModifyLiquiditiesFunction();
                modifyLiquiditiesFunction.UnlockData = unlockData;
                modifyLiquiditiesFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(modifyLiquiditiesFunction);
        }

        public virtual Task<TransactionReceipt> ModifyLiquiditiesRequestAndWaitForReceiptAsync(byte[] unlockData, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var modifyLiquiditiesFunction = new ModifyLiquiditiesFunction();
                modifyLiquiditiesFunction.UnlockData = unlockData;
                modifyLiquiditiesFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(modifyLiquiditiesFunction, cancellationToken);
        }

        public virtual Task<string> ModifyLiquiditiesWithoutUnlockRequestAsync(ModifyLiquiditiesWithoutUnlockFunction modifyLiquiditiesWithoutUnlockFunction)
        {
             return ContractHandler.SendRequestAsync(modifyLiquiditiesWithoutUnlockFunction);
        }

        public virtual Task<TransactionReceipt> ModifyLiquiditiesWithoutUnlockRequestAndWaitForReceiptAsync(ModifyLiquiditiesWithoutUnlockFunction modifyLiquiditiesWithoutUnlockFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(modifyLiquiditiesWithoutUnlockFunction, cancellationToken);
        }

        public virtual Task<string> ModifyLiquiditiesWithoutUnlockRequestAsync(byte[] actions, List<byte[]> @params)
        {
            var modifyLiquiditiesWithoutUnlockFunction = new ModifyLiquiditiesWithoutUnlockFunction();
                modifyLiquiditiesWithoutUnlockFunction.Actions = actions;
                modifyLiquiditiesWithoutUnlockFunction.Params = @params;
            
             return ContractHandler.SendRequestAsync(modifyLiquiditiesWithoutUnlockFunction);
        }

        public virtual Task<TransactionReceipt> ModifyLiquiditiesWithoutUnlockRequestAndWaitForReceiptAsync(byte[] actions, List<byte[]> @params, CancellationTokenSource cancellationToken = null)
        {
            var modifyLiquiditiesWithoutUnlockFunction = new ModifyLiquiditiesWithoutUnlockFunction();
                modifyLiquiditiesWithoutUnlockFunction.Actions = actions;
                modifyLiquiditiesWithoutUnlockFunction.Params = @params;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(modifyLiquiditiesWithoutUnlockFunction, cancellationToken);
        }

        public Task<string> MsgSenderQueryAsync(MsgSenderFunction msgSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(msgSenderFunction, blockParameter);
        }

        
        public virtual Task<string> MsgSenderQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(null, blockParameter);
        }

        public virtual Task<string> MulticallRequestAsync(MulticallFunction multicallFunction)
        {
             return ContractHandler.SendRequestAsync(multicallFunction);
        }

        public virtual Task<TransactionReceipt> MulticallRequestAndWaitForReceiptAsync(MulticallFunction multicallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(multicallFunction, cancellationToken);
        }

        public virtual Task<string> MulticallRequestAsync(List<byte[]> data)
        {
            var multicallFunction = new MulticallFunction();
                multicallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(multicallFunction);
        }

        public virtual Task<TransactionReceipt> MulticallRequestAndWaitForReceiptAsync(List<byte[]> data, CancellationTokenSource cancellationToken = null)
        {
            var multicallFunction = new MulticallFunction();
                multicallFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(multicallFunction, cancellationToken);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public virtual Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> NextTokenIdQueryAsync(NextTokenIdFunction nextTokenIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NextTokenIdFunction, BigInteger>(nextTokenIdFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NextTokenIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NextTokenIdFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> NoncesQueryAsync(NoncesFunction noncesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> NoncesQueryAsync(string owner, BigInteger word, BlockParameter blockParameter = null)
        {
            var noncesFunction = new NoncesFunction();
                noncesFunction.Owner = owner;
                noncesFunction.Word = word;
            
            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
        }

        public Task<string> OwnerOfQueryAsync(OwnerOfFunction ownerOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerOfFunction, string>(ownerOfFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerOfQueryAsync(BigInteger id, BlockParameter blockParameter = null)
        {
            var ownerOfFunction = new OwnerOfFunction();
                ownerOfFunction.Id = id;
            
            return ContractHandler.QueryAsync<OwnerOfFunction, string>(ownerOfFunction, blockParameter);
        }

        public virtual Task<string> PermitRequestAsync(Permit1Function permit1Function)
        {
             return ContractHandler.SendRequestAsync(permit1Function);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(Permit1Function permit1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permit1Function, cancellationToken);
        }

        public virtual Task<string> PermitRequestAsync(string spender, BigInteger tokenId, BigInteger deadline, BigInteger nonce, byte[] signature)
        {
            var permit1Function = new Permit1Function();
                permit1Function.Spender = spender;
                permit1Function.TokenId = tokenId;
                permit1Function.Deadline = deadline;
                permit1Function.PermitNonce = nonce;
                permit1Function.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permit1Function);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(string spender, BigInteger tokenId, BigInteger deadline, BigInteger nonce, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permit1Function = new Permit1Function();
                permit1Function.Spender = spender;
                permit1Function.TokenId = tokenId;
                permit1Function.Deadline = deadline;
                permit1Function.PermitNonce = nonce;
                permit1Function.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permit1Function, cancellationToken);
        }

        public virtual Task<string> PermitRequestAsync(PermitFunction permitFunction)
        {
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(PermitFunction permitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public virtual Task<string> PermitRequestAsync(string owner, PermitSingle permitSingle, byte[] signature)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.PermitSingle = permitSingle;
                permitFunction.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public virtual Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(string owner, PermitSingle permitSingle, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.PermitSingle = permitSingle;
                permitFunction.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public Task<string> Permit2QueryAsync(Permit2Function permit2Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Permit2Function, string>(permit2Function, blockParameter);
        }

        
        public virtual Task<string> Permit2QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Permit2Function, string>(null, blockParameter);
        }

        public virtual Task<string> PermitBatchRequestAsync(PermitBatchFunction permitBatchFunction)
        {
             return ContractHandler.SendRequestAsync(permitBatchFunction);
        }

        public virtual Task<TransactionReceipt> PermitBatchRequestAndWaitForReceiptAsync(PermitBatchFunction permitBatchFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitBatchFunction, cancellationToken);
        }

        public virtual Task<string> PermitBatchRequestAsync(string owner, PermitBatch permitBatch, byte[] signature)
        {
            var permitBatchFunction = new PermitBatchFunction();
                permitBatchFunction.Owner = owner;
                permitBatchFunction.PermitBatch = permitBatch;
                permitBatchFunction.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitBatchFunction);
        }

        public virtual Task<TransactionReceipt> PermitBatchRequestAndWaitForReceiptAsync(string owner, PermitBatch permitBatch, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitBatchFunction = new PermitBatchFunction();
                permitBatchFunction.Owner = owner;
                permitBatchFunction.PermitBatch = permitBatch;
                permitBatchFunction.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitBatchFunction, cancellationToken);
        }

        public virtual Task<string> PermitForAllRequestAsync(PermitForAllFunction permitForAllFunction)
        {
             return ContractHandler.SendRequestAsync(permitForAllFunction);
        }

        public virtual Task<TransactionReceipt> PermitForAllRequestAndWaitForReceiptAsync(PermitForAllFunction permitForAllFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitForAllFunction, cancellationToken);
        }

        public virtual Task<string> PermitForAllRequestAsync(string owner, string @operator, bool approved, BigInteger deadline, BigInteger nonce, byte[] signature)
        {
            var permitForAllFunction = new PermitForAllFunction();
                permitForAllFunction.Owner = owner;
                permitForAllFunction.Operator = @operator;
                permitForAllFunction.Approved = approved;
                permitForAllFunction.Deadline = deadline;
                permitForAllFunction.PermitNonce = nonce;
                permitForAllFunction.Signature = signature;
            
             return ContractHandler.SendRequestAsync(permitForAllFunction);
        }

        public virtual Task<TransactionReceipt> PermitForAllRequestAndWaitForReceiptAsync(string owner, string @operator, bool approved, BigInteger deadline, BigInteger nonce, byte[] signature, CancellationTokenSource cancellationToken = null)
        {
            var permitForAllFunction = new PermitForAllFunction();
                permitForAllFunction.Owner = owner;
                permitForAllFunction.Operator = @operator;
                permitForAllFunction.Approved = approved;
                permitForAllFunction.Deadline = deadline;
                permitForAllFunction.PermitNonce = nonce;
                permitForAllFunction.Signature = signature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitForAllFunction, cancellationToken);
        }

        public virtual Task<PoolKeysOutputDTO> PoolKeysQueryAsync(PoolKeysFunction poolKeysFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<PoolKeysFunction, PoolKeysOutputDTO>(poolKeysFunction, blockParameter);
        }

        public virtual Task<PoolKeysOutputDTO> PoolKeysQueryAsync(byte[] poolId, BlockParameter blockParameter = null)
        {
            var poolKeysFunction = new PoolKeysFunction();
                poolKeysFunction.PoolId = poolId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<PoolKeysFunction, PoolKeysOutputDTO>(poolKeysFunction, blockParameter);
        }

        public Task<string> PoolManagerQueryAsync(PoolManagerFunction poolManagerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(poolManagerFunction, blockParameter);
        }

        
        public virtual Task<string> PoolManagerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> PositionInfoQueryAsync(PositionInfoFunction positionInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PositionInfoFunction, BigInteger>(positionInfoFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> PositionInfoQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var positionInfoFunction = new PositionInfoFunction();
                positionInfoFunction.TokenId = tokenId;
            
            return ContractHandler.QueryAsync<PositionInfoFunction, BigInteger>(positionInfoFunction, blockParameter);
        }

        public virtual Task<string> RevokeNonceRequestAsync(RevokeNonceFunction revokeNonceFunction)
        {
             return ContractHandler.SendRequestAsync(revokeNonceFunction);
        }

        public virtual Task<TransactionReceipt> RevokeNonceRequestAndWaitForReceiptAsync(RevokeNonceFunction revokeNonceFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeNonceFunction, cancellationToken);
        }

        public virtual Task<string> RevokeNonceRequestAsync(BigInteger nonce)
        {
            var revokeNonceFunction = new RevokeNonceFunction();
                revokeNonceFunction.RevokeNonce = nonce;
            
             return ContractHandler.SendRequestAsync(revokeNonceFunction);
        }

        public virtual Task<TransactionReceipt> RevokeNonceRequestAndWaitForReceiptAsync(BigInteger nonce, CancellationTokenSource cancellationToken = null)
        {
            var revokeNonceFunction = new RevokeNonceFunction();
                revokeNonceFunction.RevokeNonce = nonce;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeNonceFunction, cancellationToken);
        }

        public virtual Task<string> SafeTransferFromRequestAsync(SafeTransferFromFunction safeTransferFromFunction)
        {
             return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public virtual Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFromFunction safeTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public virtual Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger id)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
                safeTransferFromFunction.From = from;
                safeTransferFromFunction.To = to;
                safeTransferFromFunction.Id = id;
            
             return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public virtual Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger id, CancellationTokenSource cancellationToken = null)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
                safeTransferFromFunction.From = from;
                safeTransferFromFunction.To = to;
                safeTransferFromFunction.Id = id;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public virtual Task<string> SafeTransferFromRequestAsync(SafeTransferFrom1Function safeTransferFrom1Function)
        {
             return ContractHandler.SendRequestAsync(safeTransferFrom1Function);
        }

        public virtual Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFrom1Function safeTransferFrom1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFrom1Function, cancellationToken);
        }

        public virtual Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger id, byte[] data)
        {
            var safeTransferFrom1Function = new SafeTransferFrom1Function();
                safeTransferFrom1Function.From = from;
                safeTransferFrom1Function.To = to;
                safeTransferFrom1Function.Id = id;
                safeTransferFrom1Function.Data = data;
            
             return ContractHandler.SendRequestAsync(safeTransferFrom1Function);
        }

        public virtual Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger id, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var safeTransferFrom1Function = new SafeTransferFrom1Function();
                safeTransferFrom1Function.From = from;
                safeTransferFrom1Function.To = to;
                safeTransferFrom1Function.Id = id;
                safeTransferFrom1Function.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFrom1Function, cancellationToken);
        }

        public virtual Task<string> SetApprovalForAllRequestAsync(SetApprovalForAllFunction setApprovalForAllFunction)
        {
             return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public virtual Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(SetApprovalForAllFunction setApprovalForAllFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public virtual Task<string> SetApprovalForAllRequestAsync(string @operator, bool approved)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
                setApprovalForAllFunction.Operator = @operator;
                setApprovalForAllFunction.Approved = approved;
            
             return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public virtual Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(string @operator, bool approved, CancellationTokenSource cancellationToken = null)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
                setApprovalForAllFunction.Operator = @operator;
                setApprovalForAllFunction.Approved = approved;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public virtual Task<string> SubscribeRequestAsync(SubscribeFunction subscribeFunction)
        {
             return ContractHandler.SendRequestAsync(subscribeFunction);
        }

        public virtual Task<TransactionReceipt> SubscribeRequestAndWaitForReceiptAsync(SubscribeFunction subscribeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(subscribeFunction, cancellationToken);
        }

        public virtual Task<string> SubscribeRequestAsync(BigInteger tokenId, string newSubscriber, byte[] data)
        {
            var subscribeFunction = new SubscribeFunction();
                subscribeFunction.TokenId = tokenId;
                subscribeFunction.NewSubscriber = newSubscriber;
                subscribeFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(subscribeFunction);
        }

        public virtual Task<TransactionReceipt> SubscribeRequestAndWaitForReceiptAsync(BigInteger tokenId, string newSubscriber, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var subscribeFunction = new SubscribeFunction();
                subscribeFunction.TokenId = tokenId;
                subscribeFunction.NewSubscriber = newSubscriber;
                subscribeFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(subscribeFunction, cancellationToken);
        }

        public Task<string> SubscriberQueryAsync(SubscriberFunction subscriberFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SubscriberFunction, string>(subscriberFunction, blockParameter);
        }

        
        public virtual Task<string> SubscriberQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var subscriberFunction = new SubscriberFunction();
                subscriberFunction.TokenId = tokenId;
            
            return ContractHandler.QueryAsync<SubscriberFunction, string>(subscriberFunction, blockParameter);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        
        public virtual Task<string> SymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(null, blockParameter);
        }

        public Task<string> TokenDescriptorQueryAsync(TokenDescriptorFunction tokenDescriptorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenDescriptorFunction, string>(tokenDescriptorFunction, blockParameter);
        }

        
        public virtual Task<string> TokenDescriptorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenDescriptorFunction, string>(null, blockParameter);
        }

        public Task<string> TokenURIQueryAsync(TokenURIFunction tokenURIFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenURIFunction, string>(tokenURIFunction, blockParameter);
        }

        
        public virtual Task<string> TokenURIQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var tokenURIFunction = new TokenURIFunction();
                tokenURIFunction.TokenId = tokenId;
            
            return ContractHandler.QueryAsync<TokenURIFunction, string>(tokenURIFunction, blockParameter);
        }

        public virtual Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(string from, string to, BigInteger id)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Id = id;
            
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger id, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Id = id;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public virtual Task<string> UnlockCallbackRequestAsync(UnlockCallbackFunction unlockCallbackFunction)
        {
             return ContractHandler.SendRequestAsync(unlockCallbackFunction);
        }

        public virtual Task<TransactionReceipt> UnlockCallbackRequestAndWaitForReceiptAsync(UnlockCallbackFunction unlockCallbackFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlockCallbackFunction, cancellationToken);
        }

        public virtual Task<string> UnlockCallbackRequestAsync(byte[] data)
        {
            var unlockCallbackFunction = new UnlockCallbackFunction();
                unlockCallbackFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(unlockCallbackFunction);
        }

        public virtual Task<TransactionReceipt> UnlockCallbackRequestAndWaitForReceiptAsync(byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var unlockCallbackFunction = new UnlockCallbackFunction();
                unlockCallbackFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlockCallbackFunction, cancellationToken);
        }

        public virtual Task<string> UnsubscribeRequestAsync(UnsubscribeFunction unsubscribeFunction)
        {
             return ContractHandler.SendRequestAsync(unsubscribeFunction);
        }

        public virtual Task<TransactionReceipt> UnsubscribeRequestAndWaitForReceiptAsync(UnsubscribeFunction unsubscribeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unsubscribeFunction, cancellationToken);
        }

        public virtual Task<string> UnsubscribeRequestAsync(BigInteger tokenId)
        {
            var unsubscribeFunction = new UnsubscribeFunction();
                unsubscribeFunction.TokenId = tokenId;
            
             return ContractHandler.SendRequestAsync(unsubscribeFunction);
        }

        public virtual Task<TransactionReceipt> UnsubscribeRequestAndWaitForReceiptAsync(BigInteger tokenId, CancellationTokenSource cancellationToken = null)
        {
            var unsubscribeFunction = new UnsubscribeFunction();
                unsubscribeFunction.TokenId = tokenId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unsubscribeFunction, cancellationToken);
        }

        public Task<BigInteger> UnsubscribeGasLimitQueryAsync(UnsubscribeGasLimitFunction unsubscribeGasLimitFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UnsubscribeGasLimitFunction, BigInteger>(unsubscribeGasLimitFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> UnsubscribeGasLimitQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UnsubscribeGasLimitFunction, BigInteger>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DomainSeparatorFunction),
                typeof(Weth9Function),
                typeof(ApproveFunction),
                typeof(BalanceOfFunction),
                typeof(GetApprovedFunction),
                typeof(GetPoolAndPositionInfoFunction),
                typeof(GetPositionLiquidityFunction),
                typeof(InitializePoolFunction),
                typeof(IsApprovedForAllFunction),
                typeof(ModifyLiquiditiesFunction),
                typeof(ModifyLiquiditiesWithoutUnlockFunction),
                typeof(MsgSenderFunction),
                typeof(MulticallFunction),
                typeof(NameFunction),
                typeof(NextTokenIdFunction),
                typeof(NoncesFunction),
                typeof(OwnerOfFunction),
                typeof(Permit1Function),
                typeof(PermitFunction),
                typeof(Permit2Function),
                typeof(PermitBatchFunction),
                typeof(PermitForAllFunction),
                typeof(PoolKeysFunction),
                typeof(PoolManagerFunction),
                typeof(PositionInfoFunction),
                typeof(RevokeNonceFunction),
                typeof(SafeTransferFromFunction),
                typeof(SafeTransferFrom1Function),
                typeof(SetApprovalForAllFunction),
                typeof(SubscribeFunction),
                typeof(SubscriberFunction),
                typeof(SupportsInterfaceFunction),
                typeof(SymbolFunction),
                typeof(TokenDescriptorFunction),
                typeof(TokenURIFunction),
                typeof(TransferFromFunction),
                typeof(UnlockCallbackFunction),
                typeof(UnsubscribeFunction),
                typeof(UnsubscribeGasLimitFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ApprovalEventDTO),
                typeof(ApprovalForAllEventDTO),
                typeof(SubscriptionEventDTO),
                typeof(TransferEventDTO),
                typeof(UnsubscriptionEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AlreadySubscribedError),
                typeof(BurnNotificationRevertedError),
                typeof(ContractLockedError),
                typeof(DeadlinePassedError),
                typeof(DeltaNotNegativeError),
                typeof(DeltaNotPositiveError),
                typeof(GasLimitTooLowError),
                typeof(InputLengthMismatchError),
                typeof(InsufficientBalanceError),
                typeof(InvalidContractSignatureError),
                typeof(InvalidEthSenderError),
                typeof(InvalidSignatureError),
                typeof(InvalidSignatureLengthError),
                typeof(InvalidSignerError),
                typeof(MaximumAmountExceededError),
                typeof(MinimumAmountInsufficientError),
                typeof(ModifyLiquidityNotificationRevertedError),
                typeof(NoCodeSubscriberError),
                typeof(NoSelfPermitError),
                typeof(NonceAlreadyUsedError),
                typeof(NotApprovedError),
                typeof(NotPoolManagerError),
                typeof(NotSubscribedError),
                typeof(PoolManagerMustBeLockedError),
                typeof(SignatureDeadlineExpiredError),
                typeof(SubscriptionRevertedError),
                typeof(UnauthorizedError),
                typeof(UnsupportedActionError)
            };
        }
    }
}
