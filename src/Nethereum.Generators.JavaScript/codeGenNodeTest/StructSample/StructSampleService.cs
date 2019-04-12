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
using System.Threading;
using Nethereum.Structs.StructSample.ContractDefinition;

namespace Nethereum.Structs.StructSample
{
    public partial class StructSampleService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, StructSampleDeployment structSampleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<StructSampleDeployment>().SendRequestAndWaitForReceiptAsync(structSampleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, StructSampleDeployment structSampleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<StructSampleDeployment>().SendRequestAsync(structSampleDeployment);
        }

        public static async Task<StructSampleService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, StructSampleDeployment structSampleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, structSampleDeployment, cancellationTokenSource);
            return new StructSampleService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public StructSampleService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> SetPurchaseOrderRequestAsync(SetPurchaseOrderFunction setPurchaseOrderFunction)
        {
             return ContractHandler.SendRequestAsync(setPurchaseOrderFunction);
        }

        public Task<TransactionReceipt> SetPurchaseOrderRequestAndWaitForReceiptAsync(SetPurchaseOrderFunction setPurchaseOrderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPurchaseOrderFunction, cancellationToken);
        }

        public Task<string> SetPurchaseOrderRequestAsync(PurchaseOrder purchaseOrder)
        {
            var setPurchaseOrderFunction = new SetPurchaseOrderFunction();
                setPurchaseOrderFunction.PurchaseOrder = purchaseOrder;
            
             return ContractHandler.SendRequestAsync(setPurchaseOrderFunction);
        }

        public Task<TransactionReceipt> SetPurchaseOrderRequestAndWaitForReceiptAsync(PurchaseOrder purchaseOrder, CancellationTokenSource cancellationToken = null)
        {
            var setPurchaseOrderFunction = new SetPurchaseOrderFunction();
                setPurchaseOrderFunction.PurchaseOrder = purchaseOrder;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPurchaseOrderFunction, cancellationToken);
        }

        public Task<PurchaseOrder> GetPurchaseOrderQueryAsync(GetPurchaseOrderFunction getPurchaseOrderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPurchaseOrderFunction, PurchaseOrder>(getPurchaseOrderFunction, blockParameter);
        }

        
        public Task<PurchaseOrder> GetPurchaseOrderQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPurchaseOrderFunction, PurchaseOrder>(null, blockParameter);
        }
    }
}
