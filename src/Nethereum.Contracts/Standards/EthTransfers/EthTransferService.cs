using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.Contracts.Standards.EthTransfers
{
    public class EthTransferService
    {
        private readonly IEthApiContractService _ethApiContractService;

        public EthTransferService(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        /// <summary>The address EIP-7708 emits ETH transfer logs from.</summary>
        public string EmitterAddress => AddressUtil.SYSTEM_ADDRESS;

        public Event<TransferEventDTO> GetEventHandler()
        {
            return _ethApiContractService.GetEvent<TransferEventDTO>(AddressUtil.SYSTEM_ADDRESS);
        }

        public NewFilterInput CreateFilterInput(
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return GetEventHandler().CreateFilterInput(fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInputForSender(
            string sender, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return GetEventHandler().CreateFilterInput(new object[] { sender }, fromBlock, toBlock);
        }

        public NewFilterInput CreateFilterInputForReceiver(
            string receiver, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return GetEventHandler().CreateFilterInput(null, new object[] { receiver }, fromBlock, toBlock);
        }

        public Task<List<EventLog<TransferEventDTO>>> GetAllTransfersAsync(
            BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return GetEventHandler().GetAllChangesAsync(CreateFilterInput(fromBlock, toBlock));
        }

        public Task<List<EventLog<TransferEventDTO>>> GetTransfersFromSenderAsync(
            string sender, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return GetEventHandler().GetAllChangesAsync(
                CreateFilterInputForSender(sender, fromBlock, toBlock));
        }

        public Task<List<EventLog<TransferEventDTO>>> GetTransfersToReceiverAsync(
            string receiver, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            return GetEventHandler().GetAllChangesAsync(
                CreateFilterInputForReceiver(receiver, fromBlock, toBlock));
        }
    }
}
