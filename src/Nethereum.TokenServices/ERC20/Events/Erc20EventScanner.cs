using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Events
{
    public class Erc20EventScanner : ITokenEventScanner
    {
        private readonly int _numberOfBlocksPerRequest;

        public Erc20EventScanner(int numberOfBlocksPerRequest = 500)
        {
            _numberOfBlocksPerRequest = numberOfBlocksPerRequest;
        }

        public async Task<TokenEventScanResult> ScanTransferEventsAsync(
            IWeb3 web3,
            string accountAddress,
            BigInteger fromBlock,
            BigInteger? toBlock = null,
            CancellationToken cancellationToken = default)
        {
            var result = new TokenEventScanResult();

            try
            {
                var transferEvents = await web3.Processing.Logs.ERC20.GetAllTransferEventsFromAndToAccount(
                    accountAddress,
                    fromBlock,
                    toBlock,
                    cancellationToken,
                    _numberOfBlocksPerRequest
                );

                var normalizedAccount = accountAddress.ToLowerInvariant();

                result.Transfers = transferEvents.Select(e => new TokenTransferEvent
                {
                    TokenAddress = e.Log.Address,
                    From = e.Event.From,
                    To = e.Event.To,
                    Value = e.Event.Value,
                    BlockNumber = e.Log.BlockNumber.Value,
                    TransactionHash = e.Log.TransactionHash,
                    IsIncoming = e.Event.To?.ToLowerInvariant() == normalizedAccount
                }).ToList();

                result.AffectedTokenAddresses = result.Transfers
                    .Select(t => t.TokenAddress)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (result.Transfers.Any())
                {
                    result.LatestBlockScanned = result.Transfers.Max(t => t.BlockNumber);
                }
                else if (toBlock.HasValue)
                {
                    result.LatestBlockScanned = toBlock.Value;
                }
                else
                {
                    var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    result.LatestBlockScanned = currentBlock.Value;
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<List<string>> GetAffectedTokenAddressesAsync(
            IWeb3 web3,
            string accountAddress,
            BigInteger fromBlock,
            BigInteger? toBlock = null,
            CancellationToken cancellationToken = default)
        {
            var scanResult = await ScanTransferEventsAsync(web3, accountAddress, fromBlock, toBlock, cancellationToken);
            return scanResult.AffectedTokenAddresses;
        }
    }
}
