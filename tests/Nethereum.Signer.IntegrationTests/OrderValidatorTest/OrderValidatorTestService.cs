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
using ProtocolContracts.Contracts.OrderValidatorTest.ContractDefinition;

namespace ProtocolContracts.Contracts.OrderValidatorTest
{
    public partial class OrderValidatorTestService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, OrderValidatorTestDeployment orderValidatorTestDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<OrderValidatorTestDeployment>().SendRequestAndWaitForReceiptAsync(orderValidatorTestDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, OrderValidatorTestDeployment orderValidatorTestDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<OrderValidatorTestDeployment>().SendRequestAsync(orderValidatorTestDeployment);
        }

        public static async Task<OrderValidatorTestService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, OrderValidatorTestDeployment orderValidatorTestDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, orderValidatorTestDeployment, cancellationTokenSource);
            return new OrderValidatorTestService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.IWeb3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public OrderValidatorTestService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public OrderValidatorTestService(Nethereum.Web3.IWeb3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<byte[]> EIP712NameHashQueryAsync(EIP712NameHashFunction eIP712NameHashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EIP712NameHashFunction, byte[]>(eIP712NameHashFunction, blockParameter);
        }


        public Task<byte[]> EIP712NameHashQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EIP712NameHashFunction, byte[]>(null, blockParameter);
        }

        public Task<byte[]> EIP712VersionHashQueryAsync(EIP712VersionHashFunction eIP712VersionHashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EIP712VersionHashFunction, byte[]>(eIP712VersionHashFunction, blockParameter);
        }


        public Task<byte[]> EIP712VersionHashQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EIP712VersionHashFunction, byte[]>(null, blockParameter);
        }

        public Task<string> OrdervalidatortestInitRequestAsync(OrdervalidatortestInitFunction ordervalidatortestInitFunction)
        {
            return ContractHandler.SendRequestAsync(ordervalidatortestInitFunction);
        }

        public Task<string> OrdervalidatortestInitRequestAsync()
        {
            return ContractHandler.SendRequestAsync<OrdervalidatortestInitFunction>();
        }

        public Task<TransactionReceipt> OrdervalidatortestInitRequestAndWaitForReceiptAsync(OrdervalidatortestInitFunction ordervalidatortestInitFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(ordervalidatortestInitFunction, cancellationToken);
        }

        public Task<TransactionReceipt> OrdervalidatortestInitRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<OrdervalidatortestInitFunction>(null, cancellationToken);
        }

        public Task<byte[]> DomainSeparatorV4QueryAsync(DomainSeparatorV4Function domainSeparatorV4Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorV4Function, byte[]>(domainSeparatorV4Function, blockParameter);
        }


        public Task<byte[]> DomainSeparatorV4QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorV4Function, byte[]>(null, blockParameter);
        }

        public Task<BigInteger> GetChainIdQueryAsync(GetChainIdFunction getChainIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetChainIdFunction, BigInteger>(getChainIdFunction, blockParameter);
        }


        public Task<BigInteger> GetChainIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetChainIdFunction, BigInteger>(null, blockParameter);
        }

        public Task<byte[]> HashAssetQueryAsync(HashAssetFunction hashAssetFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HashAssetFunction, byte[]>(hashAssetFunction, blockParameter);
        }


        public Task<byte[]> HashAssetQueryAsync(Asset asset, BlockParameter blockParameter = null)
        {
            var hashAssetFunction = new HashAssetFunction();
            hashAssetFunction.Asset = asset;

            return ContractHandler.QueryAsync<HashAssetFunction, byte[]>(hashAssetFunction, blockParameter);
        }

        public Task<byte[]> HashOrderQueryAsync(HashOrderFunction hashOrderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HashOrderFunction, byte[]>(hashOrderFunction, blockParameter);
        }


        public Task<byte[]> HashOrderQueryAsync(Order order, BlockParameter blockParameter = null)
        {
            var hashOrderFunction = new HashOrderFunction();
            hashOrderFunction.Order = order;

            return ContractHandler.QueryAsync<HashOrderFunction, byte[]>(hashOrderFunction, blockParameter);
        }

        public Task<byte[]> HashTypedDataV4QueryAsync(HashTypedDataV4Function hashTypedDataV4Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HashTypedDataV4Function, byte[]>(hashTypedDataV4Function, blockParameter);
        }


        public Task<byte[]> HashTypedDataV4QueryAsync(Order order, BlockParameter blockParameter = null)
        {
            var hashTypedDataV4Function = new HashTypedDataV4Function();
            hashTypedDataV4Function.Order = order;

            return ContractHandler.QueryAsync<HashTypedDataV4Function, byte[]>(hashTypedDataV4Function, blockParameter);
        }

        public Task<bool> ValidateOrderTestQueryAsync(ValidateOrderTestFunction validateOrderTestFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidateOrderTestFunction, bool>(validateOrderTestFunction, blockParameter);
        }


        public Task<bool> ValidateOrderTestQueryAsync(Order order, byte[] signature, BlockParameter blockParameter = null)
        {
            var validateOrderTestFunction = new ValidateOrderTestFunction();
            validateOrderTestFunction.Order = order;
            validateOrderTestFunction.Signature = signature;

            return ContractHandler.QueryAsync<ValidateOrderTestFunction, bool>(validateOrderTestFunction, blockParameter);
        }
    }
}
