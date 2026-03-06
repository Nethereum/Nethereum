using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.Anchoring.Hub
{
    public class HubRegistrationService : IHubRegistrationService
    {
        private readonly AppChainHubService _hubService;
        private readonly ILogger<HubRegistrationService>? _logger;

        public HubRegistrationService(HubConfig config, ILogger<HubRegistrationService>? logger = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.HubRpcUrl)) throw new ArgumentException("HubRpcUrl is required", nameof(config));
            if (string.IsNullOrEmpty(config.HubContractAddress)) throw new ArgumentException("HubContractAddress is required", nameof(config));
            if (string.IsNullOrEmpty(config.SequencerPrivateKey)) throw new ArgumentException("SequencerPrivateKey is required", nameof(config));

            _logger = logger;
            var account = new Account(config.SequencerPrivateKey, config.HubChainId);
            var web3 = new Web3.Web3(account, config.HubRpcUrl);
            _hubService = new AppChainHubService(web3, config.HubContractAddress);
        }

        public HubRegistrationService(Nethereum.Web3.IWeb3 web3, string hubContractAddress, ILogger<HubRegistrationService>? logger = null)
        {
            if (web3 == null) throw new ArgumentNullException(nameof(web3));
            if (string.IsNullOrEmpty(hubContractAddress)) throw new ArgumentException("hubContractAddress is required", nameof(hubContractAddress));
            _logger = logger;
            _hubService = new AppChainHubService(web3, hubContractAddress);
        }

        public async Task<TransactionReceipt> RegisterAppChainAsync(ulong chainId, string sequencer, byte[] sequencerSignature, BigInteger fee)
        {
            _logger?.LogInformation("Registering AppChain {ChainId} with sequencer {Sequencer}", chainId, sequencer);

            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = sequencer,
                SequencerSignature = sequencerSignature,
                AmountToSend = fee
            };

            return await _hubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);
        }

        public async Task<TransactionReceipt> UpdateMetadataAsync(ulong chainId, string name, string description, string url)
        {
            _logger?.LogInformation("Updating metadata for AppChain {ChainId}", chainId);
            return await _hubService.UpdateMetadataRequestAndWaitForReceiptAsync(chainId, name, description, url);
        }

        public async Task<TransactionReceipt> SetSequencerAsync(ulong chainId, string newSequencer)
        {
            _logger?.LogInformation("Setting sequencer for AppChain {ChainId} to {Sequencer}", chainId, newSequencer);
            return await _hubService.SetSequencerRequestAndWaitForReceiptAsync(chainId, newSequencer);
        }

        public async Task<TransactionReceipt> SetAuthorizedSenderAsync(ulong chainId, string sender, bool authorized)
        {
            _logger?.LogInformation("Setting authorized sender {Sender} for AppChain {ChainId} to {Authorized}",
                sender, chainId, authorized);
            return await _hubService.SetAuthorizedSenderRequestAndWaitForReceiptAsync(chainId, sender, authorized);
        }

        public async Task<TransactionReceipt> SetVerifierAsync(ulong chainId, string verifierAddress)
        {
            _logger?.LogInformation("Setting verifier for AppChain {ChainId} to {Verifier}", chainId, verifierAddress);
            return await _hubService.SetVerifierRequestAndWaitForReceiptAsync(chainId, verifierAddress);
        }

        public async Task<TransactionReceipt> TransferOwnershipAsync(ulong chainId, string newOwner)
        {
            _logger?.LogInformation("Transferring ownership of AppChain {ChainId} to {NewOwner}", chainId, newOwner);
            return await _hubService.TransferAppChainOwnershipRequestAndWaitForReceiptAsync(chainId, newOwner);
        }

        public async Task<HubInfo?> GetAppChainInfoAsync(ulong chainId)
        {
            try
            {
                var result = await _hubService.GetAppChainInfoQueryAsync(chainId);
                if (!result.Registered) return null;

                return new HubInfo
                {
                    ChainId = chainId,
                    Owner = result.Owner,
                    Sequencer = result.Sequencer,
                    LatestBlock = result.LatestBlock,
                    LastProcessedMessageId = result.LastProcessedMessageId,
                    NextMessageId = result.NextMessageId,
                    Registered = result.Registered
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get AppChain info for {ChainId}", chainId);
                return null;
            }
        }

        public async Task<bool> IsAuthorizedSenderAsync(ulong chainId, string sender)
        {
            try
            {
                return await _hubService.AuthorizedSendersQueryAsync(chainId, sender);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to check authorized sender {Sender} for {ChainId}", sender, chainId);
                return false;
            }
        }

        public async Task<TransactionReceipt> WithdrawFeesAsync(ulong chainId)
        {
            _logger?.LogInformation("Withdrawing fees for AppChain {ChainId}", chainId);
            return await _hubService.WithdrawFeesRequestAndWaitForReceiptAsync(chainId);
        }
    }
}
