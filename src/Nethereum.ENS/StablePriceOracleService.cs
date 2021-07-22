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
using Nethereum.ENS.StablePriceOracle.ContractDefinition;

namespace Nethereum.ENS
{
    public partial class StablePriceOracleService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, StablePriceOracleDeployment stablePriceOracleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<StablePriceOracleDeployment>().SendRequestAndWaitForReceiptAsync(stablePriceOracleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, StablePriceOracleDeployment stablePriceOracleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<StablePriceOracleDeployment>().SendRequestAsync(stablePriceOracleDeployment);
        }

        public static async Task<StablePriceOracleService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, StablePriceOracleDeployment stablePriceOracleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, stablePriceOracleDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new StablePriceOracleService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public StablePriceOracleService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<BigInteger> RentPricesQueryAsync(RentPricesFunction rentPricesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RentPricesFunction, BigInteger>(rentPricesFunction, blockParameter);
        }

        
        public Task<BigInteger> RentPricesQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var rentPricesFunction = new RentPricesFunction();
                rentPricesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<RentPricesFunction, BigInteger>(rentPricesFunction, blockParameter);
        }

        public Task<BigInteger> PriceQueryAsync(PriceFunction priceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceFunction, BigInteger>(priceFunction, blockParameter);
        }

        
        public Task<BigInteger> PriceQueryAsync(string name, BigInteger returnValue2, BigInteger duration, BlockParameter blockParameter = null)
        {
            var priceFunction = new PriceFunction();
                priceFunction.Name = name;
                priceFunction.ReturnValue2 = returnValue2;
                priceFunction.Duration = duration;
            
            return ContractHandler.QueryAsync<PriceFunction, BigInteger>(priceFunction, blockParameter);
        }

        public Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public Task<string> RenounceOwnershipRequestAsync()
        {
             return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public Task<string> SetPricesRequestAsync(SetPricesFunction setPricesFunction)
        {
             return ContractHandler.SendRequestAsync(setPricesFunction);
        }

        public Task<TransactionReceipt> SetPricesRequestAndWaitForReceiptAsync(SetPricesFunction setPricesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPricesFunction, cancellationToken);
        }

        public Task<string> SetPricesRequestAsync(List<BigInteger> rentPrices)
        {
            var setPricesFunction = new SetPricesFunction();
                setPricesFunction.RentPrices = rentPrices;
            
             return ContractHandler.SendRequestAsync(setPricesFunction);
        }

        public Task<TransactionReceipt> SetPricesRequestAndWaitForReceiptAsync(List<BigInteger> rentPrices, CancellationTokenSource cancellationToken = null)
        {
            var setPricesFunction = new SetPricesFunction();
                setPricesFunction.RentPrices = rentPrices;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPricesFunction, cancellationToken);
        }

        public Task<string> SetOracleRequestAsync(SetOracleFunction setOracleFunction)
        {
             return ContractHandler.SendRequestAsync(setOracleFunction);
        }

        public Task<TransactionReceipt> SetOracleRequestAndWaitForReceiptAsync(SetOracleFunction setOracleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOracleFunction, cancellationToken);
        }

        public Task<string> SetOracleRequestAsync(string usdOracle)
        {
            var setOracleFunction = new SetOracleFunction();
                setOracleFunction.UsdOracle = usdOracle;
            
             return ContractHandler.SendRequestAsync(setOracleFunction);
        }

        public Task<TransactionReceipt> SetOracleRequestAndWaitForReceiptAsync(string usdOracle, CancellationTokenSource cancellationToken = null)
        {
            var setOracleFunction = new SetOracleFunction();
                setOracleFunction.UsdOracle = usdOracle;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOracleFunction, cancellationToken);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<bool> IsOwnerQueryAsync(IsOwnerFunction isOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(isOwnerFunction, blockParameter);
        }

        
        public Task<bool> IsOwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(null, blockParameter);
        }

        public Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }
    }
}
