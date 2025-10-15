using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.UniversalRouter.ContractDefinition;

namespace Nethereum.Uniswap.UniversalRouter
{
    public partial class UniversalRouterService: UniversalRouterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, UniversalRouterDeployment universalRouterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniversalRouterDeployment>().SendRequestAndWaitForReceiptAsync(universalRouterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, UniversalRouterDeployment universalRouterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniversalRouterDeployment>().SendRequestAsync(universalRouterDeployment);
        }

        public static async Task<UniversalRouterService> DeployContractAndGetServiceAsync(IWeb3 web3, UniversalRouterDeployment universalRouterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, universalRouterDeployment, cancellationTokenSource);
            return new UniversalRouterService(web3, receipt.ContractAddress);
        }

        public UniversalRouterService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class UniversalRouterServiceBase: ContractWeb3ServiceBase
    {

        public UniversalRouterServiceBase(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> V3PositionManagerQueryAsync(V3PositionManagerFunction v3PositionManagerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<V3PositionManagerFunction, string>(v3PositionManagerFunction, blockParameter);
        }

        
        public virtual Task<string> V3PositionManagerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<V3PositionManagerFunction, string>(null, blockParameter);
        }

        public Task<string> V4PositionManagerQueryAsync(V4PositionManagerFunction v4PositionManagerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<V4PositionManagerFunction, string>(v4PositionManagerFunction, blockParameter);
        }

        
        public virtual Task<string> V4PositionManagerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<V4PositionManagerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> ExecuteRequestAsync(ExecuteFunction executeFunction)
        {
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(ExecuteFunction executeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(byte[] commands, List<byte[]> inputs)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Commands = commands;
                executeFunction.Inputs = inputs;
            
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(byte[] commands, List<byte[]> inputs, CancellationTokenSource cancellationToken = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Commands = commands;
                executeFunction.Inputs = inputs;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(Execute1Function execute1Function)
        {
             return ContractHandler.SendRequestAsync(execute1Function);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(Execute1Function execute1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(execute1Function, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(byte[] commands, List<byte[]> inputs, BigInteger deadline)
        {
            var execute1Function = new Execute1Function();
                execute1Function.Commands = commands;
                execute1Function.Inputs = inputs;
                execute1Function.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(execute1Function);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(byte[] commands, List<byte[]> inputs, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var execute1Function = new Execute1Function();
                execute1Function.Commands = commands;
                execute1Function.Inputs = inputs;
                execute1Function.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(execute1Function, cancellationToken);
        }

        public Task<string> MsgSenderQueryAsync(MsgSenderFunction msgSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(msgSenderFunction, blockParameter);
        }

        
        public virtual Task<string> MsgSenderQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(null, blockParameter);
        }

        public Task<string> PoolManagerQueryAsync(PoolManagerFunction poolManagerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(poolManagerFunction, blockParameter);
        }

        
        public virtual Task<string> PoolManagerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> UniswapV3SwapCallbackRequestAsync(UniswapV3SwapCallbackFunction uniswapV3SwapCallbackFunction)
        {
             return ContractHandler.SendRequestAsync(uniswapV3SwapCallbackFunction);
        }

        public virtual Task<TransactionReceipt> UniswapV3SwapCallbackRequestAndWaitForReceiptAsync(UniswapV3SwapCallbackFunction uniswapV3SwapCallbackFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(uniswapV3SwapCallbackFunction, cancellationToken);
        }

        public virtual Task<string> UniswapV3SwapCallbackRequestAsync(BigInteger amount0Delta, BigInteger amount1Delta, byte[] data)
        {
            var uniswapV3SwapCallbackFunction = new UniswapV3SwapCallbackFunction();
                uniswapV3SwapCallbackFunction.Amount0Delta = amount0Delta;
                uniswapV3SwapCallbackFunction.Amount1Delta = amount1Delta;
                uniswapV3SwapCallbackFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(uniswapV3SwapCallbackFunction);
        }

        public virtual Task<TransactionReceipt> UniswapV3SwapCallbackRequestAndWaitForReceiptAsync(BigInteger amount0Delta, BigInteger amount1Delta, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var uniswapV3SwapCallbackFunction = new UniswapV3SwapCallbackFunction();
                uniswapV3SwapCallbackFunction.Amount0Delta = amount0Delta;
                uniswapV3SwapCallbackFunction.Amount1Delta = amount1Delta;
                uniswapV3SwapCallbackFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(uniswapV3SwapCallbackFunction, cancellationToken);
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(V3PositionManagerFunction),
                typeof(V4PositionManagerFunction),
                typeof(ExecuteFunction),
                typeof(Execute1Function),
                typeof(MsgSenderFunction),
                typeof(PoolManagerFunction),
                typeof(UniswapV3SwapCallbackFunction),
                typeof(UnlockCallbackFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(BalanceTooLowError),
                typeof(ContractLockedError),
                typeof(DeltaNotNegativeError),
                typeof(DeltaNotPositiveError),
                typeof(ETHNotAcceptedError),
                typeof(ExecutionFailedError),
                typeof(FromAddressIsNotOwnerError),
                typeof(InputLengthMismatchError),
                typeof(InsufficientBalanceError),
                typeof(InsufficientETHError),
                typeof(InsufficientTokenError),
                typeof(InvalidActionError),
                typeof(InvalidBipsError),
                typeof(InvalidCommandTypeError),
                typeof(InvalidEthSenderError),
                typeof(InvalidPathError),
                typeof(InvalidReservesError),
                typeof(LengthMismatchError),
                typeof(NotAuthorizedForTokenError),
                typeof(NotPoolManagerError),
                typeof(OnlyMintAllowedError),
                typeof(SliceOutOfBoundsError),
                typeof(TransactionDeadlinePassedError),
                typeof(UnsafeCastError),
                typeof(UnsupportedActionError),
                typeof(V2InvalidPathError),
                typeof(V2TooLittleReceivedError),
                typeof(V2TooMuchRequestedError),
                typeof(V3InvalidAmountOutError),
                typeof(V3InvalidCallerError),
                typeof(V3InvalidSwapError),
                typeof(V3TooLittleReceivedError),
                typeof(V3TooMuchRequestedError),
                typeof(V4TooLittleReceivedError),
                typeof(V4TooMuchRequestedError)
            };
        }
    }
}
