using System;
using Nethereum.Contracts.Standards.EthTransfers;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public sealed class TransferLogFilter
    {
        private readonly Func<FilterLog, bool> _matches;

        private TransferLogFilter(Func<FilterLog, bool> matches)
        {
            _matches = matches;
        }

        public static readonly TransferLogFilter Everything =
            new TransferLogFilter(log => true);

        public static readonly TransferLogFilter NativeTransfers =
            new TransferLogFilter(log => log.IsFromEthTransferEmitter());

        public TransferLogFilter Negated()
        {
            return new TransferLogFilter(log => !_matches(log));
        }

        public bool Matches(FilterLog log)
        {
            return log != null && _matches(log);
        }

        public Func<FilterLog, bool> AsCriteria()
        {
            return Matches;
        }
    }
}
