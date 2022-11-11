using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Reactive.Streams;

namespace Nethereum.RPC.Reactive.Polling.Streams
{
    public sealed class PendingTransactionStreamProvider : IPendingTransactionStreamProvider
    {
        private readonly IEthApiFilterService _filterService;
        private readonly IObservable<Unit> _poller;
        private readonly IEthApiTransactionsService _transactionsService;

        public PendingTransactionStreamProvider(
            IObservable<Unit> poller,
            IEthApiFilterService filterService,
            IEthApiTransactionsService transactionsService)
        {
            _poller = poller;
            _filterService = filterService;
            _transactionsService = transactionsService;
        }

        public IObservable<Transaction> GetPendingTransactions() => ObservableExtensions
            .Using(
                async () => new DisposableFilter(await _filterService.NewPendingTransactionFilter.SendRequestAsync().ConfigureAwait(false), _filterService.UninstallFilter),
                filter => Observable
                    .FromAsync(ct => _filterService.GetFilterChangesForBlockOrTransaction.SendRequestAsync(filter.ID))
                    .SelectMany(transactionHashes => transactionHashes
                        .Select(hash => Observable.FromAsync(ct => _transactionsService.GetTransactionByHash.SendRequestAsync(hash)))
                        .ToObservable())
                    .SelectMany(x => x)
                    .Poll(_poller))
            .Publish()
            .RefCount();
    }
}