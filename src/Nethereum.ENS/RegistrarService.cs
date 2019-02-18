using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.ENS.Registrar.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ENS
{

    public partial class RegistrarService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, RegistrarDeployment registrarDeployment, CancellationToken token = default(CancellationToken))
        {
            return web3.Eth.GetContractDeploymentHandler<RegistrarDeployment>().SendRequestAndWaitForReceiptAsync(registrarDeployment, token);
        }
        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, RegistrarDeployment registrarDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<RegistrarDeployment>().SendRequestAsync(registrarDeployment);
        }
        public static async Task<RegistrarService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, RegistrarDeployment registrarDeployment, CancellationToken token = default(CancellationToken))
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, registrarDeployment, token);
            return new RegistrarService(web3, receipt.ContractAddress);
        }
    
        protected Nethereum.Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public RegistrarService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> ReleaseDeedRequestAsync(ReleaseDeedFunction releaseDeedFunction)
        {
             return ContractHandler.SendRequestAsync(releaseDeedFunction);
        }

        public Task<TransactionReceipt> ReleaseDeedRequestAndWaitForReceiptAsync(ReleaseDeedFunction releaseDeedFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(releaseDeedFunction, token);
        }

        public Task<string> ReleaseDeedRequestAsync(byte[] hash)
        {
            var releaseDeedFunction = new ReleaseDeedFunction();
                releaseDeedFunction.Hash = hash;
            
             return ContractHandler.SendRequestAsync(releaseDeedFunction);
        }

        public Task<TransactionReceipt> ReleaseDeedRequestAndWaitForReceiptAsync(byte[] hash, CancellationToken token = default(CancellationToken))
        {
            var releaseDeedFunction = new ReleaseDeedFunction();
                releaseDeedFunction.Hash = hash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(releaseDeedFunction, token);
        }

        public Task<BigInteger> GetAllowedTimeQueryAsync(GetAllowedTimeFunction getAllowedTimeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAllowedTimeFunction, BigInteger>(getAllowedTimeFunction, blockParameter);
        }

        
        public Task<BigInteger> GetAllowedTimeQueryAsync(byte[] hash, BlockParameter blockParameter = null)
        {
            var getAllowedTimeFunction = new GetAllowedTimeFunction();
                getAllowedTimeFunction.Hash = hash;
            
            return ContractHandler.QueryAsync<GetAllowedTimeFunction, BigInteger>(getAllowedTimeFunction, blockParameter);
        }



        public Task<string> InvalidateNameRequestAsync(InvalidateNameFunction invalidateNameFunction)
        {
             return ContractHandler.SendRequestAsync(invalidateNameFunction);
        }

        public Task<TransactionReceipt> InvalidateNameRequestAndWaitForReceiptAsync(InvalidateNameFunction invalidateNameFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(invalidateNameFunction, token);
        }

        public Task<string> InvalidateNameRequestAsync(string unhashedName)
        {
            var invalidateNameFunction = new InvalidateNameFunction();
                invalidateNameFunction.UnhashedName = unhashedName;
            
             return ContractHandler.SendRequestAsync(invalidateNameFunction);
        }

        public Task<TransactionReceipt> InvalidateNameRequestAndWaitForReceiptAsync(string unhashedName, CancellationToken token = default(CancellationToken))
        {
            var invalidateNameFunction = new InvalidateNameFunction();
                invalidateNameFunction.UnhashedName = unhashedName;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(invalidateNameFunction, token);
        }

        public Task<byte[]> ShaBidQueryAsync(ShaBidFunction shaBidFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ShaBidFunction, byte[]>(shaBidFunction, blockParameter);
        }

        
        public Task<byte[]> ShaBidQueryAsync(byte[] hash, string owner, BigInteger value, byte[] salt, BlockParameter blockParameter = null)
        {
            var shaBidFunction = new ShaBidFunction();
                shaBidFunction.Hash = hash;
                shaBidFunction.Owner = owner;
                shaBidFunction.Value = value;
                shaBidFunction.Salt = salt;
            
            return ContractHandler.QueryAsync<ShaBidFunction, byte[]>(shaBidFunction, blockParameter);
        }



        public Task<string> CancelBidRequestAsync(CancelBidFunction cancelBidFunction)
        {
             return ContractHandler.SendRequestAsync(cancelBidFunction);
        }

        public Task<TransactionReceipt> CancelBidRequestAndWaitForReceiptAsync(CancelBidFunction cancelBidFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelBidFunction, token);
        }

        public Task<string> CancelBidRequestAsync(string bidder, byte[] seal)
        {
            var cancelBidFunction = new CancelBidFunction();
                cancelBidFunction.Bidder = bidder;
                cancelBidFunction.Seal = seal;
            
             return ContractHandler.SendRequestAsync(cancelBidFunction);
        }

        public Task<TransactionReceipt> CancelBidRequestAndWaitForReceiptAsync(string bidder, byte[] seal, CancellationToken token = default(CancellationToken))
        {
            var cancelBidFunction = new CancelBidFunction();
                cancelBidFunction.Bidder = bidder;
                cancelBidFunction.Seal = seal;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelBidFunction, token);
        }

        public Task<EntriesOutputDTO> EntriesQueryAsync(EntriesFunction entriesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<EntriesFunction, EntriesOutputDTO>(entriesFunction, blockParameter);
        }

        
        public Task<EntriesOutputDTO> EntriesQueryAsync(byte[] hash, BlockParameter blockParameter = null)
        {
            var entriesFunction = new EntriesFunction();
                entriesFunction.Hash = hash;
            
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

        public Task<TransactionReceipt> UnsealBidRequestAndWaitForReceiptAsync(UnsealBidFunction unsealBidFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unsealBidFunction, token);
        }

        public Task<string> UnsealBidRequestAsync(byte[] hash, BigInteger value, byte[] salt)
        {
            var unsealBidFunction = new UnsealBidFunction();
                unsealBidFunction.Hash = hash;
                unsealBidFunction.Value = value;
                unsealBidFunction.Salt = salt;
            
             return ContractHandler.SendRequestAsync(unsealBidFunction);
        }

        public Task<TransactionReceipt> UnsealBidRequestAndWaitForReceiptAsync(byte[] hash, BigInteger value, byte[] salt, CancellationToken token = default(CancellationToken))
        {
            var unsealBidFunction = new UnsealBidFunction();
                unsealBidFunction.Hash = hash;
                unsealBidFunction.Value = value;
                unsealBidFunction.Salt = salt;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unsealBidFunction, token);
        }

        public Task<string> TransferRegistrarsRequestAsync(TransferRegistrarsFunction transferRegistrarsFunction)
        {
             return ContractHandler.SendRequestAsync(transferRegistrarsFunction);
        }

        public Task<TransactionReceipt> TransferRegistrarsRequestAndWaitForReceiptAsync(TransferRegistrarsFunction transferRegistrarsFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferRegistrarsFunction, token);
        }

        public Task<string> TransferRegistrarsRequestAsync(byte[] hash)
        {
            var transferRegistrarsFunction = new TransferRegistrarsFunction();
                transferRegistrarsFunction.Hash = hash;
            
             return ContractHandler.SendRequestAsync(transferRegistrarsFunction);
        }

        public Task<TransactionReceipt> TransferRegistrarsRequestAndWaitForReceiptAsync(byte[] hash, CancellationToken token = default(CancellationToken))
        {
            var transferRegistrarsFunction = new TransferRegistrarsFunction();
                transferRegistrarsFunction.Hash = hash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferRegistrarsFunction, token);
        }

        public Task<string> SealedBidsQueryAsync(SealedBidsFunction sealedBidsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SealedBidsFunction, string>(sealedBidsFunction, blockParameter);
        }

        
        public Task<string> SealedBidsQueryAsync(string returnValue1, byte[] returnValue2, BlockParameter blockParameter = null)
        {
            var sealedBidsFunction = new SealedBidsFunction();
                sealedBidsFunction.ReturnValue1 = returnValue1;
                sealedBidsFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<SealedBidsFunction, string>(sealedBidsFunction, blockParameter);
        }



        public Task<byte> StateQueryAsync(StateFunction stateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<StateFunction, byte>(stateFunction, blockParameter);
        }

        
        public Task<byte> StateQueryAsync(byte[] hash, BlockParameter blockParameter = null)
        {
            var stateFunction = new StateFunction();
                stateFunction.Hash = hash;
            
            return ContractHandler.QueryAsync<StateFunction, byte>(stateFunction, blockParameter);
        }



        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, token);
        }

        public Task<string> TransferRequestAsync(byte[] hash, string newOwner)
        {
            var transferFunction = new TransferFunction();
                transferFunction.Hash = hash;
                transferFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(byte[] hash, string newOwner, CancellationToken token = default(CancellationToken))
        {
            var transferFunction = new TransferFunction();
                transferFunction.Hash = hash;
                transferFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, token);
        }

        public Task<bool> IsAllowedQueryAsync(IsAllowedFunction isAllowedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsAllowedFunction, bool>(isAllowedFunction, blockParameter);
        }

        
        public Task<bool> IsAllowedQueryAsync(byte[] hash, BigInteger timestamp, BlockParameter blockParameter = null)
        {
            var isAllowedFunction = new IsAllowedFunction();
                isAllowedFunction.Hash = hash;
                isAllowedFunction.Timestamp = timestamp;
            
            return ContractHandler.QueryAsync<IsAllowedFunction, bool>(isAllowedFunction, blockParameter);
        }



        public Task<string> FinalizeAuctionRequestAsync(FinalizeAuctionFunction finalizeAuctionFunction)
        {
             return ContractHandler.SendRequestAsync(finalizeAuctionFunction);
        }

        public Task<TransactionReceipt> FinalizeAuctionRequestAndWaitForReceiptAsync(FinalizeAuctionFunction finalizeAuctionFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeAuctionFunction, token);
        }

        public Task<string> FinalizeAuctionRequestAsync(byte[] hash)
        {
            var finalizeAuctionFunction = new FinalizeAuctionFunction();
                finalizeAuctionFunction.Hash = hash;
            
             return ContractHandler.SendRequestAsync(finalizeAuctionFunction);
        }

        public Task<TransactionReceipt> FinalizeAuctionRequestAndWaitForReceiptAsync(byte[] hash, CancellationToken token = default(CancellationToken))
        {
            var finalizeAuctionFunction = new FinalizeAuctionFunction();
                finalizeAuctionFunction.Hash = hash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(finalizeAuctionFunction, token);
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

        public Task<TransactionReceipt> NewBidRequestAndWaitForReceiptAsync(NewBidFunction newBidFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(newBidFunction, token);
        }

        public Task<string> NewBidRequestAsync(byte[] sealedBid)
        {
            var newBidFunction = new NewBidFunction();
                newBidFunction.SealedBid = sealedBid;
            
             return ContractHandler.SendRequestAsync(newBidFunction);
        }

        public Task<TransactionReceipt> NewBidRequestAndWaitForReceiptAsync(byte[] sealedBid, CancellationToken token = default(CancellationToken))
        {
            var newBidFunction = new NewBidFunction();
                newBidFunction.SealedBid = sealedBid;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(newBidFunction, token);
        }

        public Task<string> EraseNodeRequestAsync(EraseNodeFunction eraseNodeFunction)
        {
             return ContractHandler.SendRequestAsync(eraseNodeFunction);
        }

        public Task<TransactionReceipt> EraseNodeRequestAndWaitForReceiptAsync(EraseNodeFunction eraseNodeFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(eraseNodeFunction, token);
        }

        public Task<string> EraseNodeRequestAsync(List<byte[]> labels)
        {
            var eraseNodeFunction = new EraseNodeFunction();
                eraseNodeFunction.Labels = labels;
            
             return ContractHandler.SendRequestAsync(eraseNodeFunction);
        }

        public Task<TransactionReceipt> EraseNodeRequestAndWaitForReceiptAsync(List<byte[]> labels, CancellationToken token = default(CancellationToken))
        {
            var eraseNodeFunction = new EraseNodeFunction();
                eraseNodeFunction.Labels = labels;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(eraseNodeFunction, token);
        }

        public Task<string> StartAuctionsRequestAsync(StartAuctionsFunction startAuctionsFunction)
        {
             return ContractHandler.SendRequestAsync(startAuctionsFunction);
        }

        public Task<TransactionReceipt> StartAuctionsRequestAndWaitForReceiptAsync(StartAuctionsFunction startAuctionsFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionsFunction, token);
        }

        public Task<string> StartAuctionsRequestAsync(List<byte[]> hashes)
        {
            var startAuctionsFunction = new StartAuctionsFunction();
                startAuctionsFunction.Hashes = hashes;
            
             return ContractHandler.SendRequestAsync(startAuctionsFunction);
        }

        public Task<TransactionReceipt> StartAuctionsRequestAndWaitForReceiptAsync(List<byte[]> hashes, CancellationToken token = default(CancellationToken))
        {
            var startAuctionsFunction = new StartAuctionsFunction();
                startAuctionsFunction.Hashes = hashes;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionsFunction, token);
        }

        public Task<string> AcceptRegistrarTransferRequestAsync(AcceptRegistrarTransferFunction acceptRegistrarTransferFunction)
        {
             return ContractHandler.SendRequestAsync(acceptRegistrarTransferFunction);
        }

        public Task<TransactionReceipt> AcceptRegistrarTransferRequestAndWaitForReceiptAsync(AcceptRegistrarTransferFunction acceptRegistrarTransferFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(acceptRegistrarTransferFunction, token);
        }

        public Task<string> AcceptRegistrarTransferRequestAsync(byte[] hash, string deed, BigInteger registrationDate)
        {
            var acceptRegistrarTransferFunction = new AcceptRegistrarTransferFunction();
                acceptRegistrarTransferFunction.Hash = hash;
                acceptRegistrarTransferFunction.Deed = deed;
                acceptRegistrarTransferFunction.RegistrationDate = registrationDate;
            
             return ContractHandler.SendRequestAsync(acceptRegistrarTransferFunction);
        }

        public Task<TransactionReceipt> AcceptRegistrarTransferRequestAndWaitForReceiptAsync(byte[] hash, string deed, BigInteger registrationDate, CancellationToken token = default(CancellationToken))
        {
            var acceptRegistrarTransferFunction = new AcceptRegistrarTransferFunction();
                acceptRegistrarTransferFunction.Hash = hash;
                acceptRegistrarTransferFunction.Deed = deed;
                acceptRegistrarTransferFunction.RegistrationDate = registrationDate;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(acceptRegistrarTransferFunction, token);
        }

        public Task<string> StartAuctionRequestAsync(StartAuctionFunction startAuctionFunction)
        {
             return ContractHandler.SendRequestAsync(startAuctionFunction);
        }

        public Task<TransactionReceipt> StartAuctionRequestAndWaitForReceiptAsync(StartAuctionFunction startAuctionFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionFunction, token);
        }

        public Task<string> StartAuctionRequestAsync(byte[] hash)
        {
            var startAuctionFunction = new StartAuctionFunction();
                startAuctionFunction.Hash = hash;
            
             return ContractHandler.SendRequestAsync(startAuctionFunction);
        }

        public Task<TransactionReceipt> StartAuctionRequestAndWaitForReceiptAsync(byte[] hash, CancellationToken token = default(CancellationToken))
        {
            var startAuctionFunction = new StartAuctionFunction();
                startAuctionFunction.Hash = hash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionFunction, token);
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

        public Task<TransactionReceipt> StartAuctionsAndBidRequestAndWaitForReceiptAsync(StartAuctionsAndBidFunction startAuctionsAndBidFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionsAndBidFunction, token);
        }

        public Task<string> StartAuctionsAndBidRequestAsync(List<byte[]> hashes, byte[] sealedBid)
        {
            var startAuctionsAndBidFunction = new StartAuctionsAndBidFunction();
                startAuctionsAndBidFunction.Hashes = hashes;
                startAuctionsAndBidFunction.SealedBid = sealedBid;
            
             return ContractHandler.SendRequestAsync(startAuctionsAndBidFunction);
        }

        public Task<TransactionReceipt> StartAuctionsAndBidRequestAndWaitForReceiptAsync(List<byte[]> hashes, byte[] sealedBid, CancellationToken token = default(CancellationToken))
        {
            var startAuctionsAndBidFunction = new StartAuctionsAndBidFunction();
                startAuctionsAndBidFunction.Hashes = hashes;
                startAuctionsAndBidFunction.SealedBid = sealedBid;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(startAuctionsAndBidFunction, token);
        }
    }
}
