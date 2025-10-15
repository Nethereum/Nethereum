using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V4.PoolManager.ContractDefinition;

namespace Nethereum.Uniswap.V4.Contracts.PoolManager
{
    public partial class PoolManagerService: PoolManagerServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, PoolManagerDeployment poolManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PoolManagerDeployment>().SendRequestAndWaitForReceiptAsync(poolManagerDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, PoolManagerDeployment poolManagerDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PoolManagerDeployment>().SendRequestAsync(poolManagerDeployment);
        }

        public static async Task<PoolManagerService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, PoolManagerDeployment poolManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, poolManagerDeployment, cancellationTokenSource);
            return new PoolManagerService(web3, receipt.ContractAddress);
        }

        public PoolManagerService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class PoolManagerServiceBase: ContractWeb3ServiceBase
    {

        public PoolManagerServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> AllowanceQueryAsync(string owner, string spender, BigInteger id, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
                allowanceFunction.Owner = owner;
                allowanceFunction.Spender = spender;
                allowanceFunction.Id = id;
            
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        public virtual Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public virtual Task<string> ApproveRequestAsync(string spender, BigInteger id, BigInteger amount)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Id = id;
                approveFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger id, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Id = id;
                approveFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> BalanceOfQueryAsync(string owner, BigInteger id, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Owner = owner;
                balanceOfFunction.Id = id;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public virtual Task<string> BurnRequestAsync(BurnFunction burnFunction)
        {
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public virtual Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BurnFunction burnFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public virtual Task<string> BurnRequestAsync(string from, BigInteger id, BigInteger amount)
        {
            var burnFunction = new BurnFunction();
                burnFunction.From = from;
                burnFunction.Id = id;
                burnFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public virtual Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(string from, BigInteger id, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var burnFunction = new BurnFunction();
                burnFunction.From = from;
                burnFunction.Id = id;
                burnFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public virtual Task<string> ClearRequestAsync(ClearFunction clearFunction)
        {
             return ContractHandler.SendRequestAsync(clearFunction);
        }

        public virtual Task<TransactionReceipt> ClearRequestAndWaitForReceiptAsync(ClearFunction clearFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(clearFunction, cancellationToken);
        }

        public virtual Task<string> ClearRequestAsync(string currency, BigInteger amount)
        {
            var clearFunction = new ClearFunction();
                clearFunction.Currency = currency;
                clearFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(clearFunction);
        }

        public virtual Task<TransactionReceipt> ClearRequestAndWaitForReceiptAsync(string currency, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var clearFunction = new ClearFunction();
                clearFunction.Currency = currency;
                clearFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(clearFunction, cancellationToken);
        }

        public virtual Task<string> CollectProtocolFeesRequestAsync(CollectProtocolFeesFunction collectProtocolFeesFunction)
        {
             return ContractHandler.SendRequestAsync(collectProtocolFeesFunction);
        }

        public virtual Task<TransactionReceipt> CollectProtocolFeesRequestAndWaitForReceiptAsync(CollectProtocolFeesFunction collectProtocolFeesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(collectProtocolFeesFunction, cancellationToken);
        }

        public virtual Task<string> CollectProtocolFeesRequestAsync(string recipient, string currency, BigInteger amount)
        {
            var collectProtocolFeesFunction = new CollectProtocolFeesFunction();
                collectProtocolFeesFunction.Recipient = recipient;
                collectProtocolFeesFunction.Currency = currency;
                collectProtocolFeesFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(collectProtocolFeesFunction);
        }

        public virtual Task<TransactionReceipt> CollectProtocolFeesRequestAndWaitForReceiptAsync(string recipient, string currency, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var collectProtocolFeesFunction = new CollectProtocolFeesFunction();
                collectProtocolFeesFunction.Recipient = recipient;
                collectProtocolFeesFunction.Currency = currency;
                collectProtocolFeesFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(collectProtocolFeesFunction, cancellationToken);
        }

        public virtual Task<string> DonateRequestAsync(DonateFunction donateFunction)
        {
             return ContractHandler.SendRequestAsync(donateFunction);
        }

        public virtual Task<TransactionReceipt> DonateRequestAndWaitForReceiptAsync(DonateFunction donateFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(donateFunction, cancellationToken);
        }

        public virtual Task<string> DonateRequestAsync(PoolKey key, BigInteger amount0, BigInteger amount1, byte[] hookData)
        {
            var donateFunction = new DonateFunction();
                donateFunction.Key = key;
                donateFunction.Amount0 = amount0;
                donateFunction.Amount1 = amount1;
                donateFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAsync(donateFunction);
        }

        public virtual Task<TransactionReceipt> DonateRequestAndWaitForReceiptAsync(PoolKey key, BigInteger amount0, BigInteger amount1, byte[] hookData, CancellationTokenSource cancellationToken = null)
        {
            var donateFunction = new DonateFunction();
                donateFunction.Key = key;
                donateFunction.Amount0 = amount0;
                donateFunction.Amount1 = amount1;
                donateFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(donateFunction, cancellationToken);
        }

        public Task<byte[]> ExtsloadQueryAsync(ExtsloadFunction extsloadFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ExtsloadFunction, byte[]>(extsloadFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ExtsloadQueryAsync(byte[] slot, BlockParameter blockParameter = null)
        {
            var extsloadFunction = new ExtsloadFunction();
                extsloadFunction.Slot = slot;
            
            return ContractHandler.QueryAsync<ExtsloadFunction, byte[]>(extsloadFunction, blockParameter);
        }

        public Task<List<byte[]>> ExtsloadQueryAsync(Extsload2Function extsload2Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Extsload2Function, List<byte[]>>(extsload2Function, blockParameter);
        }

        
        public virtual Task<List<byte[]>> ExtsloadQueryAsync(byte[] startSlot, BigInteger nSlots, BlockParameter blockParameter = null)
        {
            var extsload2Function = new Extsload2Function();
                extsload2Function.StartSlot = startSlot;
                extsload2Function.NSlots = nSlots;
            
            return ContractHandler.QueryAsync<Extsload2Function, List<byte[]>>(extsload2Function, blockParameter);
        }

        public Task<List<byte[]>> ExtsloadQueryAsync(Extsload1Function extsload1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Extsload1Function, List<byte[]>>(extsload1Function, blockParameter);
        }

        
        public virtual Task<List<byte[]>> ExtsloadQueryAsync(List<byte[]> slots, BlockParameter blockParameter = null)
        {
            var extsload1Function = new Extsload1Function();
                extsload1Function.Slots = slots;
            
            return ContractHandler.QueryAsync<Extsload1Function, List<byte[]>>(extsload1Function, blockParameter);
        }

        public Task<List<byte[]>> ExttloadQueryAsync(ExttloadFunction exttloadFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ExttloadFunction, List<byte[]>>(exttloadFunction, blockParameter);
        }

        
        public virtual Task<List<byte[]>> ExttloadQueryAsync(List<byte[]> slots, BlockParameter blockParameter = null)
        {
            var exttloadFunction = new ExttloadFunction();
                exttloadFunction.Slots = slots;
            
            return ContractHandler.QueryAsync<ExttloadFunction, List<byte[]>>(exttloadFunction, blockParameter);
        }

        public Task<byte[]> ExttloadQueryAsync(Exttload1Function exttload1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Exttload1Function, byte[]>(exttload1Function, blockParameter);
        }

        
        public virtual Task<byte[]> ExttloadQueryAsync(byte[] slot, BlockParameter blockParameter = null)
        {
            var exttload1Function = new Exttload1Function();
                exttload1Function.Slot = slot;
            
            return ContractHandler.QueryAsync<Exttload1Function, byte[]>(exttload1Function, blockParameter);
        }

        public virtual Task<string> InitializeRequestAsync(InitializeFunction initializeFunction)
        {
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(InitializeFunction initializeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public virtual Task<string> InitializeRequestAsync(PoolKey key, BigInteger sqrtPriceX96)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.Key = key;
                initializeFunction.SqrtPriceX96 = sqrtPriceX96;
            
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(PoolKey key, BigInteger sqrtPriceX96, CancellationTokenSource cancellationToken = null)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.Key = key;
                initializeFunction.SqrtPriceX96 = sqrtPriceX96;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<bool> IsOperatorQueryAsync(IsOperatorFunction isOperatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOperatorFunction, bool>(isOperatorFunction, blockParameter);
        }

        
        public virtual Task<bool> IsOperatorQueryAsync(string owner, string @operator, BlockParameter blockParameter = null)
        {
            var isOperatorFunction = new IsOperatorFunction();
                isOperatorFunction.Owner = owner;
                isOperatorFunction.Operator = @operator;
            
            return ContractHandler.QueryAsync<IsOperatorFunction, bool>(isOperatorFunction, blockParameter);
        }

        public virtual Task<string> MintRequestAsync(MintFunction mintFunction)
        {
             return ContractHandler.SendRequestAsync(mintFunction);
        }

        public virtual Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(MintFunction mintFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }

        public virtual Task<string> MintRequestAsync(string to, BigInteger id, BigInteger amount)
        {
            var mintFunction = new MintFunction();
                mintFunction.To = to;
                mintFunction.Id = id;
                mintFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(mintFunction);
        }

        public virtual Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(string to, BigInteger id, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var mintFunction = new MintFunction();
                mintFunction.To = to;
                mintFunction.Id = id;
                mintFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }

        public virtual Task<string> ModifyLiquidityRequestAsync(ModifyLiquidityFunction modifyLiquidityFunction)
        {
             return ContractHandler.SendRequestAsync(modifyLiquidityFunction);
        }

        public virtual Task<TransactionReceipt> ModifyLiquidityRequestAndWaitForReceiptAsync(ModifyLiquidityFunction modifyLiquidityFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(modifyLiquidityFunction, cancellationToken);
        }

        public virtual Task<string> ModifyLiquidityRequestAsync(PoolKey key, ModifyLiquidityParams @params, byte[] hookData)
        {
            var modifyLiquidityFunction = new ModifyLiquidityFunction();
                modifyLiquidityFunction.Key = key;
                modifyLiquidityFunction.Params = @params;
                modifyLiquidityFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAsync(modifyLiquidityFunction);
        }

        public virtual Task<TransactionReceipt> ModifyLiquidityRequestAndWaitForReceiptAsync(PoolKey key, ModifyLiquidityParams @params, byte[] hookData, CancellationTokenSource cancellationToken = null)
        {
            var modifyLiquidityFunction = new ModifyLiquidityFunction();
                modifyLiquidityFunction.Key = key;
                modifyLiquidityFunction.Params = @params;
                modifyLiquidityFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(modifyLiquidityFunction, cancellationToken);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> ProtocolFeeControllerQueryAsync(ProtocolFeeControllerFunction protocolFeeControllerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProtocolFeeControllerFunction, string>(protocolFeeControllerFunction, blockParameter);
        }

        
        public virtual Task<string> ProtocolFeeControllerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProtocolFeeControllerFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> ProtocolFeesAccruedQueryAsync(ProtocolFeesAccruedFunction protocolFeesAccruedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProtocolFeesAccruedFunction, BigInteger>(protocolFeesAccruedFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> ProtocolFeesAccruedQueryAsync(string currency, BlockParameter blockParameter = null)
        {
            var protocolFeesAccruedFunction = new ProtocolFeesAccruedFunction();
                protocolFeesAccruedFunction.Currency = currency;
            
            return ContractHandler.QueryAsync<ProtocolFeesAccruedFunction, BigInteger>(protocolFeesAccruedFunction, blockParameter);
        }

        public virtual Task<string> SetOperatorRequestAsync(SetOperatorFunction setOperatorFunction)
        {
             return ContractHandler.SendRequestAsync(setOperatorFunction);
        }

        public virtual Task<TransactionReceipt> SetOperatorRequestAndWaitForReceiptAsync(SetOperatorFunction setOperatorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOperatorFunction, cancellationToken);
        }

        public virtual Task<string> SetOperatorRequestAsync(string @operator, bool approved)
        {
            var setOperatorFunction = new SetOperatorFunction();
                setOperatorFunction.Operator = @operator;
                setOperatorFunction.Approved = approved;
            
             return ContractHandler.SendRequestAsync(setOperatorFunction);
        }

        public virtual Task<TransactionReceipt> SetOperatorRequestAndWaitForReceiptAsync(string @operator, bool approved, CancellationTokenSource cancellationToken = null)
        {
            var setOperatorFunction = new SetOperatorFunction();
                setOperatorFunction.Operator = @operator;
                setOperatorFunction.Approved = approved;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOperatorFunction, cancellationToken);
        }

        public virtual Task<string> SetProtocolFeeRequestAsync(SetProtocolFeeFunction setProtocolFeeFunction)
        {
             return ContractHandler.SendRequestAsync(setProtocolFeeFunction);
        }

        public virtual Task<TransactionReceipt> SetProtocolFeeRequestAndWaitForReceiptAsync(SetProtocolFeeFunction setProtocolFeeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProtocolFeeFunction, cancellationToken);
        }

        public virtual Task<string> SetProtocolFeeRequestAsync(PoolKey key, uint newProtocolFee)
        {
            var setProtocolFeeFunction = new SetProtocolFeeFunction();
                setProtocolFeeFunction.Key = key;
                setProtocolFeeFunction.NewProtocolFee = newProtocolFee;
            
             return ContractHandler.SendRequestAsync(setProtocolFeeFunction);
        }

        public virtual Task<TransactionReceipt> SetProtocolFeeRequestAndWaitForReceiptAsync(PoolKey key, uint newProtocolFee, CancellationTokenSource cancellationToken = null)
        {
            var setProtocolFeeFunction = new SetProtocolFeeFunction();
                setProtocolFeeFunction.Key = key;
                setProtocolFeeFunction.NewProtocolFee = newProtocolFee;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProtocolFeeFunction, cancellationToken);
        }

        public virtual Task<string> SetProtocolFeeControllerRequestAsync(SetProtocolFeeControllerFunction setProtocolFeeControllerFunction)
        {
             return ContractHandler.SendRequestAsync(setProtocolFeeControllerFunction);
        }

        public virtual Task<TransactionReceipt> SetProtocolFeeControllerRequestAndWaitForReceiptAsync(SetProtocolFeeControllerFunction setProtocolFeeControllerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProtocolFeeControllerFunction, cancellationToken);
        }

        public virtual Task<string> SetProtocolFeeControllerRequestAsync(string controller)
        {
            var setProtocolFeeControllerFunction = new SetProtocolFeeControllerFunction();
                setProtocolFeeControllerFunction.Controller = controller;
            
             return ContractHandler.SendRequestAsync(setProtocolFeeControllerFunction);
        }

        public virtual Task<TransactionReceipt> SetProtocolFeeControllerRequestAndWaitForReceiptAsync(string controller, CancellationTokenSource cancellationToken = null)
        {
            var setProtocolFeeControllerFunction = new SetProtocolFeeControllerFunction();
                setProtocolFeeControllerFunction.Controller = controller;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setProtocolFeeControllerFunction, cancellationToken);
        }

        public virtual Task<string> SettleRequestAsync(SettleFunction settleFunction)
        {
             return ContractHandler.SendRequestAsync(settleFunction);
        }

        public virtual Task<string> SettleRequestAsync()
        {
             return ContractHandler.SendRequestAsync<SettleFunction>();
        }

        public virtual Task<TransactionReceipt> SettleRequestAndWaitForReceiptAsync(SettleFunction settleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(settleFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> SettleRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<SettleFunction>(null, cancellationToken);
        }

        public virtual Task<string> SettleForRequestAsync(SettleForFunction settleForFunction)
        {
             return ContractHandler.SendRequestAsync(settleForFunction);
        }

        public virtual Task<TransactionReceipt> SettleForRequestAndWaitForReceiptAsync(SettleForFunction settleForFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(settleForFunction, cancellationToken);
        }

        public virtual Task<string> SettleForRequestAsync(string recipient)
        {
            var settleForFunction = new SettleForFunction();
                settleForFunction.Recipient = recipient;
            
             return ContractHandler.SendRequestAsync(settleForFunction);
        }

        public virtual Task<TransactionReceipt> SettleForRequestAndWaitForReceiptAsync(string recipient, CancellationTokenSource cancellationToken = null)
        {
            var settleForFunction = new SettleForFunction();
                settleForFunction.Recipient = recipient;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(settleForFunction, cancellationToken);
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

        public virtual Task<string> SwapRequestAsync(SwapFunction swapFunction)
        {
             return ContractHandler.SendRequestAsync(swapFunction);
        }

        public virtual Task<TransactionReceipt> SwapRequestAndWaitForReceiptAsync(SwapFunction swapFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapFunction, cancellationToken);
        }

        public virtual Task<string> SwapRequestAsync(PoolKey key, SwapParams @params, byte[] hookData)
        {
            var swapFunction = new SwapFunction();
                swapFunction.Key = key;
                swapFunction.Params = @params;
                swapFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAsync(swapFunction);
        }

        public virtual Task<TransactionReceipt> SwapRequestAndWaitForReceiptAsync(PoolKey key, SwapParams @params, byte[] hookData, CancellationTokenSource cancellationToken = null)
        {
            var swapFunction = new SwapFunction();
                swapFunction.Key = key;
                swapFunction.Params = @params;
                swapFunction.HookData = hookData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapFunction, cancellationToken);
        }

        public virtual Task<string> SyncRequestAsync(SyncFunction syncFunction)
        {
             return ContractHandler.SendRequestAsync(syncFunction);
        }

        public virtual Task<TransactionReceipt> SyncRequestAndWaitForReceiptAsync(SyncFunction syncFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(syncFunction, cancellationToken);
        }

        public virtual Task<string> SyncRequestAsync(string currency)
        {
            var syncFunction = new SyncFunction();
                syncFunction.Currency = currency;
            
             return ContractHandler.SendRequestAsync(syncFunction);
        }

        public virtual Task<TransactionReceipt> SyncRequestAndWaitForReceiptAsync(string currency, CancellationTokenSource cancellationToken = null)
        {
            var syncFunction = new SyncFunction();
                syncFunction.Currency = currency;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(syncFunction, cancellationToken);
        }

        public virtual Task<string> TakeRequestAsync(TakeFunction takeFunction)
        {
             return ContractHandler.SendRequestAsync(takeFunction);
        }

        public virtual Task<TransactionReceipt> TakeRequestAndWaitForReceiptAsync(TakeFunction takeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(takeFunction, cancellationToken);
        }

        public virtual Task<string> TakeRequestAsync(string currency, string to, BigInteger amount)
        {
            var takeFunction = new TakeFunction();
                takeFunction.Currency = currency;
                takeFunction.To = to;
                takeFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(takeFunction);
        }

        public virtual Task<TransactionReceipt> TakeRequestAndWaitForReceiptAsync(string currency, string to, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var takeFunction = new TakeFunction();
                takeFunction.Currency = currency;
                takeFunction.To = to;
                takeFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(takeFunction, cancellationToken);
        }

        public virtual Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public virtual Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public virtual Task<string> TransferRequestAsync(string receiver, BigInteger id, BigInteger amount)
        {
            var transferFunction = new TransferFunction();
                transferFunction.Receiver = receiver;
                transferFunction.Id = id;
                transferFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public virtual Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string receiver, BigInteger id, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var transferFunction = new TransferFunction();
                transferFunction.Receiver = receiver;
                transferFunction.Id = id;
                transferFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(string sender, string receiver, BigInteger id, BigInteger amount)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.Sender = sender;
                transferFromFunction.Receiver = receiver;
                transferFromFunction.Id = id;
                transferFromFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string sender, string receiver, BigInteger id, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.Sender = sender;
                transferFromFunction.Receiver = receiver;
                transferFromFunction.Id = id;
                transferFromFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> UnlockRequestAsync(UnlockFunction unlockFunction)
        {
             return ContractHandler.SendRequestAsync(unlockFunction);
        }

        public virtual Task<TransactionReceipt> UnlockRequestAndWaitForReceiptAsync(UnlockFunction unlockFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlockFunction, cancellationToken);
        }

        public virtual Task<string> UnlockRequestAsync(byte[] data)
        {
            var unlockFunction = new UnlockFunction();
                unlockFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(unlockFunction);
        }

        public virtual Task<TransactionReceipt> UnlockRequestAndWaitForReceiptAsync(byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var unlockFunction = new UnlockFunction();
                unlockFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlockFunction, cancellationToken);
        }

        public virtual Task<string> UpdateDynamicLPFeeRequestAsync(UpdateDynamicLPFeeFunction updateDynamicLPFeeFunction)
        {
             return ContractHandler.SendRequestAsync(updateDynamicLPFeeFunction);
        }

        public virtual Task<TransactionReceipt> UpdateDynamicLPFeeRequestAndWaitForReceiptAsync(UpdateDynamicLPFeeFunction updateDynamicLPFeeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateDynamicLPFeeFunction, cancellationToken);
        }

        public virtual Task<string> UpdateDynamicLPFeeRequestAsync(PoolKey key, uint newDynamicLPFee)
        {
            var updateDynamicLPFeeFunction = new UpdateDynamicLPFeeFunction();
                updateDynamicLPFeeFunction.Key = key;
                updateDynamicLPFeeFunction.NewDynamicLPFee = newDynamicLPFee;
            
             return ContractHandler.SendRequestAsync(updateDynamicLPFeeFunction);
        }

        public virtual Task<TransactionReceipt> UpdateDynamicLPFeeRequestAndWaitForReceiptAsync(PoolKey key, uint newDynamicLPFee, CancellationTokenSource cancellationToken = null)
        {
            var updateDynamicLPFeeFunction = new UpdateDynamicLPFeeFunction();
                updateDynamicLPFeeFunction.Key = key;
                updateDynamicLPFeeFunction.NewDynamicLPFee = newDynamicLPFee;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateDynamicLPFeeFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AllowanceFunction),
                typeof(ApproveFunction),
                typeof(BalanceOfFunction),
                typeof(BurnFunction),
                typeof(ClearFunction),
                typeof(CollectProtocolFeesFunction),
                typeof(DonateFunction),
                typeof(ExtsloadFunction),
                typeof(Extsload2Function),
                typeof(Extsload1Function),
                typeof(ExttloadFunction),
                typeof(Exttload1Function),
                typeof(InitializeFunction),
                typeof(IsOperatorFunction),
                typeof(MintFunction),
                typeof(ModifyLiquidityFunction),
                typeof(OwnerFunction),
                typeof(ProtocolFeeControllerFunction),
                typeof(ProtocolFeesAccruedFunction),
                typeof(SetOperatorFunction),
                typeof(SetProtocolFeeFunction),
                typeof(SetProtocolFeeControllerFunction),
                typeof(SettleFunction),
                typeof(SettleForFunction),
                typeof(SupportsInterfaceFunction),
                typeof(SwapFunction),
                typeof(SyncFunction),
                typeof(TakeFunction),
                typeof(TransferFunction),
                typeof(TransferFromFunction),
                typeof(TransferOwnershipFunction),
                typeof(UnlockFunction),
                typeof(UpdateDynamicLPFeeFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ApprovalEventDTO),
                typeof(DonateEventDTO),
                typeof(InitializeEventDTO),
                typeof(ModifyLiquidityEventDTO),
                typeof(OperatorSetEventDTO),
                typeof(OwnershipTransferredEventDTO),
                typeof(ProtocolFeeControllerUpdatedEventDTO),
                typeof(ProtocolFeeUpdatedEventDTO),
                typeof(SwapEventDTO),
                typeof(TransferEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AlreadyUnlockedError),
                typeof(CurrenciesOutOfOrderOrEqualError),
                typeof(CurrencyNotSettledError),
                typeof(DelegateCallNotAllowedError),
                typeof(InvalidCallerError),
                typeof(ManagerLockedError),
                typeof(MustClearExactPositiveDeltaError),
                typeof(NonzeroNativeValueError),
                typeof(PoolNotInitializedError),
                typeof(ProtocolFeeCurrencySyncedError),
                typeof(ProtocolFeeTooLargeError),
                typeof(SwapAmountCannotBeZeroError),
                typeof(TickSpacingTooLargeError),
                typeof(TickSpacingTooSmallError),
                typeof(UnauthorizedDynamicLPFeeUpdateError)
            };
        }
    }
}
