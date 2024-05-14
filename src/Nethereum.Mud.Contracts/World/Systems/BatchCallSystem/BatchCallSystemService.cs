using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using System.Threading;

namespace Nethereum.Mud.Contracts.World.Systems.BatchCallSystem
{
    public partial class BatchCallSystemService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, BatchCallSystemDeployment batchCallSystemDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BatchCallSystemDeployment>().SendRequestAndWaitForReceiptAsync(batchCallSystemDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, BatchCallSystemDeployment batchCallSystemDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BatchCallSystemDeployment>().SendRequestAsync(batchCallSystemDeployment);
        }

        public static async Task<BatchCallSystemService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, BatchCallSystemDeployment batchCallSystemDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, batchCallSystemDeployment, cancellationTokenSource);
            return new BatchCallSystemService(web3, receipt.ContractAddress);
        }

        public BatchCallSystemService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> MsgSenderQueryAsync(MsgSenderFunction msgSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(msgSenderFunction, blockParameter);
        }

        
        public Task<string> MsgSenderQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> MsgValueQueryAsync(MsgValueFunction msgValueFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgValueFunction, BigInteger>(msgValueFunction, blockParameter);
        }

        
        public Task<BigInteger> MsgValueQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgValueFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> WorldQueryAsync(WorldFunction worldFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldFunction, string>(worldFunction, blockParameter);
        }

        
        public Task<string> WorldQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldFunction, string>(null, blockParameter);
        }

        public Task<string> BatchCallRequestAsync(BatchCallFunction batchCallFunction)
        {
             return ContractHandler.SendRequestAsync(batchCallFunction);
        }

        public Task<TransactionReceipt> BatchCallRequestAndWaitForReceiptAsync(BatchCallFunction batchCallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(batchCallFunction, cancellationToken);
        }

        public Task<string> BatchCallRequestAsync(List<SystemCallData> systemCalls)
        {
            var batchCallFunction = new BatchCallFunction();
                batchCallFunction.SystemCalls = systemCalls;
            
             return ContractHandler.SendRequestAsync(batchCallFunction);
        }

        public Task<TransactionReceipt> BatchCallRequestAndWaitForReceiptAsync(List<SystemCallData> systemCalls, CancellationTokenSource cancellationToken = null)
        {
            var batchCallFunction = new BatchCallFunction();
                batchCallFunction.SystemCalls = systemCalls;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(batchCallFunction, cancellationToken);
        }

        public Task<string> BatchCallFromRequestAsync(BatchCallFromFunction batchCallFromFunction)
        {
             return ContractHandler.SendRequestAsync(batchCallFromFunction);
        }

        public Task<TransactionReceipt> BatchCallFromRequestAndWaitForReceiptAsync(BatchCallFromFunction batchCallFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(batchCallFromFunction, cancellationToken);
        }

        public Task<string> BatchCallFromRequestAsync(List<SystemCallFromData> systemCalls)
        {
            var batchCallFromFunction = new BatchCallFromFunction();
                batchCallFromFunction.SystemCalls = systemCalls;
            
             return ContractHandler.SendRequestAsync(batchCallFromFunction);
        }

        public Task<TransactionReceipt> BatchCallFromRequestAndWaitForReceiptAsync(List<SystemCallFromData> systemCalls, CancellationTokenSource cancellationToken = null)
        {
            var batchCallFromFunction = new BatchCallFromFunction();
                batchCallFromFunction.SystemCalls = systemCalls;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(batchCallFromFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MsgSenderFunction),
                typeof(MsgValueFunction),
                typeof(WorldFunction),
                typeof(BatchCallFunction),
                typeof(BatchCallFromFunction),
                typeof(SupportsInterfaceFunction)
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
                typeof(UnauthorizedCallContextError)
            };
        }
    }
}
