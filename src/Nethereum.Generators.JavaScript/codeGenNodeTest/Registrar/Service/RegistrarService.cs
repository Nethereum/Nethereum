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
using Nethereum.ENS.Registrar.ContractDefinition;
namespace Nethereum.ENS.Registrar.Service
{

    public class RegistrarService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3 web3, RegistrarDeployment registrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<RegistrarDeployment>().SendRequestAndWaitForReceiptAsync(registrarDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3 web3, RegistrarDeployment registrarDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<RegistrarDeployment>().SendRequestAsync(registrarDeployment);
        }
        public static async Task<RegistrarService> DeployContractAndGetServiceAsync(Web3 web3, RegistrarDeployment registrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, registrarDeployment, cancellationTokenSource);
            return new RegistrarService(web3, receipt.ContractAddress);
        }
    
        protected Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public RegistrarService(Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> ReleaseDeedRequestAsync(ReleaseDeedFunction releaseDeedFunction)
        {
             return ContractHandler.SendRequestAsync(releaseDeedFunction);
        }
        public Task<TransactionReceipt> ReleaseDeedRequestAndWaitForReceiptAsync(ReleaseDeedFunction releaseDeedFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(releaseDeedFunction, cancellationToken);
        }
        public Task<BigInteger> GetAllowedTimeQueryAsync(GetAllowedTimeFunction getAllowedTimeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAllowedTimeFunction, BigInteger>(getAllowedTimeFunction, blockParameter);
        }
        public Task<string> InvalidateNameRequestAsync(InvalidateNameFunction invalidateNameFunction)
        {
             return ContractHandler.SendRequestAsync(invalidateNameFunction);
        }
        public Task<TransactionReceipt> InvalidateNameRequestAndWaitForReceiptAsync(InvalidateNameFunction invalidateNameFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(invalidateNameFunction, cancellationToken);
        }
        public Task<byte[]> ShaBidQueryAsync(ShaBidFunction shaBidFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ShaBidFunction, byte[]>(shaBidFunction, blockParameter);
        }
        public Task<string> CancelBidRequestAsync(CancelBidFunction cancelBidFunction)
        {
             return ContractHandler.SendRequestAsync(cancelBidFunction);
        }
        public Task<TransactionReceipt> CancelBidRequestAndWaitForReceiptAsync(CancelBidFunction cancelBidFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelBidFunction, cancellationToken);
        }
        public Task<EntriesOutputDTO> EntriesQueryAsync(EntriesFunction entriesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<EntriesFunction, EntriesOutputDTO>(entriesFunction, blockParameter);
        }
        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }        
        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }
        public Task<string> UnsealBidRequestAsync(UnsealBidFunction unsealBidFunction)
        {
             return ContractHandler.SendRequestAsync(unsealBidFunction);
        }
        public Task<TransactionReceipt> UnsealBidRequestAndWaitForReceiptAsync(UnsealBidFunction unsealBidFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unsealBidFunction, cancellationToken);
        }
        public Task<string> TransferRegistrarsRequestAsync(TransferRegistrarsFunction transferRegistrarsFunction)
        {
             return ContractHandler.SendRequestAsync(transferRegistrarsFunction);
        }
        public Task<TransactionReceipt> TransferRegistrarsRequestAndWaitForReceiptAsync(TransferRegistrarsFunction transferRegistrarsFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferRegistrarsFunction, cancellationToken);
        }
        public Task<string> SealedBidsQueryAsync(SealedBidsFunction sealedBidsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SealedBidsFunction, string>(sealedBidsFunction, blockParameter);
        }
        public Task<byte> StateQueryAsync(StateFunction stateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<StateFunction, byte>(stateFunction, blockParameter);
        }
        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }
        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }
        public Task<bool> IsAllowedQueryAsync(IsAllowedFunction isAllowedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsAllowedFunction, bool>(isAllowedFunction, blockParameter);
        }
        public Task<string> FinalizeAuctionRequestAsync(FinalizeAuctionFunction finalizeAuctionFunction)
        {
             return ContractHandler.SendRequestAsync(finalizeAuctionFunction);
        }
        public Task<TransactionReceipt> FinalizeAuctionRequestAndWaitForReceiptAsync(FinalizeAuctionFunction finalizeAuctionFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeAuctionFunction, cancellationToken);
        }
        public Task<BigInteger> RegistryStartedQueryAsync(RegistryStartedFunction registryStartedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistryStartedFunction, BigInteger>(registryStartedFunction, blockParameter);
        }        
        public Task<BigInteger> RegistryStartedQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistryStartedFunction, BigInteger>(null, blockParameter);
        }
        public Task<uint> LaunchLengthQueryAsync(LaunchLengthFunction launchLengthFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LaunchLengthFunction, uint>(launchLengthFunction, blockParameter);
        }        
        public Task<uint> LaunchLengthQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LaunchLengthFunction, uint>(null, blockParameter);
        }
        public Task<string> NewBidRequestAsync(NewBidFunction newBidFunction)
        {
             return ContractHandler.SendRequestAsync(newBidFunction);
        }
        public Task<TransactionReceipt> NewBidRequestAndWaitForReceiptAsync(NewBidFunction newBidFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(newBidFunction, cancellationToken);
        }
        public Task<string> EraseNodeRequestAsync(EraseNodeFunction eraseNodeFunction)
        {
             return ContractHandler.SendRequestAsync(eraseNodeFunction);
        }
        public Task<TransactionReceipt> EraseNodeRequestAndWaitForReceiptAsync(EraseNodeFunction eraseNodeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(eraseNodeFunction, cancellationToken);
        }
        public Task<string> StartAuctionsRequestAsync(StartAuctionsFunction startAuctionsFunction)
        {
             return ContractHandler.SendRequestAsync(startAuctionsFunction);
        }
        public Task<TransactionReceipt> StartAuctionsRequestAndWaitForReceiptAsync(StartAuctionsFunction startAuctionsFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionsFunction, cancellationToken);
        }
        public Task<string> AcceptRegistrarTransferRequestAsync(AcceptRegistrarTransferFunction acceptRegistrarTransferFunction)
        {
             return ContractHandler.SendRequestAsync(acceptRegistrarTransferFunction);
        }
        public Task<TransactionReceipt> AcceptRegistrarTransferRequestAndWaitForReceiptAsync(AcceptRegistrarTransferFunction acceptRegistrarTransferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(acceptRegistrarTransferFunction, cancellationToken);
        }
        public Task<string> StartAuctionRequestAsync(StartAuctionFunction startAuctionFunction)
        {
             return ContractHandler.SendRequestAsync(startAuctionFunction);
        }
        public Task<TransactionReceipt> StartAuctionRequestAndWaitForReceiptAsync(StartAuctionFunction startAuctionFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionFunction, cancellationToken);
        }
        public Task<byte[]> RootNodeQueryAsync(RootNodeFunction rootNodeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootNodeFunction, byte[]>(rootNodeFunction, blockParameter);
        }        
        public Task<byte[]> RootNodeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootNodeFunction, byte[]>(null, blockParameter);
        }
        public Task<string> StartAuctionsAndBidRequestAsync(StartAuctionsAndBidFunction startAuctionsAndBidFunction)
        {
             return ContractHandler.SendRequestAsync(startAuctionsAndBidFunction);
        }
        public Task<TransactionReceipt> StartAuctionsAndBidRequestAndWaitForReceiptAsync(StartAuctionsAndBidFunction startAuctionsAndBidFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionsAndBidFunction, cancellationToken);
        }
    }
}
